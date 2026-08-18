using System.Runtime.InteropServices;

namespace GrokReserve.Ui;

static class NativeChrome
{
    const int DwmwaWindowCornerPreference = 33;
    const int DwmwaUseImmersiveDarkMode = 20;
    const int DwmwaBorderColor = 34;
    const int DwmWcpRound = 2;
    const int DwmWcpRoundSmall = 3;

    public static void ApplyPopover(Form form)
    {
        if (!form.IsHandleCreated) return;
        var corner = DwmWcpRoundSmall;
        if (DwmSetWindowAttribute(form.Handle, DwmwaWindowCornerPreference, ref corner, sizeof(int)) != 0)
            return;
        SetBorder(form, Theme.Current.Hairline);
    }

    public static void ApplyWindow(Form form)
    {
        if (!form.IsHandleCreated) return;
        var dark = Theme.IsLight ? 0 : 1;
        DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        var corner = DwmWcpRound;
        DwmSetWindowAttribute(form.Handle, DwmwaWindowCornerPreference, ref corner, sizeof(int));
        SetBorder(form, Theme.Current.Hairline);
    }

    static void SetBorder(Form form, Color color)
    {
        var value = color.R | (color.G << 8) | (color.B << 16);
        DwmSetWindowAttribute(form.Handle, DwmwaBorderColor, ref value, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
