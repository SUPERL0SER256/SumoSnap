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
    private BitmapSource _originalImage;
    private BitmapSource _currentImage;

    public PostCaptureWindow(BitmapSource capturedImage)
    {
        InitializeComponent();
        ThemeManager.ApplyDarkTitleBar(this);
        
        _originalImage = capturedImage;
        _currentImage = capturedImage;
        PreviewImage.Source = _currentImage;
    }

    private void UpdateImage(BitmapSource newImage)
    {
        _currentImage = newImage;
        PreviewImage.Source = _currentImage;
        UndoButton.IsEnabled = (_currentImage != _originalImage);
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateImage(_originalImage);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataObject = new System.Windows.DataObject();
            
            // 1. Standard DIB format for legacy apps (Word, Paint, etc)
            dataObject.SetImage(_currentImage);

            // 2. Explicit PNG format for modern apps that support transparency (Discord, Chrome, Slack, etc)
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

    private async void EnhanceButton_Click(object sender, RoutedEventArgs e)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        
        try
        {
            var aiClient = new AiClient();
            var newImage = await aiClient.EnhanceAsync(_currentImage);
            UpdateImage(newImage);
        }
        catch (AiClient.MissingKeyException ex)
        {
            var result = System.Windows.MessageBox.Show($"Please enter your {ex.Message} API key in Settings.", "Missing API Key", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                new SettingsWindow().ShowDialog();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "AI Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void ReframeButton_Click(object sender, RoutedEventArgs e)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        
        try
        {
            var aiClient = new AiClient();
            var newImage = await aiClient.ReframeAsync(_currentImage);
            UpdateImage(newImage);
        }
        catch (AiClient.MissingKeyException ex)
        {
            var result = System.Windows.MessageBox.Show($"Please enter your {ex.Message} API key in Settings.", "Missing API Key", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                new SettingsWindow().ShowDialog();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "AI Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void RemoveBgButton_Click(object sender, RoutedEventArgs e)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        
        try
        {
            var aiClient = new AiClient();
            var newImage = await aiClient.RemoveBackgroundAsync(_currentImage);
            UpdateImage(newImage);
        }
        catch (AiClient.MissingKeyException ex)
        {
            var result = System.Windows.MessageBox.Show($"Please enter your {ex.Message} API key in Settings.", "Missing API Key", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                new SettingsWindow().ShowDialog();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "AI Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    // ========== Gemini Chat ==========

    private void AskGeminiButton_Click(object sender, RoutedEventArgs e)
    {
        ChatPanel.Visibility = Visibility.Visible;
        ChatColumn.Width = new GridLength(320);
        ChatInput.Focus();
    }

    private void CloseChatButton_Click(object sender, RoutedEventArgs e)
    {
        ChatPanel.Visibility = Visibility.Collapsed;
        ChatColumn.Width = new GridLength(0);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow().ShowDialog();
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

        // Add user message bubble
        AddChatBubble(userMessage, isUser: true);
        ChatInput.Text = "";
        SendButton.IsEnabled = false;

        try
        {
            var gemini = new GeminiClient();
            string response = await gemini.ChatWithImageAsync(_currentImage, userMessage);
            AddChatBubble(response, isUser: false);
        }
        catch (AiClient.MissingKeyException ex)
        {
            var result = System.Windows.MessageBox.Show($"Please enter your {ex.Message} API key in Settings.", "Missing API Key", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                new SettingsWindow().ShowDialog();
            }
        }
        catch (Exception ex)
        {
            AddChatBubble($"Error: {ex.Message}", isUser: false);
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    private void AddChatBubble(string text, bool isUser)
    {
        var userColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3A3A3A");
        var aiColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2A2A");

        var bubble = new Border
        {
            Background = new SolidColorBrush(isUser ? userColor : aiColor),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(isUser ? 30 : 0, 4, isUser ? 0 : 30, 4),
            HorizontalAlignment = isUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left
        };

        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 240
        };

        bubble.Child = textBlock;
        ChatMessages.Children.Add(bubble);
        ChatScrollViewer.ScrollToEnd();
    }
}
