using System.Diagnostics;
using System.Windows;

namespace LegacyEditor;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            Debug.WriteLine($"[LegacyEditor] Unhandled: {e.Exception}");
            e.Handled = true;
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        var main = new Views.WelcomeWindow();
        MainWindow = main;
        main.Show();
    }
}
