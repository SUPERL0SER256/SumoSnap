using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SumoSnap;

public class ReminderItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime DueDateTime { get; set; }
    public string Title { get; set; } = "";
    public bool Notified { get; set; } = false;
}

public class AppSettings
{
    public string GeminiApiKey { get; set; } = "";
    public string OpenAiApiKey { get; set; } = "";
    public string AnthropicApiKey { get; set; } = "";
    public string ActiveProvider { get; set; } = "Gemini"; // "Gemini", "OpenAI", or "Anthropic"
    public bool ShowFloatingBuddy { get; set; } = true;
    public bool RunOnStartup { get; set; } = true;
    public bool HasSeenOnboarding { get; set; } = false;
    
    // Logic Puzzle Tracking
    public int DailyLogicStreak { get; set; } = 0;
    public int HighestLogicStreak { get; set; } = 0;
    public DateTime LastPuzzleSolvedDate { get; set; } = DateTime.MinValue;
    public string DailyPuzzleText { get; set; } = "";
    public string DailyPuzzleAnswer { get; set; } = "";
    public DateTime DailyPuzzleDate { get; set; } = DateTime.MinValue;
    public int DailyPuzzleAttemptsLeft { get; set; } = 3;
    public List<string> PastPuzzles { get; set; } = new();

    // Mini Sudoku 6x6 Daily Tracking
    public int SudokuSolvedCount { get; set; } = 0;
    public int BestSudokuStreak { get; set; } = 0;
    public DateTime DailySudokuDate { get; set; } = DateTime.MinValue;
    public DateTime LastSudokuSolvedDate { get; set; } = DateTime.MinValue;
    public int[] DailySudokuInitial { get; set; } = Array.Empty<int>();
    public int[] DailySudokuCurrent { get; set; } = Array.Empty<int>();

    // Calendar Reminders
    public List<ReminderItem> Reminders { get; set; } = new();
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
