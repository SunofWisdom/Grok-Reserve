using System.Text.Json;

namespace GrokReserve.Core;

public sealed class Prefs
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    readonly Dictionary<string, JsonElement> _values;
    readonly string _path;

    public static string SupportDir
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(root, "GrokReserve");
        }
    }

    Prefs(string path, Dictionary<string, JsonElement> values)
    {
        _path = path;
        _values = values;
    }

    public static Prefs Load()
    {
        Directory.CreateDirectory(SupportDir);
        var path = Path.Combine(SupportDir, "prefs.json");
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (File.Exists(path))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                foreach (var prop in doc.RootElement.EnumerateObject())
                    values[prop.Name] = prop.Value.Clone();
            }
            catch { /* start clean */ }
        }
        return new Prefs(path, values);
    }

    public int RefreshIntervalMinutes
    {
        get => GetInt("refresh.intervalMinutes", 30);
        set { SetNumber("refresh.intervalMinutes", Math.Max(1, value)); Save(); }
    }

    public bool StartWithWindows
    {
        get => GetBool("launch.atLogin", false);
        set { SetBool("launch.atLogin", value); Save(); }
    }

    int GetInt(string key, int fallback) =>
        _values.TryGetValue(key, out var el) && el.TryGetInt32(out var n) ? n : fallback;

    bool GetBool(string key, bool fallback) =>
        _values.TryGetValue(key, out var el) && el.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? el.GetBoolean()
            : fallback;

    void SetBool(string key, bool value) =>
        _values[key] = JsonSerializer.SerializeToElement(value);

    void SetNumber(string key, int value) =>
        _values[key] = JsonSerializer.SerializeToElement(value);

    void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var obj = new Dictionary<string, object?>();
        foreach (var (key, el) in _values)
            obj[key] = el.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => el.TryGetInt32(out var n) ? n : el.GetDouble(),
                JsonValueKind.String => el.GetString(),
                _ => el.GetRawText(),
            };
        File.WriteAllText(_path, JsonSerializer.Serialize(obj, JsonOptions));
    }
}
