using Microsoft.Win32;

namespace GrokReserve.Ui;

sealed record Palette(
    Color Window,
    Color Card,
    Color Text,
    Color Muted,
    Color Subtle,
    Color Hairline,
    Color Track,
    Color Hover,
    Color AccentSoft,
    Color AccentText);

static class Theme
{
    static readonly UserPreferenceChangedEventHandler PreferenceHandler = OnPreferenceChanged;

    public static event Action? Changed;
    public static bool IsLight { get; private set; } = true;
    public static Palette Current { get; private set; }

    public static readonly Palette Light = new(
        Window: Color.FromArgb(0xF6, 0xF5, 0xF1),
        Card: Color.FromArgb(0xEE, 0xED, 0xE8),
        Text: Color.FromArgb(0x1C, 0x1C, 0x1A),
        Muted: Color.FromArgb(0x6A, 0x6A, 0x64),
        Subtle: Color.FromArgb(0x8E, 0x8E, 0x86),
        Hairline: Color.FromArgb(0xDD, 0xDB, 0xD4),
        Track: Color.FromArgb(0xE3, 0xE1, 0xDA),
        Hover: Color.FromArgb(0xE7, 0xE6, 0xE0),
        AccentSoft: Color.FromArgb(0xF4, 0xE6, 0xCC),
        AccentText: Color.FromArgb(0xB8, 0x6A, 0x00));

    public static readonly Palette Dark = new(
        Window: Color.FromArgb(0x2A, 0x2A, 0x28),
        Card: Color.FromArgb(0x33, 0x33, 0x30),
        Text: Color.FromArgb(0xF3, 0xF2, 0xEC),
        Muted: Color.FromArgb(0xA8, 0xA8, 0xA0),
        Subtle: Color.FromArgb(0x86, 0x86, 0x7E),
        Hairline: Color.FromArgb(0x44, 0x44, 0x40),
        Track: Color.FromArgb(0x3C, 0x3C, 0x38),
        Hover: Color.FromArgb(0x3A, 0x3A, 0x36),
        AccentSoft: Color.FromArgb(0x4A, 0x38, 0x1C),
        AccentText: Color.FromArgb(0xFF, 0xB0, 0x2A));

    static Theme() => Current = Light;

    public static void Start()
    {
        Refresh();
        SystemEvents.UserPreferenceChanged -= PreferenceHandler;
        SystemEvents.UserPreferenceChanged += PreferenceHandler;
    }

    public static void Stop()
    {
        SystemEvents.UserPreferenceChanged -= PreferenceHandler;
    }

    public static void Refresh()
    {
        var light = ReadLight();
        var next = light ? Light : Dark;
        if (light == IsLight && ReferenceEquals(Current, next)) return;
        IsLight = light;
        Current = next;
        Changed?.Invoke();
    }

    static void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General
            or UserPreferenceCategory.VisualStyle
            or UserPreferenceCategory.Color)
            Refresh();
    }

    static bool ReadLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch
        {
            return true;
        }
    }
}
