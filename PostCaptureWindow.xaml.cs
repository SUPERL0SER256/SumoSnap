using System;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SumoSnap;

public partial class PostCaptureWindow : Window
{
    private bool _isGameMode = false;
    private string _gameSystemPrompt = @"You are a diverse logic puzzle generator. Generate a short logic puzzle. Mix it up randomly between: number patterns, spatial reasoning described in text, letter sequences, and classic logic riddles. Output ONLY a valid JSON object without markdown. Schema: { ""puzzle"": ""the puzzle text"", ""answer"": ""the exact brief answer"" }. Make it very concise to save tokens.";

    private async void GameToggle_Click(object sender, RoutedEventArgs e)
    {
        _isGameMode = !_isGameMode;
        if (_isGameMode)
        {
            GameToggleButton.Content = "\uE8BD"; // Chat icon
            GameToggleButton.ToolTip = "Chat Mode";
            
            MainScroll.Visibility = Visibility.Collapsed;
            ChatInputBar.Visibility = Visibility.Collapsed;
            
            GamePanel.Visibility = Visibility.Visible;
            GameInputBar.Visibility = Visibility.Visible;
            
            var settings = SettingsManager.LoadSettings();
            StreakText.Text = $"Streak: {settings.DailyLogicStreak}";
            BestStreakText.Text = $"Best: {settings.HighestLogicStreak}";

            if (settings.LastPuzzleSolvedDate.Date == DateTime.Today)
            {
                GameText.Text = "You've already played today's puzzle! Come back tomorrow for a new challenge.";
                GameInput.Visibility = Visibility.Collapsed;
                GameSubmitButton.Visibility = Visibility.Collapsed;
                TriesIndicator.Visibility = Visibility.Collapsed;
            }
            else if (settings.DailyPuzzleDate.Date == DateTime.Today)
            {
                // Resume today's puzzle
                GameText.Text = settings.DailyPuzzleText;
                TriesIndicator.Text = $"{settings.DailyPuzzleAttemptsLeft} Tries Left";
                GameInput.Visibility = Visibility.Visible;
                GameSubmitButton.Visibility = Visibility.Visible;
                TriesIndicator.Visibility = Visibility.Visible;
                GameInput.IsEnabled = true;
                GameInput.Focus();
            }
            else
            {
                // Generate new puzzle
                await StartNewGameAsync();
            }
        }
        else
        {
            GameToggleButton.Content = "\uE7FC"; // Gamepad icon
            GameToggleButton.ToolTip = "Game Mode";
            
            GamePanel.Visibility = Visibility.Collapsed;
            GameInputBar.Visibility = Visibility.Collapsed;
            
            MainScroll.Visibility = Visibility.Visible;
            ChatInputBar.Visibility = Visibility.Visible;
        }
    }

    private async Task StartNewGameAsync()
    {
        GameText.Text = "Generating today's logic puzzle...";
        GameButtonsPanel.Visibility = Visibility.Collapsed;
        GameSubmitButton.Visibility = Visibility.Visible;
        TriesIndicator.Visibility = Visibility.Visible;
        GameLoadingIndicator.Visibility = Visibility.Visible;
        GameInput.Visibility = Visibility.Visible;
        GameInput.IsEnabled = false;

        try
        {
            var aiClient = AiProviderFactory.CreateClient();
            AiResponse response = await aiClient.ChatAsync("Generate a new logic puzzle.", _gameSystemPrompt);
            
            string json = response.Text.Replace("```json", "").Replace("```", "").Trim();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var settings = SettingsManager.LoadSettings();
            if (root.TryGetProperty("puzzle", out var puzzleProp))
                settings.DailyPuzzleText = puzzleProp.GetString();
            if (root.TryGetProperty("answer", out var ansProp))
                settings.DailyPuzzleAnswer = ansProp.GetString();
                
            settings.DailyPuzzleDate = DateTime.Today;
            settings.DailyPuzzleAttemptsLeft = 3;
            SettingsManager.SaveSettings(settings);

            GameText.Text = settings.DailyPuzzleText;
            TriesIndicator.Text = "3 Tries Left";
        }
        catch (Exception ex)
        {
            GameText.Text = "Failed to generate puzzle. " + ex.Message;
        }
        finally
        {
            GameButtonsPanel.Visibility = Visibility.Visible;
            GameLoadingIndicator.Visibility = Visibility.Collapsed;
            GameInput.IsEnabled = true;
            GameInput.Focus();
        }
    }

    private void GameNextButton_Click(object sender, RoutedEventArgs e)
    {
        GameToggle_Click(this, new RoutedEventArgs());
    }

    private async void GameSubmitButton_Click(object sender, RoutedEventArgs e)
    {
        string answer = GameInput.Text.Trim();
        if (string.IsNullOrEmpty(answer)) return;

        var settings = SettingsManager.LoadSettings();

        GameInput.Text = "";
        GameButtonsPanel.Visibility = Visibility.Collapsed;
        GameLoadingIndicator.Visibility = Visibility.Visible;
        GameInput.IsEnabled = false;

        try
        {
            var aiClient = AiProviderFactory.CreateClient();
            string prompt = $"The puzzle was:\n{settings.DailyPuzzleText}\n\nThe correct answer is: {settings.DailyPuzzleAnswer}\n\nThe user guessed: '{answer}'. Evaluate their guess. If they are completely correct or conceptually correct, reply EXACTLY with 'CORRECT'. If they are wrong, reply EXACTLY with 'WRONG'. Do not include markdown.";
            AiResponse response = await aiClient.ChatAsync(prompt, "You are a puzzle master evaluating a user's guess.");
            
            string reply = response.Text.Trim().ToUpper();
            if (reply.StartsWith("CORRECT"))
            {
                settings.DailyLogicStreak++;
                if (settings.DailyLogicStreak > settings.HighestLogicStreak) 
                    settings.HighestLogicStreak = settings.DailyLogicStreak;
                settings.LastPuzzleSolvedDate = DateTime.Today;
                SettingsManager.SaveSettings(settings);

                StreakText.Text = $"Streak: {settings.DailyLogicStreak}";
                BestStreakText.Text = $"Best: {settings.HighestLogicStreak}";
                GameText.Text = "Correct! Come back tomorrow for the next puzzle.\n\n" + settings.DailyPuzzleText;
                
                GameInput.Visibility = Visibility.Collapsed;
                GameSubmitButton.Visibility = Visibility.Collapsed;
                TriesIndicator.Visibility = Visibility.Collapsed;
            }
            else
            {
                settings.DailyPuzzleAttemptsLeft--;
                if (settings.DailyPuzzleAttemptsLeft <= 0)
                {
                    settings.DailyLogicStreak = 0;
                    settings.LastPuzzleSolvedDate = DateTime.Today;
                    SettingsManager.SaveSettings(settings);

                    StreakText.Text = "Streak: 0";
                    GameText.Text = $"Out of tries! The correct answer was: {settings.DailyPuzzleAnswer}\n\nCome back tomorrow for the next puzzle.\n\n{settings.DailyPuzzleText}";
                    
                    GameInput.Visibility = Visibility.Collapsed;
                    GameSubmitButton.Visibility = Visibility.Collapsed;
                    TriesIndicator.Visibility = Visibility.Collapsed;
                }
                else
                {
                    SettingsManager.SaveSettings(settings);
                    TriesIndicator.Text = $"{settings.DailyPuzzleAttemptsLeft} Tries Left";
                    GameText.Text = $"Wrong guess!\n\n" + settings.DailyPuzzleText;
                }
            }
        }
        catch (Exception ex)
        {
            GameText.Text = "Error checking answer: " + ex.Message + "\n\n" + settings.DailyPuzzleText;
        }
        finally
        {
            GameButtonsPanel.Visibility = Visibility.Visible;
            GameLoadingIndicator.Visibility = Visibility.Collapsed;
            GameInput.IsEnabled = true;
            GameInput.Focus();
        }
    }

    private void GameInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (GameSubmitButton.Visibility == Visibility.Visible)
                GameSubmitButton_Click(sender, e);
            else if (GameNextButton.Visibility == Visibility.Visible)
                GameNextButton_Click(sender, e);
            e.Handled = true;
        }
    }
    private BitmapSource _currentImage;

    private void Minimize_Click(object sender, RoutedEventArgs e) { this.WindowState = WindowState.Minimized; }
    private void Maximize_Click(object sender, RoutedEventArgs e) { this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    private void InfoButton_Click(object sender, RoutedEventArgs e) { new InfoWindow().ShowDialog(); }
    
    // Completely destroy the session on close
    private void Close_Click(object sender, RoutedEventArgs e) { this.Close(); }

    public PostCaptureWindow(BitmapSource capturedImage)
    {
        InitializeComponent();
        ThemeManager.ApplyDarkTitleBar(this);
        _currentImage = capturedImage;
        PreviewImage.Source = _currentImage;
        
        
        
        ChatInput.Focus();
        this.PreviewKeyDown += (s, e) => 
        {
            if (e.Key == Key.Escape) 
            {
                this.WindowState = WindowState.Minimized;
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

    
        private void UpdateLiquidGlow(System.Windows.Controls.Button btn, System.Windows.Controls.TextBox input, bool isHovering)
    {
        bool hasText = input.Text.Trim().Length > 0;
        bool shouldGlow = hasText || isHovering;

        var outerRing = btn.Template.FindName("OuterRingHover", btn) as System.Windows.Controls.Border;
        var innerOverlay = btn.Template.FindName("InnerHoverOverlay", btn) as System.Windows.Controls.Border;

        if (outerRing != null && innerOverlay != null)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(shouldGlow ? 1.0 : 0.0, new System.Windows.Duration(TimeSpan.FromMilliseconds(shouldGlow ? 200 : 300)));
            outerRing.BeginAnimation(System.Windows.UIElement.OpacityProperty, anim);
            innerOverlay.BeginAnimation(System.Windows.UIElement.OpacityProperty, anim);
        }
    }

    private void ChatInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateLiquidGlow(SendButton, ChatInput, SendButton.IsMouseOver);
    }

    private void SendButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateLiquidGlow(SendButton, ChatInput, true);
    }

    private void SendButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateLiquidGlow(SendButton, ChatInput, false);
    }

    private void GameInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        UpdateLiquidGlow(GameSubmitButton, GameInput, GameSubmitButton.IsMouseOver);
    }

    private void GameSubmitButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateLiquidGlow(GameSubmitButton, GameInput, true);
    }

    private void GameSubmitButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateLiquidGlow(GameSubmitButton, GameInput, false);
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

            
                } catch (MissingKeyException ex)
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








