using System.Windows;

namespace SumoSnap;

public partial class InfoWindow : Window
{
    public InfoWindow()
    {
        InitializeComponent();
        ThemeManager.ApplyDarkTitleBar(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
