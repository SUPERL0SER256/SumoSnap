using System.Configuration;
using System.Data;
using System.Windows;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace SumoSnap;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private HotkeyManager? _hotkeyManager;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon("icon.ico"),
            Visible = true,
            Text = "SumoSnap"
        };
        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("New Screenshot", null, OnNewScreenshotClicked);
        contextMenu.Items.Add("Settings", null, OnSettingsClicked);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add("Quit", null, OnQuitClicked);
        
        _notifyIcon.ContextMenuStrip = contextMenu;

        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.OnPrintScreenPressed += HandleScreenshot;

        // Let the user know the app is ready
        _notifyIcon.ShowBalloonTip(3000, "SumoSnap", "Ready! Press Ctrl+Shift+Q to capture.", System.Windows.Forms.ToolTipIcon.Info);
    }

    private void HandleScreenshot()
    {
        try
        {
            var window = new RegionCaptureWindow();
            if (window.ShowDialog() == true)
            {
                using var bmp = CaptureEngine.CaptureRegion(window.SelectedRegion);
                var imageSource = BitmapToImageSource(bmp);
                
                var postCaptureWindow = new PostCaptureWindow(imageSource);
                postCaptureWindow.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error during capture: {ex.Message}\n{ex.StackTrace}", "Capture Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private BitmapImage BitmapToImageSource(Bitmap bitmap)
    {
        using (MemoryStream memory = new MemoryStream())
        {
            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
            memory.Position = 0;
            BitmapImage bitmapimage = new BitmapImage();
            bitmapimage.BeginInit();
            bitmapimage.StreamSource = memory;
            bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapimage.EndInit();
            return bitmapimage;
        }
    }

    private void OnNewScreenshotClicked(object? sender, EventArgs e)
    {
        HandleScreenshot();
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        new SettingsWindow().ShowDialog();
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        _hotkeyManager?.Dispose();
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        Current.Shutdown();
    }
}
