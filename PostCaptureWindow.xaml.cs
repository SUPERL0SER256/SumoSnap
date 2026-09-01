using System;
using System.IO;
using System.Windows;
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
        System.Windows.Clipboard.SetImage(_currentImage);
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
}
