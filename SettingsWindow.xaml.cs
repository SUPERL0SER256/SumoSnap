using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace SumoSnap;

public partial class SettingsWindow : Window
{
    
    private void Minimize_Click(object sender, RoutedEventArgs e) { this.WindowState = WindowState.Minimized; }
    private void Close_Click(object sender, RoutedEventArgs e) { this.Close(); }

    public SettingsWindow()
    {
        InitializeComponent();
        ThemeManager.ApplyDarkTitleBar(this);
        
        var settings = SettingsManager.LoadSettings();
        TxtGemini.Password = settings.GeminiApiKey;
        TxtOpenAI.Password = settings.OpenAiApiKey;
        TxtAnthropic.Password = settings.AnthropicApiKey;
        TxtTokenBudget.Text = settings.MonthlyTokenBudget.ToString();

        if (settings.ActiveProvider == "OpenAI")
            RadioOpenAI.IsChecked = true;
        else if (settings.ActiveProvider == "Anthropic")
            RadioAnthropic.IsChecked = true;
        else if (settings.ActiveProvider == "GeminiPro")
            RadioGeminiPro.IsChecked = true;
        else
            RadioGemini.IsChecked = true;

        ChkShowBuddy.IsChecked = settings.ShowFloatingBuddy;
        ChkRunOnStartup.IsChecked = settings.RunOnStartup;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string active = "Gemini";
        if (RadioGeminiPro.IsChecked == true) active = "GeminiPro";
        if (RadioOpenAI.IsChecked == true) active = "OpenAI";
        if (RadioAnthropic.IsChecked == true) active = "Anthropic";

        int.TryParse(TxtTokenBudget.Text.Trim(), out int tokenBudget);

        // Update the existing settings so fields not shown here (e.g. HasSeenOnboarding) are preserved
        var settings = SettingsManager.LoadSettings();
        settings.GeminiApiKey = TxtGemini.Password.Trim();
        settings.OpenAiApiKey = TxtOpenAI.Password.Trim();
        settings.AnthropicApiKey = TxtAnthropic.Password.Trim();
        settings.ActiveProvider = active;
        settings.MonthlyTokenBudget = tokenBudget;
        settings.ShowFloatingBuddy = ChkShowBuddy.IsChecked ?? true;
        settings.RunOnStartup = ChkRunOnStartup.IsChecked ?? true;

        SettingsManager.SaveSettings(settings);
        StartupManager.Apply(settings.RunOnStartup);
        DialogResult = true;
        Close();
    }

    private void ResetTokensButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = SettingsManager.LoadSettings();
        settings.TotalTokensUsed = 0;
        SettingsManager.SaveSettings(settings);
        System.Windows.MessageBox.Show("Your token usage has been reset to 0.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
