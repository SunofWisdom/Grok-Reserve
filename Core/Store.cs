namespace GrokReserve.Core;

public sealed class Store
{
    public Prefs Prefs { get; }
    public ProviderState State { get; } = new();
    public event Action? Changed;
    readonly bool _demo;
    System.Threading.Timer? _timer;

    public Store(Prefs prefs, bool demo = false)
    {
        Prefs = prefs;
        _demo = demo;
    }

    public void Start()
    {
        RestartTimer();
        _ = RefreshAsync();
    }

    public void RestartTimer()
    {
        _timer?.Dispose();
        var ms = Math.Max(1, Prefs.RefreshIntervalMinutes) * 60_000;
        _timer = new System.Threading.Timer(_ => _ = RefreshAsync(), null, ms, ms);
    }

    public async Task RefreshAsync()
    {
        if (State.IsRefreshing) return;
        State.IsRefreshing = true;
        Raise();
        try
        {
            if (_demo)
            {
                InstallDemo();
            }
            else
            {
                State.Snapshot = await GrokProvider.FetchAsync();
                State.Error = null;
                State.RequiresConnection = false;
            }
        }
        catch (ProviderException ex)
        {
            State.Error = ex.Message;
            State.RequiresConnection = ex.RequiresConnection;
        }
        catch (Exception ex)
        {
            State.Error = ex.Message;
        }
        finally
        {
            State.IsRefreshing = false;
            Raise();
        }
    }

    public async Task ConnectAsync()
    {
        var exe = Locator.FindGrok();
        if (exe is null)
        {
            State.Error = "Grok Build CLI is not installed.";
            State.RequiresConnection = true;
            Raise();
            return;
        }
        State.IsConnecting = true;
        State.Error = null;
        Raise();
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo(exe, ["login", "--oauth"])
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                },
            };
            process.Start();
            _ = Task.Run(() => process.StandardInput.WriteLine());
            var output = await process.StandardOutput.ReadToEndAsync();
            var url = FindLoginUrl(output);
            if (url is not null)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(5));
            State.IsConnecting = false;
            if (process.ExitCode == 0)
            {
                State.Error = null;
                await RefreshAsync();
            }
            else
            {
                State.RequiresConnection = true;
                State.Error ??= "Grok sign-in was not completed. Use Sign in to retry.";
                Raise();
            }
        }
        catch (Exception ex)
        {
            State.IsConnecting = false;
            State.RequiresConnection = true;
            State.Error = ex.Message;
            Raise();
        }
    }

    public UsageWindow? PrimaryWindow
    {
        get
        {
            var windows = State.Snapshot?.Windows ?? [];
            var plan = windows.Where(w => !w.IsComponentShare).ToList();
            return plan.FirstOrDefault(w => w.Label.Equals("Weekly", StringComparison.OrdinalIgnoreCase))
                ?? plan.MaxBy(w => w.UsedPercent)
                ?? windows.MaxBy(w => w.UsedPercent);
        }
    }

    public PaceState Pace
    {
        get
        {
            var window = PrimaryWindow;
            return PaceState.Calculate(window, State.Snapshot?.FetchedAt, State.Error is not null, DateTimeOffset.Now);
        }
    }

    static string? FindLoginUrl(string output)
    {
        foreach (System.Text.RegularExpressions.Match match in
                 System.Text.RegularExpressions.Regex.Matches(output, @"https://[^\s\u001B<>""]+"))
        {
            var text = match.Value.TrimEnd('\'', '(', ')', ',', '.', ';');
            if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
                && uri.Scheme == "https"
                && uri.Host.ToLowerInvariant() is "auth.x.ai" or "x.ai" or "grok.com")
                return uri.ToString();
        }
        return null;
    }

    void InstallDemo()
    {
        var now = DateTimeOffset.Now;
        State.Snapshot = new UsageSnapshot
        {
            PlanName = "SuperGrok Heavy",
            FetchedAt = now.AddSeconds(-30),
            Windows =
            [
                new("usage-pool", "Weekly", 26, 10080, now.AddDays(2.8)),
                new("product-grokbuild", "Grok Build share", 38, 10080, now.AddDays(2.8)),
                new("product-grokimagine", "Grok Imagine share", 9, 10080, now.AddDays(2.8)),
                new("product-grokchat", "Grok Chat share", 5, 10080, now.AddDays(2.8)),
                new("product-grokbot", "Grok Bot share", 12, 10080, now.AddDays(2.8)),
            ],
        };
        State.Error = null;
        State.RequiresConnection = false;
    }

    void Raise() => Changed?.Invoke();

    public void Dispose() => _timer?.Dispose();
}
