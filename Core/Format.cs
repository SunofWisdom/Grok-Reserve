namespace GrokReserve.Core;

public static class Format
{
    public static string Gap(DateTimeOffset from, DateTimeOffset to)
    {
        var minutes = Math.Max(0, (int)(to - from).TotalMinutes);
        var days = minutes / 1440;
        var hours = minutes % 1440 / 60;
        if (days > 0) return $"{days}d {hours}h";
        if (hours > 0) return $"{hours}h {minutes % 60}m";
        return $"{minutes}m";
    }

    public static string Countdown(DateTimeOffset to, DateTimeOffset now) => $"in {Gap(now, to)}";
    public static string ShortCountdown(DateTimeOffset to, DateTimeOffset now) => Gap(now, to);

    public static string Moment(DateTimeOffset date, DateTimeOffset now)
    {
        var local = date.ToLocalTime();
        var time = local.ToString("t");
        var interval = date - now;
        if (interval.TotalHours < 12) return time;
        if (interval.TotalDays < 6) return $"{local:dddd}, {time}";
        return $"{local:MMM} {local.Day}, {time}";
    }

    public static string LimitLine(string title, DateTimeOffset? resetsAt, DateTimeOffset now)
    {
        if (resetsAt is not { } reset || reset <= now) return $"{title}\nNext reset unknown";
        return $"{title}\nResets {Moment(reset, now)}";
    }

    public static string Forecast(UsageWindow window, PaceState pace, DateTimeOffset now)
    {
        var projection = PaceProjection.Calculate(window, now);
        var projected = projection?.ProjectedRemainingPercent is { } left
            ? $"{Math.Round(left)}% projected left at reset"
            : null;
        return pace.Kind switch
        {
            "exhausted" when window.ResetsAt is { } reset && reset > now =>
                $"Limit exhausted · resets {Countdown(reset, now)}",
            "exhausted" => "Limit exhausted",
            "stale" => "Forecast unavailable while live data is unavailable",
            "unknown" => "Forecast unavailable",
            "reserve" => projected is null
                ? $"{Math.Round(pace.Percent)}% in reserve"
                : $"{Math.Round(pace.Percent)}% in reserve · {projected}",
            "onPace" => projected is null ? "On pace" : $"On pace · {projected}",
            "deficit" when projection?.ProjectedExhaustionAt is { } runsOut
                && window.ResetsAt is { } renewal && runsOut < renewal =>
                $"{Math.Round(pace.Percent)}% deficit · runs out {Gap(runsOut, renewal)} early",
            "deficit" => projected is null
                ? $"{Math.Round(pace.Percent)}% deficit"
                : $"{Math.Round(pace.Percent)}% deficit · {projected}",
            _ => "Forecast unavailable",
        };
    }

    public static string PaceLabel(string kind) => kind switch
    {
        "reserve" => "Reserve",
        "onPace" => "On pace",
        "deficit" => "Deficit",
        "exhausted" => "Exhausted",
        "stale" => "Stale",
        _ => "Unknown",
    };

    public static Color PaceColor(string kind) => kind switch
    {
        "reserve" or "onPace" => Color.FromArgb(0x32, 0xD7, 0x4B),
        "deficit" or "exhausted" => Color.FromArgb(0xFF, 0x9F, 0x0A),
        _ => Color.FromArgb(0x83, 0x94, 0x89),
    };

    public static string ShareName(string label)
    {
        var text = label.Trim();
        if (text.StartsWith("Grok ", StringComparison.OrdinalIgnoreCase))
            text = text[5..];
        if (text.EndsWith(" share", StringComparison.OrdinalIgnoreCase))
            text = text[..^6];
        return string.IsNullOrWhiteSpace(text) ? label : text;
    }

    public static string WindowTitle(string label)
    {
        if (label.Equals("Weekly", StringComparison.OrdinalIgnoreCase)) return "Weekly limit";
        if (label.Equals("Monthly", StringComparison.OrdinalIgnoreCase)) return "Monthly limit";
        return label;
    }
}
