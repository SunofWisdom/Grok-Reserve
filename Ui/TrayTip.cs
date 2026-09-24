using System.Drawing.Drawing2D;

namespace GrokReserve.Ui;

readonly record struct TipContent(string Percent, Color PercentColor, string Detail, string Reset, string? Bot);

sealed class TrayTip : Form
{
    static readonly Font PercentFont = new("Segoe UI", 22f, FontStyle.Bold);
    static readonly Font DetailFont = new("Segoe UI", 9f);
    static readonly Font QuietFont = new("Segoe UI", 8.5f);
    const TextFormatFlags MeasureFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter;
    const int PadX = 18;
    const int PadY = 14;

    Rectangle _anchor;
    TipContent _content;
    bool _placed;

    public TrayTip()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        AutoSize = false;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Resize += (_, _) => ApplyShape();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000;
            cp.ExStyle |= 0x00000080;
            return cp;
        }
    }

    public void UpdateLines(TipContent content)
    {
        if (!Visible || content == _content) return;
        Apply(content, move: false);
    }

    public void Present(TipContent content, Rectangle icon)
    {
        _anchor = icon;
        _placed = false;
        Apply(content, move: true);
        if (!Visible)
            Show();
    }

    void Apply(TipContent content, bool move)
    {
        _content = content;
        var theme = Theme.Current;
        BackColor = theme.Window;
        var layout = MeasureTip(content);
        Size = layout.Size;
        ApplyShape();
        if (move || !_placed)
        {
            var area = Screen.FromRectangle(_anchor).WorkingArea;
            var x = _anchor.X + _anchor.Width / 2 - layout.Size.Width / 2;
            var y = _anchor.Y - layout.Size.Height - 8;
            if (y < area.Top)
                y = _anchor.Bottom + 8;
            x = Math.Min(Math.Max(area.Left + 4, x), area.Right - layout.Size.Width - 4);
            Location = new Point(x, y);
            _placed = true;
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var theme = Theme.Current;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var box = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var fill = new SolidBrush(theme.Window))
        using (var path = Round(box, 14))
            g.FillPath(fill, path);
        using (var pen = new Pen(theme.Hairline))
        using (var path = Round(box, 14))
            g.DrawPath(pen, path);

        var layout = MeasureTip(_content);
        Draw(g, _content.Percent, PercentFont, _content.PercentColor, layout.Percent);
        Draw(g, _content.Detail, DetailFont, theme.Muted, layout.Detail);
        Draw(g, _content.Reset, QuietFont, theme.Subtle, layout.Reset);
        if (_content.Bot is { } bot)
        {
            using var pen = new Pen(theme.Hairline);
            g.DrawLine(pen, layout.Rule.Left, layout.Rule.Top, layout.Rule.Right, layout.Rule.Top);
            Draw(g, bot, QuietFont, theme.Muted, layout.Bot);
        }
    }

    static void Draw(Graphics g, string text, Font font, Color color, Rectangle bounds)
    {
        TextRenderer.DrawText(g, text, font, bounds, color, MeasureFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    readonly record struct TipLayout(Size Size, Rectangle Percent, Rectangle Detail, Rectangle Reset, Rectangle Rule, Rectangle Bot);

    static TipLayout MeasureTip(TipContent content)
    {
        var percent = Line(content.Percent, PercentFont);
        var detail = Line(content.Detail, DetailFont);
        var reset = Line(content.Reset, QuietFont);
        var bot = content.Bot is { } text ? Line(text, QuietFont) : Size.Empty;
        var inner = Math.Max(percent.Width, Math.Max(detail.Width, reset.Width));
        if (bot.Width > 0) inner = Math.Max(inner, bot.Width);
        inner = Math.Max(inner, 120);
        var width = inner + PadX * 2;
        var y = PadY;
        var percentBox = new Rectangle(PadX, y, inner, percent.Height);
        y += percent.Height + 2;
        var detailBox = new Rectangle(PadX, y, inner, detail.Height);
        y += detail.Height + 1;
        var resetBox = new Rectangle(PadX, y, inner, reset.Height);
        y += reset.Height;
        var rule = Rectangle.Empty;
        var botBox = Rectangle.Empty;
        if (bot.Width > 0)
        {
            y += 10;
            rule = new Rectangle(PadX, y, inner, 1);
            y += 9;
            botBox = new Rectangle(PadX, y, inner, bot.Height);
            y += bot.Height;
        }
        y += PadY;
        return new TipLayout(new Size(width, y), percentBox, detailBox, resetBox, rule, botBox);
    }

    static Size Line(string text, Font font)
    {
        var size = TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), MeasureFlags);
        return new Size(size.Width + 8, size.Height + 4);
    }

    void ApplyShape()
    {
        if (Width < 8 || Height < 8) return;
        using var path = Round(new Rectangle(0, 0, Width, Height), 14);
        var next = new Region(path);
        var previous = Region;
        Region = next;
        previous?.Dispose();
    }

    static GraphicsPath Round(Rectangle box, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(box.X, box.Y, d, d, 180, 90);
        path.AddArc(box.Right - d, box.Y, d, d, 270, 90);
        path.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90);
        path.AddArc(box.X, box.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
