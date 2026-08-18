namespace GrokReserve.Core;

public static class Locator
{
    public static string? FindGrok()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dirs = new[]
        {
            Path.Combine(home, ".local", "bin"),
            Path.Combine(home, ".grok", "bin"),
            Path.Combine(local, "Programs"),
            Path.Combine(roaming, "npm"),
            Path.Combine(local, "npm"),
            Path.Combine(home, "AppData", "Roaming", "npm"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs"),
        };
        var names = new[] { "grok.exe", "grok.cmd", "grok" };
        foreach (var dir in dirs.Concat(PathDirs()))
        {
            foreach (var name in names)
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    static IEnumerable<string> PathDirs()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
    }
}
