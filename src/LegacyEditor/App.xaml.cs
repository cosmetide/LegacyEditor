using System.Diagnostics;
using System.Windows;
using LegacyEditor.Services;

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
        var settings = SettingsService.Load();
        _currentSettings = settings;

        var main = new Views.WelcomeWindow();
        MainWindow = main;
        main.Show();
    }

    internal static AppSettings _currentSettings = new();

    public static AppSettings CurrentSettings => _currentSettings;

    public static void SaveSettings()
    {
        SettingsService.Save(_currentSettings);
    }

    public static void SetTheme()
    {
        // Dark theme only
    }
}