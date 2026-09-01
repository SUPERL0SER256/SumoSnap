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
        TxtRemoveBg.Text = settings.RemoveBgApiKey;
        TxtStability.Text = settings.StabilityApiKey;
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
        var settings = new AppSettings
        {
            RemoveBgApiKey = TxtRemoveBg.Text.Trim(),
            StabilityApiKey = TxtStability.Text.Trim()
        };

        SettingsManager.SaveSettings(settings);
        DialogResult = true;
        Close();
    }
}
