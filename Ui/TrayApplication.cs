using GrokReserve.Core;

namespace GrokReserve.Ui;

public sealed class TrayApplication : ApplicationContext
{
    readonly Store _store;
    readonly NotifyIcon _tray;
    DashboardForm? _dashboard;
    SettingsForm? _settings;
    Icon? _currentIcon;

    public TrayApplication(Store store)
    {
        _store = store;
        _tray = new NotifyIcon
        {
            Visible = true,
            Text = "Grok Reserve",
            ContextMenuStrip = BuildMenu(),
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ToggleDashboard(Cursor.Position);
        };
        _store.Changed += OnChanged;
        Theme.Changed += OnThemeChanged;
        ApplyTray();
        _tray.ShowBalloonTip(4000, "Grok Reserve", "It is in the notification area. Click the Grok mark. If you do not see it, open the arrow beside the clock.", ToolTipIcon.None);
    }

    void OnThemeChanged()
    {
        if (_tray.ContextMenuStrip?.InvokeRequired == true)
        {
            _tray.ContextMenuStrip.BeginInvoke(OnThemeChanged);
            return;
        }
        _dashboard?.Render();
    }

    void OnChanged()
    {
        if (_tray.ContextMenuStrip?.InvokeRequired == true)
        {
            _tray.ContextMenuStrip.BeginInvoke(OnChanged);
            return;
        }
        ApplyTray();
        _dashboard?.Render();
    }

    void ApplyTray()
    {
        var window = _store.PrimaryWindow;
        var pace = _store.Pace;
        var remaining = window?.RemainingPercent;
        var now = DateTimeOffset.Now;
        var plan = string.Join(" ", new[] { "Grok", _store.State.Snapshot?.PlanName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var capacity = remaining is { } left ? $"{Math.Round(left)}% left" : "capacity unavailable";
        var reset = window?.ResetsAt is { } at && at > now ? Format.ShortCountdown(at, now) : null;
        var bot = _store.State.Snapshot?.Windows.FirstOrDefault(w => w.Id == "bot-weekly");
        var lines = new List<string>
        {
            string.IsNullOrWhiteSpace(plan) ? "Grok" : plan,
            $"{capacity} · {Format.PaceLabel(pace.Kind)}",
            reset is null ? "Reset time unavailable" : $"Resets in {reset}",
        };
        if (bot is not null)
            lines.Add($"Bot {Math.Round(bot.RemainingPercent)}% left");
        _tray.Text = TrimTip(string.Join("\r\n", lines));
        var next = GaugeIcon.Create(remaining, pace.Kind);
        var old = _currentIcon;
        _tray.Icon = next;
        _currentIcon = next;
        old?.Dispose();
    }

    static string TrimTip(string text) => text.Length <= 127 ? text : text[..127];

    void ToggleDashboard(Point? anchor = null)
    {
        if (_dashboard is { Visible: true })
        {
            _dashboard.Hide();
            return;
        }
        _dashboard ??= CreateDashboard();
        if (!_dashboard.IsHandleCreated)
            _ = _dashboard.Handle;
        _dashboard.Render();
        var cursor = anchor ?? Cursor.Position;
        var icon = TrayIconBounds.From(_tray, cursor);
        _dashboard.PerformLayout();
        PositionAboveIcon(_dashboard, icon);
        _dashboard.OpenedAtMs = Environment.TickCount64;
        _dashboard.Show();
        _dashboard.Activate();
    }

    DashboardForm CreateDashboard()
    {
        var form = new DashboardForm(_store);
        form.OpenSettings += ShowSettings;
        return form;
    }

    void ShowSettings()
    {
        if (_settings is null || _settings.IsDisposed)
            _settings = new SettingsForm(_store);
        _settings.Show();
        _settings.BringToFront();
        _settings.Activate();
    }

    ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Grok Reserve", null, (_, _) => ToggleDashboard(Cursor.Position));
        menu.Items.Add("Refresh", null, async (_, _) => await _store.RefreshAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit Grok Reserve", null, (_, _) => ExitThread());
        return menu;
    }

    static void PositionAboveIcon(Form form, Rectangle icon)
    {
        var area = Screen.FromPoint(new Point(icon.X + icon.Width / 2, icon.Y)).WorkingArea;
        var width = Math.Max(form.Width, form.PreferredSize.Width);
        var height = Math.Max(form.Height, form.PreferredSize.Height);
        var x = icon.X + icon.Width / 2 - width / 2;
        var y = icon.Y - height - 10;
        if (y < area.Top)
            y = icon.Bottom + 10;
        x = Math.Min(Math.Max(area.Left + 8, x), area.Right - width - 8);
        y = Math.Min(Math.Max(area.Top + 8, y), area.Bottom - height - 8);
        form.Location = new Point(x, y);
    }

    protected override void ExitThreadCore()
    {
        Theme.Changed -= OnThemeChanged;
        Theme.Stop();
        _store.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _currentIcon?.Dispose();
        _dashboard?.Dispose();
        _settings?.Dispose();
        base.ExitThreadCore();
    }
}
