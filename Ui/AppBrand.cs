using System.Reflection;

namespace GrokReserve.Ui;

static class AppBrand
{
    static Icon? _window;
    static Image? _mark;

    public static Icon? WindowIcon
    {
        get
        {
            if (_window is not null) return _window;
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("grok.ico", StringComparison.OrdinalIgnoreCase));
            if (name is not null)
            {
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream is not null)
                    _window = new Icon(stream);
            }
            _window ??= Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            return _window;
        }
    }

    public static Image? Mark(Color color, int size)
    {
        var source = SourceMark();
        if (source is null) return null;
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        using var tinted = Tint(source, color);
        g.DrawImage(tinted, new Rectangle(0, 0, size, size));
        return bmp;
    }

    static Image? SourceMark()
    {
        if (_mark is not null) return _mark;
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("grok.png", StringComparison.OrdinalIgnoreCase));
        if (name is null) return null;
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null) return null;
        _mark = new Bitmap(stream);
        return _mark;
    }

    static Bitmap Tint(Image source, Color color)
    {
        var bmp = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        var matrix = new System.Drawing.Imaging.ColorMatrix([
            [0, 0, 0, 0, 0],
            [0, 0, 0, 0, 0],
            [0, 0, 0, 0, 0],
            [0, 0, 0, 1, 0],
            [color.R / 255f, color.G / 255f, color.B / 255f, 0, 1],
        ]);
        using var attrs = new System.Drawing.Imaging.ImageAttributes();
        attrs.SetColorMatrix(matrix);
        g.DrawImage(source, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
        return bmp;
    }
}
