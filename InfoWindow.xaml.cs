using System.Windows;

namespace SumoSnap;

public partial class InfoWindow : Window
{
    public InfoWindow(bool isOnboarding = false)
    {
        InitializeComponent();
        ThemeManager.ApplyDarkTitleBar(this);
        
        if (isOnboarding)
        {
            GetStartedBtn.Visibility = Visibility.Visible;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
