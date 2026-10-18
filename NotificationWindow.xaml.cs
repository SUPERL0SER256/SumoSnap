using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SumoSnap;

public partial class NotificationWindow : Window
{
    private readonly DispatcherTimer _autoCloseTimer;

    public NotificationWindow(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;

        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(7)
        };
        _autoCloseTimer.Tick += (s, e) =>
        {
            _autoCloseTimer.Stop();
            FadeOutAndClose();
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        this.Left = workArea.Right - this.ActualWidth - 16;
        this.Top = workArea.Bottom - this.ActualHeight - 16;

        this.Opacity = 0;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
        this.BeginAnimation(OpacityProperty, fadeIn);

        _autoCloseTimer.Start();
    }

    private void FadeOutAndClose()
    {
        var fadeOut = new DoubleAnimation(this.Opacity, 0, TimeSpan.FromMilliseconds(220));
        fadeOut.Completed += (s, e) => this.Close();
        this.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        FadeOutAndClose();
    }

    public static void ShowDarkCard(string title, string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var card = new NotificationWindow(title, message);
            card.Show();
        });
    }
}
