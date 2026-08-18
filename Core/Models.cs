namespace GrokReserve.Core;

public sealed class ProviderException : Exception
{
    public string Code { get; }
    public bool RequiresConnection =>
        Code is "credentialsNotFound" or "unauthorized" or "executableNotFound";

    public ProviderException(string code, string message) : base(message) => Code = code;
}

public sealed class UsageWindow
{
    public const int MaxWindowMinutes = 366 * 24 * 60;
    public string Id { get; }
    public string Label { get; }
    public double UsedPercent { get; }
    public int? WindowMinutes { get; }
    public DateTimeOffset? ResetsAt { get; }
    public bool IsComponentShare =>
        Id.StartsWith("product-", StringComparison.OrdinalIgnoreCase)
        || Label.Contains("share", StringComparison.OrdinalIgnoreCase);
    public double RemainingPercent => Math.Clamp(100 - UsedPercent, 0, 100);

    public UsageWindow(string id, string label, double usedPercent, int? windowMinutes, DateTimeOffset? resetsAt)
    {
        Id = id.Length > 512 ? id[..512] : id;
        Label = label.Length > 96 ? label[..96] : label;
        UsedPercent = double.IsFinite(usedPercent) ? Math.Clamp(usedPercent, 0, 100) : 0;
        WindowMinutes = windowMinutes is >= 1 and <= MaxWindowMinutes ? windowMinutes : null;
        if (resetsAt is { } reset && Math.Abs((reset - DateTimeOffset.Now).TotalDays) <= 3660)
            ResetsAt = reset;
    }
}

public sealed class PaceProjection
{
    public const double OnPaceTolerancePercent = 2;
    public string Position { get; init; } = "onPace";
    public double VariancePercent { get; init; }
    public DateTimeOffset? ProjectedExhaustionAt { get; init; }
    public double? ProjectedRemainingPercent { get; init; }

    public static PaceProjection? Calculate(UsageWindow window, DateTimeOffset now)
    {
        if (window.WindowMinutes is not > 0 || window.ResetsAt is not { } reset || reset <= now)
            return null;
        var duration = TimeSpan.FromMinutes(window.WindowMinutes.Value);
        var start = reset - duration;
        var elapsed = now - start;
        if (elapsed <= TimeSpan.Zero) return null;
        var elapsedFraction = Math.Min(1, elapsed / duration);
        if (elapsedFraction < 0.01) return null;

        var usedFraction = Math.Clamp(window.UsedPercent / 100, 0, 1);
        var signed = (elapsedFraction - usedFraction) * 100;
        var position = signed > OnPaceTolerancePercent ? "reserve"
            : signed < -OnPaceTolerancePercent ? "deficit"
            : "onPace";

        var projectedUsage = usedFraction == 0 ? 0 : usedFraction / elapsedFraction;
        var projectedRemaining = Math.Clamp((1 - projectedUsage) * 100, 0, 100);
        DateTimeOffset? exhaustion = null;
        if (usedFraction > 0)
        {
            var at = start + TimeSpan.FromTicks((long)(elapsed.Ticks / usedFraction));
            if (at < reset) exhaustion = at > now ? at : now;
        }

        return new PaceProjection
        {
            Position = position,
            VariancePercent = Math.Abs(signed),
            ProjectedExhaustionAt = exhaustion,
            ProjectedRemainingPercent = projectedRemaining,
        };
    }
}

public sealed class PaceState
{
    public static readonly TimeSpan StalenessLimit = TimeSpan.FromMinutes(30);
    public string Kind { get; }
    public double Percent { get; }
    public PaceState(string kind, double percent = 0) { Kind = kind; Percent = percent; }

    public static PaceState Calculate(UsageWindow? window, DateTimeOffset? fetchedAt, bool hasError, DateTimeOffset now)
    {
        if (window is null) return new PaceState("unknown");
        if (hasError || fetchedAt is { } at && now - at > StalenessLimit) return new PaceState("stale");
        if (window.UsedPercent >= 99.5) return new PaceState("exhausted");
        var projection = PaceProjection.Calculate(window, now);
        if (projection is null) return new PaceState("unknown");
        return projection.Position switch
        {
            "reserve" => new PaceState("reserve", projection.VariancePercent),
            "deficit" => new PaceState("deficit", projection.VariancePercent),
            _ => new PaceState("onPace"),
        };
    }
}

public sealed class UsageSnapshot
{
    public string PlanName { get; init; } = "";
    public IReadOnlyList<UsageWindow> Windows { get; init; } = [];
    public DateTimeOffset FetchedAt { get; init; } = DateTimeOffset.Now;
}

public sealed class ProviderState
{
    public UsageSnapshot? Snapshot { get; set; }
    public string? Error { get; set; }
    public bool IsRefreshing { get; set; }
    public bool IsConnecting { get; set; }
    public bool RequiresConnection { get; set; }
}
