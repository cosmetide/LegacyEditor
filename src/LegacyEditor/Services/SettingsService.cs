using System.IO;
using System.Text.Json;

namespace LegacyEditor.Services;

public class AppSettings
{
    public bool DarkMode { get; set; } = true;
    public int WipeEmptyXpLevel { get; set; } = 1;
    public int WipeEmptyItemCount { get; set; } = 1;
}

public static class SettingsService
{
    static string SettingsPath => System.IO.Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOpts);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}