using System.Reflection;
using System.Runtime.InteropServices;

namespace GrokReserve.Ui;

static class TrayIconBounds
{
    public static Rectangle From(NotifyIcon tray, Point fallback)
    {
        if (TryGetNotifyIconRect(tray, out var rect) && rect.Width > 0 && rect.Height > 0)
            return rect;
        return new Rectangle(fallback.X - 12, fallback.Y - 12, 24, 24);
    }

    static bool TryGetNotifyIconRect(NotifyIcon tray, out Rectangle rect)
    {
        rect = Rectangle.Empty;
        if (!TryReadNativeWindow(tray, out var hwnd, out var id) || hwnd == IntPtr.Zero)
            return false;

        var identifier = new NotifyIconIdentifier
        {
            CbSize = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            HWnd = hwnd,
            Uid = id,
            GuidItem = Guid.Empty,
        };
        if (Shell_NotifyIconGetRect(ref identifier, out var native) != 0)
            return false;

        rect = Rectangle.FromLTRB(native.Left, native.Top, native.Right, native.Bottom);
        return true;
    }

    static bool TryReadNativeWindow(NotifyIcon tray, out IntPtr hwnd, out uint id)
    {
        hwnd = IntPtr.Zero;
        id = 0;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var field in typeof(NotifyIcon).GetFields(flags))
        {
            var value = field.GetValue(tray);
            if (value is NativeWindow window && window.Handle != IntPtr.Zero)
                hwnd = window.Handle;
            else if (IsIdField(field.Name) && value is int parsed)
                id = (uint)parsed;
        }
        return hwnd != IntPtr.Zero;
    }

    static bool IsIdField(string name) =>
        name.Equals("id", StringComparison.OrdinalIgnoreCase)
        || name.Equals("_id", StringComparison.OrdinalIgnoreCase);

    [DllImport("shell32.dll")]
    static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out NativeRect iconLocation);

    [StructLayout(LayoutKind.Sequential)]
    struct NotifyIconIdentifier
    {
        public uint CbSize;
        public IntPtr HWnd;
        public uint Uid;
        public Guid GuidItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
