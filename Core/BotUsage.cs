using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GrokReserve.Core;

/// <summary>
/// Weekly Grok Bot allowance. It is not in the Grok CLI billing feed.
/// The Grok Bot desktop app stores a Cursor session locally; that session
/// answers GetSandUsageStatus, which is the same meter as grok.com Usage.
/// </summary>
public static class BotUsage
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    public static UsageWindow? TryRead()
    {
        try
        {
            var token = ReadToken();
            if (string.IsNullOrWhiteSpace(token)) return null;
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api2.cursor.sh/aiserver.v1.DashboardService/GetSandUsageStatus");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("Connect-Protocol-Version", "1");
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = Http.Send(request);
            if (!response.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            var root = doc.RootElement;
            if (!root.TryGetProperty("usagePercent", out var usedEl) || !usedEl.TryGetDouble(out var used))
                return null;
            var reset = root.TryGetProperty("nextResetTimestampUtc", out var resetEl)
                && DateTimeOffset.TryParse(resetEl.GetString(), out var at) ? at : (DateTimeOffset?)null;
            var start = root.TryGetProperty("currentPeriodStart", out var startEl)
                && DateTimeOffset.TryParse(startEl.GetString(), out var from) ? from : (DateTimeOffset?)null;
            int? minutes = start is { } s && reset is { } e && e > s
                ? (int)Math.Clamp((e - s).TotalMinutes, 1, UsageWindow.MaxWindowMinutes)
                : null;
            return new UsageWindow("bot-weekly", "Grok Bot", used, minutes, reset);
        }
        catch
        {
            return null;
        }
    }

    static string? ReadToken()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Grok Bot");
        var statePath = Path.Combine(root, "Local State");
        var secretsPath = Path.Combine(root, "sand-secrets.json");
        if (!File.Exists(statePath) || !File.Exists(secretsPath)) return null;

        using var state = JsonDocument.Parse(File.ReadAllText(statePath));
        var encryptedKey = Convert.FromBase64String(state.RootElement.GetProperty("os_crypt").GetProperty("encrypted_key").GetString()!);
        var key = ProtectedData.Unprotect(encryptedKey[5..], null, DataProtectionScope.CurrentUser);

        using var secrets = JsonDocument.Parse(File.ReadAllText(secretsPath));
        var accountsJson = secrets.RootElement.GetProperty("cursor-accounts").GetString();
        if (string.IsNullOrWhiteSpace(accountsJson)) return null;
        using var accounts = JsonDocument.Parse(accountsJson);
        var active = accounts.RootElement.GetProperty("active").GetString();
        if (string.IsNullOrWhiteSpace(active)) return null;
        var packed = accounts.RootElement.GetProperty("accounts").GetProperty(active).GetProperty("cursor-access-token").GetString();
        if (string.IsNullOrWhiteSpace(packed)) return null;
        var blob = Convert.FromBase64String(packed);
        if (blob.Length < 3 + 12 + 16 || Encoding.ASCII.GetString(blob, 0, 3) != "v10") return null;
        var nonce = blob[3..15];
        var cipher = blob[15..^16];
        var tag = blob[^16..];
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
