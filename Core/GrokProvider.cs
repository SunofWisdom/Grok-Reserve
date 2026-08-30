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
        if (percent is null)
            throw new ProviderException("unavailable", "Grok billing did not include a usage percentage.");

        var period = Get(config, "currentPeriod") ?? Get(config, "current_period");
        var start = ParseDate(Str(period, "start") ?? Str(config, "billingPeriodStart") ?? Str(config, "billing_period_start"));
        var end = ParseDate(Str(period, "end") ?? Str(config, "billingPeriodEnd") ?? Str(config, "billing_period_end"));
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
        var products = Get(config, "productUsage") ?? Get(config, "product_usage");
        if (products is { ValueKind: JsonValueKind.Array })
        {
            foreach (var product in products.Value.EnumerateArray().Take(31))
            {
                var usage = product.TryGetProperty("usagePercent", out var up) ? up.GetDouble()
                    : product.TryGetProperty("usage_percent", out var up2) ? up2.GetDouble() : 0;
                var name = product.TryGetProperty("product", out var p) ? p.GetString() ?? "share" : "share";
                windows.Add(new($"product-{name.ToLowerInvariant()}", ProductLabel(name), usage, minutes, end));
            }
        }

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
        var unified = Get(config, "isUnifiedBillingUser") ?? Get(config, "is_unified_billing_user");
        if (unified is { ValueKind: JsonValueKind.True })
        {
            var period = Get(config, "currentPeriod") ?? Get(config, "current_period");
            var start = ParseDate(Str(period, "start"));
            var end = ParseDate(Str(period, "end"));
            if (start is { } s && end is { } e && e > s) return 0;
        }
        return null;
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
}
