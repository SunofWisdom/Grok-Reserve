using System.Runtime.InteropServices;
using GrokReserve.Core;

namespace GrokReserve.Ui;

public static class GaugeIcon
{
    const int Size = 32;

    public static Icon Create(double? remainingPercent, string paceKind)
    {
        var color = Format.PaceColor(paceKind);
        using var bmp = new Bitmap(Size, Size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            var stroke = 3.6f;
            var pad = stroke / 2f + 0.6f;
            var rect = new RectangleF(pad, pad, Size - pad * 2, Size - pad * 2);
            using (var track = new Pen(Color.FromArgb(56, 255, 255, 255), stroke))
                g.DrawEllipse(track, rect);
            if (remainingPercent is { } left and > 0)
            {
                using var fill = new Pen(color, stroke)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round,
                };
                var sweep = (float)(Math.Clamp(left, 0, 100) / 100.0 * 360.0);
                g.DrawArc(fill, rect, -90, sweep);
            }
            var dest = new Rectangle(8, 8, 16, 16);
            using var mark = AppBrand.Mark(color, 16);
            if (mark is null)
            {
                using var brush = new SolidBrush(color);
                g.FillEllipse(brush, dest);
            }
            else
            {
                g.DrawImage(mark, dest);
            }
        }
        var handle = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyIcon(IntPtr hIcon);
}
