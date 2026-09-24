using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GrokReserve.Core;

public static class GrokProvider
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    static string? PlanFrom(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        var normalized = Regex.Replace(trimmed.ToLowerInvariant(), @"[_\-\s]", "");
        if (normalized.Contains("heavy")) return "SuperGrok Heavy";
        if (normalized.Contains("supergrok")) return "SuperGrok";
        if (normalized.Contains("premiumplus") || normalized.Contains("premium+")) return "X Premium+";
        if (normalized.Contains("premium")) return "X Premium";
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }

    public static async Task<UsageSnapshot> FetchAsync(CancellationToken ct = default)
    {
        var exe = Locator.FindGrok()
            ?? throw new ProviderException("executableNotFound", "Grok Build CLI is not installed or could not be found.");
        var versionText = await RunAsync(exe, ["--version"], TimeSpan.FromSeconds(5), ct);
        var match = Regex.Match(versionText, @"(\d+)\.(\d+)\.(\d+)");
        if (!match.Success)
            throw new ProviderException("unavailable", $"Grok Build 1.0.0 or newer is required. Found: {versionText}");
        var version = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}";

        var (key, userId) = LoadCredentials();
        using var billing = new HttpRequestMessage(HttpMethod.Get, "https://cli-chat-proxy.grok.com/v1/billing?format=credits");
        ApplyHeaders(billing, key, userId, version);
        using var response = await Http.SendAsync(billing, ct);
        if ((int)response.StatusCode is 401 or 403)
            throw new ProviderException("unauthorized", "Grok authentication expired. Run grok login and refresh.");
        if (!response.IsSuccessStatusCode)
            throw new ProviderException("unavailable", $"Grok billing request returned HTTP {(int)response.StatusCode}.");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var config = root.TryGetProperty("config", out var cfg) ? cfg : default;
        var percent = UsedPercent(config);
        var period = Get(config, "currentPeriod") ?? Get(config, "current_period");
        var start = ParseDate(Str(period, "start") ?? Str(config, "billingPeriodStart") ?? Str(config, "billing_period_start"));
        var end = ParseDate(Str(period, "end") ?? Str(config, "billingPeriodEnd") ?? Str(config, "billing_period_end"));
        var productWindows = ReadProductWindows(config, minutes: null, end);

        if (percent is null || productWindows.Count == 0)
        {
            var grpc = await TryCreditsGrpcAsync(key, userId, version, ct);
            percent ??= grpc.Percent;
            end ??= grpc.End;
            if (productWindows.Count == 0)
                productWindows = grpc.Products;
        }

        var unified = Get(config, "isUnifiedBillingUser") ?? Get(config, "is_unified_billing_user");
        if (percent is null && unified is { ValueKind: JsonValueKind.True } && start is { } && end is { } && end > start)
            percent = 0;
        if (percent is null)
            throw new ProviderException("unavailable", "Grok billing did not include a usage percentage.");

        int? minutes = null;
        if (start is { } s && end is { } e && e > s)
        {
            var value = (e - s).TotalMinutes;
            if (value is >= 1 and <= UsageWindow.MaxWindowMinutes) minutes = (int)value;
        }
        var type = Str(period, "type");
        var label = PeriodLabel(type, minutes);

        var windows = new List<UsageWindow>
        {
            new("usage-pool", label, percent.Value, minutes, end),
        };
        foreach (var product in productWindows)
            windows.Add(new(product.Id, product.Label, product.UsedPercent, minutes, end));

        var tier = Str(root, "subscriptionTier") ?? Str(root, "subscription_tier");
        try
        {
            using var settingsReq = new HttpRequestMessage(HttpMethod.Get, "https://cli-chat-proxy.grok.com/v1/settings");
            ApplyHeaders(settingsReq, key, userId, version);
            settingsReq.Headers.TryAddWithoutValidation("x-grok-client-identifier", "grok-shell");
            using var settings = await Http.SendAsync(settingsReq, ct);
            if (settings.IsSuccessStatusCode)
            {
                using var settingsDoc = JsonDocument.Parse(await settings.Content.ReadAsStringAsync(ct));
                var body = settingsDoc.RootElement;
                tier = Str(body, "subscription_tier_display") ?? Str(body, "subscriptionTierDisplay")
                    ?? Str(body, "subscription_tier") ?? Str(body, "subscriptionTier") ?? tier;
            }
        }
        catch { /* optional */ }

        return new UsageSnapshot
        {
            PlanName = PlanFrom(tier) ?? "",
            Windows = windows,
            FetchedAt = DateTimeOffset.Now,
        };
    }

    static (string Key, string UserId) LoadCredentials()
    {
        var home = Environment.GetEnvironmentVariable("GROK_HOME");
        if (string.IsNullOrWhiteSpace(home))
            home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grok");
        var file = Path.Combine(home, "auth.json");
        JsonDocument doc;
        try
        {
            var raw = File.ReadAllText(file);
            if (raw.Length > 1_048_576) throw new Exception("too large");
            doc = JsonDocument.Parse(raw);
        }
        catch
        {
            throw new ProviderException("credentialsNotFound", "Grok credentials were not found. Run grok login first.");
        }
        using (doc)
        {
            var now = DateTimeOffset.Now;
            var entries = doc.RootElement.EnumerateObject()
                .OrderByDescending(p => p.Name.StartsWith("https://auth.x.ai::", StringComparison.Ordinal))
                .ThenBy(p => p.Name);
            foreach (var entry in entries)
            {
                var expires = ParseDate(Str(entry.Value, "expires_at") ?? Str(entry.Value, "expiresAt"));
                if (expires is { } exp && exp <= now) continue;
                var key = Str(entry.Value, "key")?.Trim();
                var user = (Str(entry.Value, "user_id") ?? Str(entry.Value, "userID"))?.Trim();
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(user))
                    return (key, user);
            }
        }
        throw new ProviderException("unauthorized", "Grok authentication expired. Run grok login and refresh.");
    }

    static void ApplyHeaders(HttpRequestMessage request, string key, string userId, string version)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.TryAddWithoutValidation("X-XAI-Token-Auth", "xai-grok-cli");
        request.Headers.TryAddWithoutValidation("x-userid", userId);
        request.Headers.TryAddWithoutValidation("x-grok-client-version", version);
        request.Headers.TryAddWithoutValidation("x-grok-client-mode", "headless");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    static double? UsedPercent(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object) return null;
        if (config.TryGetProperty("creditUsagePercent", out var a) && a.TryGetDouble(out var p)) return p;
        if (config.TryGetProperty("credit_usage_percent", out var b) && b.TryGetDouble(out var p2)) return p2;
        return null;
    }

    static List<UsageWindow> ReadProductWindows(JsonElement config, int? minutes, DateTimeOffset? end)
    {
        var windows = new List<UsageWindow>();
        var products = Get(config, "productUsage") ?? Get(config, "product_usage");
        if (products is not { ValueKind: JsonValueKind.Array }) return windows;
        foreach (var product in products.Value.EnumerateArray().Take(31))
        {
            JsonElement usageEl;
            if (product.TryGetProperty("usagePercent", out var up)) usageEl = up;
            else if (product.TryGetProperty("usage_percent", out var up2)) usageEl = up2;
            else continue;
            if (!usageEl.TryGetDouble(out var usage)) continue;
            var name = product.TryGetProperty("product", out var p) ? p.GetString() ?? "share" : "share";
            windows.Add(new($"product-{name.ToLowerInvariant()}", ProductLabel(name), usage, minutes, end));
        }
        return windows;
    }

    static string PeriodLabel(string? type, int? minutes)
    {
        if (type?.Contains("weekly", StringComparison.OrdinalIgnoreCase) == true) return "Weekly";
        if (type?.Contains("monthly", StringComparison.OrdinalIgnoreCase) == true) return "Monthly";
        if (minutes is >= 6 * 24 * 60 and <= 8 * 24 * 60) return "Weekly";
        if (minutes is >= 27 * 24 * 60 and <= 32 * 24 * 60) return "Monthly";
        return "Usage pool";
    }

    static string ProductLabel(string product)
    {
        var key = Regex.Replace(product.ToLowerInvariant(), @"[_\-\s]", "");
        return key switch
        {
            "grokbuild" => "Grok Build share",
            "grokchat" => "Grok Chat share",
            "grokimagine" => "Grok Imagine share",
            "grokvoice" => "Grok Voice share",
            "groktasks" or "tasks" or "automations" => "Automations",
            "grokbot" or "bot" => "Grok Bot share",
            _ => $"{product} share",
        };
    }

    static JsonElement? Get(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) ? v : null;

    static string? Str(JsonElement? el, string name) =>
        el is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTimeOffset.TryParse(value, out var date) ? date : null;
    }

    static async Task<string> RunAsync(string exe, string[] args, TimeSpan timeout, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).WaitAsync(timeout, ct);
        return output.Trim();
    }

    sealed record CreditsGrpc(double? Percent, DateTimeOffset? End, List<UsageWindow> Products);

    static async Task<CreditsGrpc> TryCreditsGrpcAsync(string key, string userId, string version, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://grok.com/grok_api_v2.GrokBuildBilling/GetGrokCreditsConfig");
            ApplyHeaders(request, key, userId, version);
            request.Headers.TryAddWithoutValidation("x-grpc-web", "1");
            request.Headers.TryAddWithoutValidation("origin", "https://grok.com");
            request.Content = new ByteArrayContent([0, 0, 0, 0, 0]);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc-web+proto");
            using var response = await Http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return new(null, null, []);
            return ParseCreditsGrpc(await response.Content.ReadAsByteArrayAsync(ct));
        }
        catch
        {
            return new(null, null, []);
        }
    }

    static CreditsGrpc ParseCreditsGrpc(byte[] body)
    {
        var message = GrpcData(body);
        if (message is null) return new(null, null, []);
        var config = ProtoBytes(message, 1) ?? message;
        double? percent = ProtoFloat(config, 1);
        var end = ProtoTimestamp(config, 5) ?? ProtoTimestamp(config, 4);
        var products = new List<UsageWindow>();
        foreach (var item in ProtoRepeated(config, 7))
        {
            var kind = ProtoVarint(item, 1);
            var usage = ProtoFloat(item, 2) ?? 0;
            var name = kind switch
            {
                1 => "Api",
                2 => "GrokBuild",
                4 => "GrokChat",
                5 => "GrokImagine",
                6 => "GrokVoice",
                7 => "GrokBot",
                _ => $"product-{kind}",
            };
            products.Add(new($"product-{name.ToLowerInvariant()}", ProductLabel(name), usage, null, end));
        }
        return new(percent, end, products);
    }

    static byte[]? GrpcData(byte[] body)
    {
        var i = 0;
        while (i + 5 <= body.Length)
        {
            var compressed = body[i];
            var length = (body[i + 1] << 24) | (body[i + 2] << 16) | (body[i + 3] << 8) | body[i + 4];
            i += 5;
            if (length < 0 || i + length > body.Length) return null;
            if (compressed == 0 && length > 0)
                return body[i..(i + length)];
            i += length;
        }
        return null;
    }

    static IEnumerable<byte[]> ProtoRepeated(byte[] data, int field)
    {
        var i = 0;
        while (TryProto(data, ref i, out var tag, out var value, out var bytes))
        {
            if ((tag >> 3) == field && (tag & 7) == 2 && bytes is not null)
                yield return bytes;
        }
    }

    static byte[]? ProtoBytes(byte[] data, int field)
    {
        var i = 0;
        while (TryProto(data, ref i, out var tag, out _, out var bytes))
        {
            if ((tag >> 3) == field && (tag & 7) == 2)
                return bytes;
        }
        return null;
    }

    static double? ProtoFloat(byte[] data, int field)
    {
        var i = 0;
        while (TryProto(data, ref i, out var tag, out var value, out var bytes))
        {
            if ((tag >> 3) != field) continue;
            if ((tag & 7) == 1 && bytes is { Length: 8 })
                return BitConverter.ToDouble(bytes, 0);
            if ((tag & 7) == 5 && bytes is { Length: 4 })
                return BitConverter.ToSingle(bytes, 0);
            if ((tag & 7) == 0)
                return value;
        }
        return null;
    }

    static long? ProtoVarint(byte[] data, int field)
    {
        var i = 0;
        while (TryProto(data, ref i, out var tag, out var value, out _))
        {
            if ((tag >> 3) == field && (tag & 7) == 0)
                return value;
        }
        return null;
    }

    static DateTimeOffset? ProtoTimestamp(byte[] data, int field)
    {
        var payload = ProtoBytes(data, field);
        if (payload is null) return null;
        var seconds = ProtoVarint(payload, 1);
        if (seconds is null) return null;
        try { return DateTimeOffset.FromUnixTimeSeconds(seconds.Value); }
        catch { return null; }
    }

    static bool TryProto(byte[] data, ref int i, out int tag, out long value, out byte[]? bytes)
    {
        tag = 0;
        value = 0;
        bytes = null;
        if (!TryVarint(data, ref i, out var raw)) return false;
        tag = (int)raw;
        switch (tag & 7)
        {
            case 0:
                return TryVarint(data, ref i, out value);
            case 1:
                if (i + 8 > data.Length) return false;
                bytes = data[i..(i + 8)];
                i += 8;
                return true;
            case 2:
                if (!TryVarint(data, ref i, out var length) || length < 0 || i + length > data.Length) return false;
                bytes = data[i..(i + (int)length)];
                i += (int)length;
                return true;
            case 5:
                if (i + 4 > data.Length) return false;
                bytes = data[i..(i + 4)];
                i += 4;
                return true;
            default:
                return false;
        }
    }

    static bool TryVarint(byte[] data, ref int i, out long value)
    {
        value = 0;
        var shift = 0;
        while (i < data.Length && shift < 64)
        {
            var b = data[i++];
            value |= (long)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return true;
            shift += 7;
        }
        return false;
    }
}
