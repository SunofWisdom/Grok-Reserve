using GrokReserve.Core;
using GrokReserve.Ui;

namespace GrokReserve;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Theme.Start();
        var demo = args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        var prefs = Prefs.Load();
        if (prefs.StartWithWindows && !Startup.IsEnabled())
            Startup.SetEnabled(true);
        var store = new Store(prefs, demo);
        store.Start();
        Application.Run(new TrayApplication(store));
    }
}
