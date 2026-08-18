using GrokReserve.Core;

namespace GrokReserve.Ui;

public sealed class DashboardForm : Form
{
    const int BodyWidth = 320;

    readonly Store _store;
    readonly PictureBox _logo = new();
    readonly Label _wordmark = MakeLabel(11f, FontStyle.Bold);
    readonly Label _fresh = MakeLabel(8.25f);
    readonly RefreshButton _refresh = new();
    readonly Label _headline = MakeLabel(32f, FontStyle.Bold);
    readonly Label _sub = MakeLabel(9f);
    readonly Label _limit = MakeLabel(8.5f);
    readonly Label _error = MakeLabel(8.5f);
    readonly MeterBar _meter = new();
    readonly Button _signIn = GhostButton("Sign in");
    readonly Button _settings = GhostButton("Settings");
    readonly Button _quit = GhostButton("Quit");
    readonly FlowLayoutPanel _shares = new();
    readonly Panel _footerLine = new();

    public long OpenedAtMs;

    public DashboardForm(Store store)
    {
        _store = store;
        Text = "Grok Reserve";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9.5f);
        Padding = new Padding(22, 16, 20, 14);
        MinimumSize = new Size(364, 0);
        DoubleBuffered = true;
        if (AppBrand.WindowIcon is { } icon) Icon = icon;

        _logo.Size = new Size(18, 18);
        _logo.SizeMode = PictureBoxSizeMode.Zoom;
        _logo.Margin = new Padding(0, 1, 8, 0);
        _wordmark.Text = "Grok Reserve";
        _wordmark.Anchor = AnchorStyles.Left;
        _fresh.TextAlign = ContentAlignment.MiddleRight;
        _fresh.Anchor = AnchorStyles.Right;
        _refresh.Click += async (_, _) => await _store.RefreshAsync();

        var headerRight = Row();
        headerRight.AutoSize = true;
        headerRight.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        headerRight.Padding = new Padding(0);
        headerRight.Margin = new Padding(8, 0, 0, 0);
        headerRight.WrapContents = false;
        headerRight.Controls.Add(_fresh);
        headerRight.Controls.Add(_refresh);
        headerRight.Anchor = AnchorStyles.Right;

        _headline.MaximumSize = new Size(BodyWidth, 0);
        _headline.Margin = new Padding(0, 14, 0, 0);
        _sub.MaximumSize = new Size(BodyWidth, 0);
        _sub.Margin = new Padding(0, 4, 0, 16);
        _error.MaximumSize = new Size(BodyWidth, 0);
        _error.Margin = new Padding(0, 0, 0, 10);
        _limit.MaximumSize = new Size(BodyWidth, 0);
        _limit.Margin = new Padding(0, 0, 0, 8);
        _meter.Size = new Size(BodyWidth, 4);
        _meter.Margin = new Padding(0, 0, 0, 16);
        _signIn.Click += async (_, _) => await _store.ConnectAsync();
        _signIn.Padding = new Padding(10, 4, 10, 4);
        _signIn.Margin = new Padding(0, 0, 0, 12);

        _shares.AutoSize = true;
        _shares.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _shares.FlowDirection = FlowDirection.TopDown;
        _shares.WrapContents = false;
        _shares.Margin = new Padding(0, 0, 0, 14);
        _shares.MinimumSize = new Size(BodyWidth, 0);

        _footerLine.Height = 1;
        _footerLine.Width = BodyWidth;
        _footerLine.Margin = new Padding(0, 2, 0, 8);
        _quit.Anchor = AnchorStyles.Right;

        var title = Row();
        title.Controls.Add(_logo);
        title.Controls.Add(_wordmark);
        var root = Stack();
        root.MinimumSize = new Size(BodyWidth, 0);
        root.Controls.Add(Split(title, headerRight, 0, 0));
        root.Controls.Add(_headline);
        root.Controls.Add(_sub);
        root.Controls.Add(_error);
        root.Controls.Add(_signIn);
        root.Controls.Add(_limit);
        root.Controls.Add(_meter);
        root.Controls.Add(_shares);
        root.Controls.Add(_footerLine);
        root.Controls.Add(Split(_settings, _quit, 0, 0));
        Controls.Add(root);

        _settings.Click += (_, _) => OpenSettings?.Invoke();
        _quit.Click += (_, _) => Application.Exit();

        Theme.Changed += OnThemeChanged;
        HandleCreated += (_, _) => NativeChrome.ApplyPopover(this);
        Deactivate += (_, _) =>
        {
            if (Environment.TickCount64 - OpenedAtMs < 250) return;
            BeginInvoke(Hide);
        };
        Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Current.Hairline);
            var box = ClientRectangle;
            box.Width -= 1;
            box.Height -= 1;
            e.Graphics.DrawRectangle(pen, box);
        };
        ApplyTheme();
        Render();
    }

    public event Action? OpenSettings;

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
        Render();
        Invalidate();
    }

    public void Render()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(Render); return; }

        var theme = Theme.Current;
        var now = DateTimeOffset.Now;
        var window = _store.PrimaryWindow;
        var pace = _store.Pace;
        var color = Format.PaceColor(pace.Kind);
        var remaining = window?.RemainingPercent;
        var needsSignIn = _store.State.RequiresConnection && !_store.State.IsConnecting;

        _headline.Text = remaining is { } left ? $"{Math.Round(left)}% left" : "Not connected";
        _headline.ForeColor = window is null ? theme.Text : color;
        _sub.Text = window is null
            ? "Sign in with the Grok CLI to read your allowance"
            : Format.Forecast(window, pace, now);
        _fresh.Text = _store.State.IsRefreshing ? "Updating"
            : _store.State.Snapshot is { } snap ? Updated(snap.FetchedAt, now) : "Waiting";

        _error.Visible = window is null && !string.IsNullOrEmpty(_store.State.Error);
        _error.Text = _store.State.Error ?? "";
        _signIn.Visible = needsSignIn;
        _limit.Visible = window is not null;
        _meter.Visible = window is not null;

        if (window is not null)
        {
            _limit.Text = Format.LimitLine(Format.WindowTitle(window.Label), window.ResetsAt, now);
            _meter.Fill = color;
            _meter.Percent = window.RemainingPercent;
            _meter.Invalidate();
        }

        BindShares(window is null ? [] : _store.State.Snapshot?.Windows.Where(w => w.IsComponentShare) ?? []);
        ApplyTheme();
        PerformLayout();
    }

    void BindShares(IEnumerable<UsageWindow> shares)
    {
        var theme = Theme.Current;
        var items = shares
            .OrderBy(w => ShareOrder(Format.ShareName(w.Label)))
            .ToList();
        _shares.Visible = items.Count > 0;
        while (_shares.Controls.Count > items.Count)
        {
            var last = _shares.Controls[^1];
            _shares.Controls.RemoveAt(_shares.Controls.Count - 1);
            last.Dispose();
        }
        for (var i = 0; i < items.Count; i++)
        {
            if (_shares.Controls.Count == i)
                _shares.Controls.Add(new ShareRow());
            ((ShareRow)_shares.Controls[i]).Bind(items[i], theme, i < items.Count - 1);
        }
    }

    void ApplyTheme()
    {
        var theme = Theme.Current;
        BackColor = theme.Window;
        ForeColor = theme.Text;
        _wordmark.ForeColor = theme.Text;
        var previous = _logo.Image;
        _logo.Image = AppBrand.Mark(theme.Text, 18);
        _logo.BackColor = theme.Window;
        previous?.Dispose();
        _fresh.ForeColor = theme.Subtle;
        _sub.ForeColor = theme.Muted;
        _limit.ForeColor = theme.Muted;
        _error.ForeColor = theme.AccentText;
        _footerLine.BackColor = theme.Hairline;
        _meter.Track = theme.Track;
        PaintGhost(_refresh, theme);
        PaintGhost(_settings, theme);
        PaintGhost(_quit, theme);
        _signIn.ForeColor = theme.AccentText;
        _signIn.BackColor = theme.AccentSoft;
        _signIn.FlatAppearance.MouseOverBackColor = theme.Hover;
        _signIn.FlatAppearance.MouseDownBackColor = theme.Card;
        if (IsHandleCreated) NativeChrome.ApplyPopover(this);
    }

    static void PaintGhost(Button button, Palette theme)
    {
        button.ForeColor = theme.Muted;
        button.BackColor = theme.Window;
        button.FlatAppearance.MouseOverBackColor = theme.Hover;
        button.FlatAppearance.MouseDownBackColor = theme.Card;
    }

    sealed class RefreshButton : Button
    {
        public RefreshButton()
        {
            Text = string.Empty;
            Size = new Size(22, 22);
            MinimumSize = new Size(22, 22);
            Margin = new Padding(8, 0, 2, 0);
            Padding = new Padding(0);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            TabStop = false;
            AccessibleName = "Refresh";
            MouseEnter += (_, _) => Invalidate();
            MouseLeave += (_, _) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var hover = ClientRectangle.Contains(PointToClient(Cursor.Position));
            var back = hover ? FlatAppearance.MouseOverBackColor : BackColor;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            e.Graphics.Clear(back);

            var bounds = new RectangleF(4.2f, 4.2f, Width - 9.2f, Height - 9.2f);
            using var pen = new Pen(ForeColor, 1.5f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
            };
            e.Graphics.DrawArc(pen, bounds, -20, 290);

            var cx = bounds.X + bounds.Width / 2f;
            var cy = bounds.Y + bounds.Height / 2f;
            var rx = bounds.Width / 2f;
            var ry = bounds.Height / 2f;
            const float tipAngle = -20f * (float)Math.PI / 180f;
            var tip = new PointF(cx + rx * (float)Math.Cos(tipAngle), cy + ry * (float)Math.Sin(tipAngle));
            using var fill = new SolidBrush(ForeColor);
            var arrow = new[]
            {
                tip,
                new PointF(tip.X - 4.2f, tip.Y - 0.4f),
                new PointF(tip.X - 0.6f, tip.Y + 4.4f),
            };
            e.Graphics.FillPolygon(fill, arrow);
        }
    }

    static FlowLayoutPanel Stack() => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        Margin = new Padding(0),
        Padding = new Padding(0),
    };

    static FlowLayoutPanel Row() => new()
    {
        AutoSize = true,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        Margin = new Padding(0),
        Padding = new Padding(0),
    };

    static TableLayoutPanel Split(Control left, Control right, int top, int bottom)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, top, 0, bottom),
            MinimumSize = new Size(BodyWidth, 0),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(left, 0, 0);
        row.Controls.Add(right, 1, 0);
        return row;
    }

    static Label MakeLabel(float size, FontStyle style = FontStyle.Regular) => new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI", size, style),
        Margin = new Padding(0),
    };

    static Button GhostButton(string text)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            ForeColor = Color.Gray,
            Cursor = Cursors.Hand,
            Margin = new Padding(0),
            Padding = new Padding(2, 2, 2, 2),
            TabStop = false,
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    static int ShareOrder(string name)
    {
        var key = name.ToLowerInvariant();
        if (key.Contains("build")) return 0;
        if (key.Contains("imagine")) return 1;
        if (key.Contains("chat")) return 2;
        if (key.Contains("voice")) return 3;
        return 8;
    }

    static string Updated(DateTimeOffset at, DateTimeOffset now)
    {
        var seconds = Math.Max(0, (now - at).TotalSeconds);
        if (seconds < 60) return "Just now";
        if (seconds < 3600) return $"{(int)(seconds / 60)} min ago";
        return $"{(int)(seconds / 3600)}h ago";
    }

    sealed class ShareRow : TableLayoutPanel
    {
        readonly Label _name = MakeLabel(9.5f);
        readonly Label _value = MakeLabel(9.5f, FontStyle.Bold);
        readonly Panel _rule = new() { Height = 1, Margin = new Padding(0, 8, 0, 8) };

        public ShareRow()
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ColumnCount = 2;
            RowCount = 2;
            Margin = new Padding(0);
            MinimumSize = new Size(BodyWidth, 0);
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _name.Anchor = AnchorStyles.Left;
            _value.Anchor = AnchorStyles.Right;
            _value.TextAlign = ContentAlignment.MiddleRight;
            _rule.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_name, 0, 0);
            Controls.Add(_value, 1, 0);
            SetColumnSpan(_rule, 2);
            Controls.Add(_rule, 0, 1);
        }

        public void Bind(UsageWindow window, Palette theme, bool showRule)
        {
            _name.Text = Format.ShareName(window.Label);
            _name.ForeColor = theme.Text;
            _value.Text = window.UsedPercent <= 0.5 ? "Ready" : $"{Math.Round(window.RemainingPercent)}%";
            _value.ForeColor = window.UsedPercent <= 0.5 ? theme.Subtle : theme.Text;
            _rule.BackColor = theme.Hairline;
            _rule.Visible = showRule;
        }
    }

    sealed class MeterBar : Panel
    {
        public double Percent { get; set; }
        public Color Fill { get; set; } = Color.FromArgb(0x32, 0xD7, 0x4B);
        public Color Track { get; set; } = Color.FromArgb(0xE3, 0xE1, 0xDA);

        public MeterBar()
        {
            Height = 4;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var bounds = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var track = new SolidBrush(Track))
                FillRound(e.Graphics, track, bounds, Height / 2f);
            var width = (float)(Width * Math.Clamp(Percent, 0, 100) / 100.0);
            if (width < 2) return;
            using var fill = new SolidBrush(Fill);
            FillRound(e.Graphics, fill, new RectangleF(0, 0, width, Height), Height / 2f);
        }

        static void FillRound(Graphics g, Brush brush, RectangleF rect, float radius)
        {
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            var d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            g.FillPath(brush, path);
        }
    }
}
