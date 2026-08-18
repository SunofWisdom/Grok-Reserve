using GrokReserve.Core;

namespace GrokReserve.Ui;

public sealed class SettingsForm : Form
{
    static readonly int[] Intervals = [1, 5, 10, 15, 30];

    readonly Store _store;
    readonly Label _title = new();
    readonly Label _lede = new();
    readonly Label _intervalLabel = new();
    readonly ComboBox _interval = new();
    readonly CheckBox _startup = new();
    readonly Label _about = new();

    public SettingsForm(Store store)
    {
        _store = store;
        Text = "Grok Reserve";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9.5f);
        Padding = new Padding(28, 24, 28, 28);
        if (AppBrand.WindowIcon is { } icon) Icon = icon;

        _title.Text = "Settings";
        _title.Font = new Font("Segoe UI", 18, FontStyle.Bold);
        _title.AutoSize = true;
        _title.Margin = new Padding(0, 0, 0, 6);

        _lede.Text = "How often to check your allowance, and whether Grok Reserve starts with Windows.";
        _lede.AutoSize = true;
        _lede.MaximumSize = new Size(384, 0);
        _lede.Margin = new Padding(0, 0, 0, 22);

        _intervalLabel.Text = "Refresh interval";
        _intervalLabel.AutoSize = true;
        _intervalLabel.Anchor = AnchorStyles.Left;
        _interval.DropDownStyle = ComboBoxStyle.DropDownList;
        _interval.FlatStyle = FlatStyle.Flat;
        _interval.Width = 150;
        _interval.Anchor = AnchorStyles.Right;
        foreach (var minutes in Intervals)
            _interval.Items.Add($"Every {minutes} min");
        var selected = Array.IndexOf(Intervals, _store.Prefs.RefreshIntervalMinutes);
        _interval.SelectedIndex = selected >= 0 ? selected : 4;
        _interval.SelectedIndexChanged += (_, _) =>
        {
            _store.Prefs.RefreshIntervalMinutes = Intervals[_interval.SelectedIndex];
            _store.RestartTimer();
        };

        var intervalRow = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 16),
        };
        intervalRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        intervalRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        intervalRow.Controls.Add(_intervalLabel, 0, 0);
        intervalRow.Controls.Add(_interval, 1, 0);

        _startup.Text = "Start with Windows";
        _startup.Checked = Startup.IsEnabled();
        _startup.AutoSize = true;
        _startup.Margin = new Padding(0, 0, 0, 28);
        _startup.CheckedChanged += (_, _) =>
        {
            Startup.SetEnabled(_startup.Checked);
            _store.Prefs.StartWithWindows = _startup.Checked;
        };

        _about.Text = "Grok Reserve 1.0.0\nSign-in stays in the official Grok CLI.\nMIT licensed. Independent — not affiliated with xAI.";
        _about.AutoSize = true;
        _about.MaximumSize = new Size(384, 0);
        _about.Margin = new Padding(0);

        var root = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
        root.Controls.AddRange(new Control[] { _title, _lede, intervalRow, _startup, _about });
        Controls.Add(root);

        Theme.Changed += OnThemeChanged;
        HandleCreated += (_, _) => NativeChrome.ApplyWindow(this);
        ApplyTheme();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Theme.Changed -= OnThemeChanged;
        base.Dispose(disposing);
    }

    void OnThemeChanged()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(OnThemeChanged); return; }
        ApplyTheme();
    }

    void ApplyTheme()
    {
        var theme = Theme.Current;
        BackColor = theme.Window;
        ForeColor = theme.Text;
        _title.ForeColor = theme.Text;
        _lede.ForeColor = theme.Muted;
        _intervalLabel.ForeColor = theme.Text;
        _startup.ForeColor = theme.Text;
        _startup.BackColor = theme.Window;
        _about.ForeColor = theme.Subtle;
        _interval.BackColor = theme.Card;
        _interval.ForeColor = theme.Text;
        if (IsHandleCreated) NativeChrome.ApplyWindow(this);
    }
}
