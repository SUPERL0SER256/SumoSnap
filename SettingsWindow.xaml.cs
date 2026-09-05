using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace SumoSnap;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        ThemeManager.ApplyDarkTitleBar(this);
        
        var settings = SettingsManager.LoadSettings();
        TxtGemini.Text = settings.GeminiApiKey;
        TxtOpenAI.Text = settings.OpenAiApiKey;
        TxtAnthropic.Text = settings.AnthropicApiKey;

        if (settings.ActiveProvider == "OpenAI")
            RadioOpenAI.IsChecked = true;
        else if (settings.ActiveProvider == "Anthropic")
            RadioAnthropic.IsChecked = true;
        else
            RadioGemini.IsChecked = true;
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
        if (RadioOpenAI.IsChecked == true) active = "OpenAI";
        if (RadioAnthropic.IsChecked == true) active = "Anthropic";

        var settings = new AppSettings
        {
            GeminiApiKey = TxtGemini.Text.Trim(),
            OpenAiApiKey = TxtOpenAI.Text.Trim(),
            AnthropicApiKey = TxtAnthropic.Text.Trim(),
            ActiveProvider = active
        };

        SettingsManager.SaveSettings(settings);
        DialogResult = true;
        Close();
    }
}
