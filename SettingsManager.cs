using System;
using System.IO;
using System.Text.Json;

namespace SumoSnap;

public class AppSettings
{
    public string RemoveBgApiKey { get; set; } = "";
    public string StabilityApiKey { get; set; } = "";
    public string GeminiApiKey { get; set; } = "";
}

public static class SettingsManager
{
    private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SumoSnap");
    private static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "settings.json");

    public static AppSettings LoadSettings()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void SaveSettings(AppSettings settings)
    {
        if (!Directory.Exists(AppDataFolder))
        {
            Directory.CreateDirectory(AppDataFolder);
        }

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFilePath, json);
    }
}
