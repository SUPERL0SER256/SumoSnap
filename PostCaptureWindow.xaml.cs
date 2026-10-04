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

    private void Minimize_Click(object sender, RoutedEventArgs e) { this.Hide(); }
    private void Maximize_Click(object sender, RoutedEventArgs e) { this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    private void InfoButton_Click(object sender, RoutedEventArgs e) { new InfoWindow().ShowDialog(); }
    
    // Instead of completely destroying the session on close, we hide it to the system tray
    // so the user can bring it back if they need to check the AI's answer again.
    private void Close_Click(object sender, RoutedEventArgs e) { this.Hide(); }

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
            if (e.Key == Key.Escape) 
            {
                this.Hide();
                e.Handled = true;
            }
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

    
    private void UpdateSendButtonGlow(bool isHovering)
    {
        bool hasText = ChatInput.Text.Trim().Length > 0;
        bool shouldGlow = hasText || isHovering;

        var outerRing = SendButton.Template.FindName("OuterRingHover", SendButton) as System.Windows.Controls.Border;
        var innerOverlay = SendButton.Template.FindName("InnerHoverOverlay", SendButton) as System.Windows.Controls.Border;

        if (outerRing != null && innerOverlay != null)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(shouldGlow ? 1.0 : 0.0, new System.Windows.Duration(TimeSpan.FromMilliseconds(shouldGlow ? 200 : 300)));
            outerRing.BeginAnimation(System.Windows.UIElement.OpacityProperty, anim);
            innerOverlay.BeginAnimation(System.Windows.UIElement.OpacityProperty, anim);
        }
    }

    private void ChatInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateSendButtonGlow(SendButton.IsMouseOver);
    }

    private void SendButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateSendButtonGlow(true);
    }

    private void SendButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateSendButtonGlow(false);
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
        
        FrameworkElement thinkingBubble = null;
        try
        {
            SendButton.Visibility = Visibility.Collapsed;
            ChatInput.IsEnabled = false;

            thinkingBubble = CreateMetallicLoader();

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
        catch (Exception ex)
        {
            if (thinkingBubble != null) ChatMessages.Children.Remove(thinkingBubble);
            
            string errorMsg = ex.Message;
            if (errorMsg.Contains("TooManyRequests") || errorMsg.Contains("429"))
            {
                AddChatBubble("Rate limit exceeded. You've sent too many requests to the AI provider. Please wait a moment and try again.", isUser: false);
            }
            else if (errorMsg.Contains("Unauthorized") || errorMsg.Contains("401") || errorMsg.Contains("Forbidden") || errorMsg.Contains("403"))
            {
                AddChatBubble("Authentication failed. Your API key might be invalid, expired, or lacking permissions. Please check your settings.", isUser: false);
            }
            else if (errorMsg.Contains("InternalServerError") || errorMsg.Contains("500") || errorMsg.Contains("502") || errorMsg.Contains("503"))
            {
                AddChatBubble("The AI provider's servers are currently down or returning errors. Please try again later.", isUser: false);
            }
            else
            {
                AddChatBubble($"An unexpected error occurred communicating with the AI. Details: {ex.Message}", isUser: false);
            }
        }
        finally
        {
            SendButton.Visibility = Visibility.Visible;
            LoadingIndicator.Visibility = Visibility.Collapsed;
            ChatInput.IsEnabled = true;
            ChatInput.Focus();
        }
    }

    private FrameworkElement CreateMetallicLoader()
    {
        const double d = 6;
        var canvas = new System.Windows.Controls.Canvas
        {
            Width = 22,
            Height = 20,
            Margin = new Thickness(16, 8, 0, 12),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        };

        var points = new[] { new System.Windows.Point(8, 0), new System.Windows.Point(1, 11), new System.Windows.Point(15, 11) };
        var flatGrey = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC8, 0xC8, 0xCC));
        flatGrey.Freeze();

        for (int i = 0; i < 3; i++)
        {
            var scale = new System.Windows.Media.ScaleTransform(0.7, 0.7);
            var ball = new System.Windows.Shapes.Ellipse
            {
                Width = d,
                Height = d,
                Fill = flatGrey,
                Opacity = 0.5,
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                RenderTransform = scale
            };
            System.Windows.Controls.Canvas.SetLeft(ball, points[i].X);
            System.Windows.Controls.Canvas.SetTop(ball, points[i].Y);
            canvas.Children.Add(ball);

            var begin = TimeSpan.FromMilliseconds(i * 250);
            var total = TimeSpan.FromMilliseconds(1000);

            System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames Pulse(double idle, double peak)
            {
                var kf = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames
                {
                    BeginTime = begin,
                    Duration = new Duration(total),
                    RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                };
                kf.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(peak, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250)),
                    new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }));
                kf.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(idle, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600)),
                    new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }));
                kf.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(idle, System.Windows.Media.Animation.KeyTime.FromTimeSpan(total)));
                return kf;
            }

            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, Pulse(0.7, 1.3));
            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, Pulse(0.7, 1.3));
            ball.BeginAnimation(System.Windows.UIElement.OpacityProperty, Pulse(0.5, 1.0));
        }

        ChatMessages.Children.Add(canvas);
        MainScroll.ScrollToBottom();
        return canvas;
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

        System.Windows.Controls.TextBox MakeTextBox(string t) => new System.Windows.Controls.TextBox
        {
            Text = t,
            Foreground = System.Windows.Media.Brushes.White,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            IsReadOnly = true,
            Cursor = System.Windows.Input.Cursors.IBeam
        };

        var segments = isUser ? null : CodeBlockRenderer.Parse(text);
        if (segments == null || !segments.Exists(s => s is CodeBlockRenderer.CodeSegment))
        {
            bubble.Child = MakeTextBox(text);
        }
        else
        {
            var stack = new StackPanel();
            foreach (var seg in segments)
            {
                if (seg is CodeBlockRenderer.CodeSegment code)
                    stack.Children.Add(CodeBlockRenderer.BuildCodeBlock(code.Language, code.Code, MainScroll));
                else if (seg is CodeBlockRenderer.TextSegment txt)
                    stack.Children.Add(MakeTextBox(txt.Text));
            }
            bubble.Child = stack;
        }

        ChatMessages.Children.Add(bubble);
        MainScroll.ScrollToEnd();
        return bubble;
    }
}
