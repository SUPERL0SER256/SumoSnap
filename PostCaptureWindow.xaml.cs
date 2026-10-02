using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SumoSnap;

public partial class PostCaptureWindow : Window
{
    private BitmapSource _currentImage;

    
    private void Minimize_Click(object sender, RoutedEventArgs e) { this.WindowState = WindowState.Minimized; }
    private void Maximize_Click(object sender, RoutedEventArgs e) { this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    private void InfoButton_Click(object sender, RoutedEventArgs e) { new InfoWindow().ShowDialog(); }
    private void Close_Click(object sender, RoutedEventArgs e) { this.Close(); }

    public PostCaptureWindow(BitmapSource capturedImage)
    {
        InitializeComponent();
        ThemeManager.ApplyDarkTitleBar(this);
        _currentImage = capturedImage;
        PreviewImage.Source = _currentImage;
        
        UpdateUsageDisplay();
        
        ChatInput.Focus();
        this.PreviewKeyDown += (s, e) => 
        {
            if (e.Key == Key.Escape) this.WindowState = WindowState.Minimized;
        };
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataObject = new System.Windows.DataObject();
            dataObject.SetImage(_currentImage);

            var pngEncoder = new PngBitmapEncoder();
            pngEncoder.Frames.Add(BitmapFrame.Create(_currentImage));
            var ms = new MemoryStream();
            pngEncoder.Save(ms);
            dataObject.SetData("PNG", ms, false);

            System.Windows.Clipboard.SetDataObject(dataObject, true);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to copy: {ex.Message}");
        }
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG Image|*.png|JPEG Image|*.jpg",
            DefaultExt = ".png"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            BitmapEncoder encoder = saveFileDialog.FilterIndex == 2 
                ? new JpegBitmapEncoder() 
                : new PngBitmapEncoder();
                
            encoder.Frames.Add(BitmapFrame.Create(_currentImage));
            
            using (var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create))
            {
                encoder.Save(fileStream);
            }
            Close();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow().ShowDialog();
        UpdateUsageDisplay();
        ((App)System.Windows.Application.Current).SyncBuddyVisibility();
    }

    
    private void TriggerKeystrokePulse()
    {
        if (ChatChromeFocused != null)
        {
            var thicknessAnim = new System.Windows.Media.Animation.ThicknessAnimation();
            thicknessAnim.From = new System.Windows.Thickness(1.4);
            thicknessAnim.To = new System.Windows.Thickness(0);
            thicknessAnim.Duration = new System.Windows.Duration(TimeSpan.FromMilliseconds(300));
            thicknessAnim.EasingFunction = new System.Windows.Media.Animation.QuinticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            
            ChatChromeFocused.BeginAnimation(System.Windows.FrameworkElement.MarginProperty, thicknessAnim, System.Windows.Media.Animation.HandoffBehavior.Compose);
        }
    } 

    private void ChatInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendButton_Click(sender, e);
            e.Handled = true;
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        string userMessage = ChatInput.Text.Trim();
        if (string.IsNullOrEmpty(userMessage)) return;

        AddChatBubble(userMessage, isUser: true);
        ChatInput.Text = "";
        
        Border thinkingBubble = null;
        try
        {
            SendButton.Visibility = Visibility.Collapsed;
            LoadingIndicator.Visibility = Visibility.Visible;
            ChatInput.IsEnabled = false;

            thinkingBubble = AddChatBubble("Thinking...", isUser: false);

            var aiClient = AiProviderFactory.CreateClient();
            AiResponse response = await aiClient.ChatWithImageAsync(_currentImage, userMessage);
            
            if (thinkingBubble != null) ChatMessages.Children.Remove(thinkingBubble);
            AddChatBubble(response.Text, isUser: false);

            if (response.TokensUsed > 0 || aiClient is GeminiClient)
            {
                var settings = SettingsManager.LoadSettings();
                settings.TotalTokensUsed += response.TokensUsed;
                settings.DailyRequestsCount += 1;
                settings.LastRequestDate = DateTime.Now;
                SettingsManager.SaveSettings(settings);
                UpdateUsageDisplay();
            }
        }
        catch (MissingKeyException ex)
        {
            if (thinkingBubble != null) ChatMessages.Children.Remove(thinkingBubble);
            var result = System.Windows.MessageBox.Show($"Please enter your {ex.Message} API key in Settings.", "Missing API Key", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                new SettingsWindow().ShowDialog();
            }
        }
        catch (Exception)
        {
            if (thinkingBubble != null) ChatMessages.Children.Remove(thinkingBubble);
            AddChatBubble("The AI server is currently busy or unresponsive. Please try sending your message again.", isUser: false);
        }
        finally
        {
            SendButton.Visibility = Visibility.Visible;
            LoadingIndicator.Visibility = Visibility.Collapsed;
            ChatInput.IsEnabled = true;
            ChatInput.Focus();
        }
    }

    private void UpdateUsageDisplay()
    {
        var settings = SettingsManager.LoadSettings();

        int maxTokens = settings.MonthlyTokenBudget > 0 ? settings.MonthlyTokenBudget : 100000;
        int remaining = Math.Max(0, maxTokens - settings.TotalTokensUsed);
        
        UsageText.Text = $"Tokens: {settings.TotalTokensUsed:N0} / {maxTokens:N0}";
        
        // Highlight in red if they exceed the budget
        UsageText.Foreground = settings.TotalTokensUsed >= maxTokens 
            ? System.Windows.Media.Brushes.Red 
            : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#888888"));
    }

    private Border AddChatBubble(string text, bool isUser)
    {
        var userColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#050508"); // Pure black
        var aiColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1C1C1E");

        var bubble = new Border
        {
            Background = new SolidColorBrush(isUser ? userColor : aiColor),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(15, 10, 15, 10),
            Margin = new Thickness(isUser ? 50 : 0, 0, isUser ? 0 : 50, 15),
            HorizontalAlignment = isUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left
        };

        var textBox = new System.Windows.Controls.TextBox
        {
            Text = text,
            Foreground = System.Windows.Media.Brushes.White,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            IsReadOnly = true,
            Cursor = System.Windows.Input.Cursors.IBeam
        };

        bubble.Child = textBox;
        ChatMessages.Children.Add(bubble);
        MainScroll.ScrollToEnd();
        return bubble;
    }
}
