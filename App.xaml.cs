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
    private BuddyWindow? _buddyWindow;

    private System.Windows.Threading.DispatcherTimer? _backgroundScheduler;
    public static DateTime? PomodoroEndTime { get; set; }
    public static int PomodoroDurationMinutes { get; set; } = 25;
    public static int PomodoroRemainingPausedSeconds { get; set; } = 25 * 60;
    public static bool IsPomodoroRunning => PomodoroEndTime.HasValue;

    private static System.Threading.Mutex? _singleInstanceMutex;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Only one SumoSnap may run; otherwise a manual launch would fight the boot instance for the hotkey
        _singleInstanceMutex = new System.Threading.Mutex(true, "SumoSnap_SingleInstance_Mutex", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        bool launchedAtBoot = Array.IndexOf(e.Args, "--startup") >= 0;
        StartupManager.Apply(SettingsManager.LoadSettings().RunOnStartup);

        CreateStartMenuShortcut();
        string iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = File.Exists(iconPath) ? new System.Drawing.Icon(iconPath) : System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Visible = true,
            Text = "SumoSnap"
        };
        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("Show Active Chat", null, OnShowChatClicked);
        contextMenu.Items.Add("New Screenshot", null, OnNewScreenshotClicked);
        contextMenu.Items.Add("Toggle Floating Buddy", null, OnToggleBuddyClicked);
        contextMenu.Items.Add("Settings", null, OnSettingsClicked);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add("Quit", null, OnQuitClicked);
        
        _notifyIcon.ContextMenuStrip = contextMenu;

        _buddyWindow = new BuddyWindow(HandleScreenshot);
        if (SettingsManager.LoadSettings().ShowFloatingBuddy)
        {
            _buddyWindow.Show();
        }

        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.OnPrintScreenPressed += HandleScreenshot;

        _backgroundScheduler = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _backgroundScheduler.Tick += BackgroundScheduler_Tick;
        _backgroundScheduler.Start();
        
        var settings = SettingsManager.LoadSettings();
        if (!settings.HasSeenOnboarding)
        {
            var infoWindow = new InfoWindow(isOnboarding: true);
            infoWindow.ShowDialog();
            settings.HasSeenOnboarding = true;
            SettingsManager.SaveSettings(settings);
        }
        
        // Let the user know the app is ready (stay silent when auto-launched at boot)
        if (!launchedAtBoot)
            _notifyIcon.ShowBalloonTip(3000, "SumoSnap", "Ready! Press Ctrl+Shift+Q to capture.", System.Windows.Forms.ToolTipIcon.Info);
    }

    private void BackgroundScheduler_Tick(object? sender, EventArgs e)
    {
        // 1. Check Pomodoro Timer
        if (PomodoroEndTime.HasValue && DateTime.Now >= PomodoroEndTime.Value)
        {
            PomodoroEndTime = null;
            PomodoroRemainingPausedSeconds = PomodoroDurationMinutes * 60;
            NotificationWindow.ShowDarkCard("Pomodoro Timer", $"Your {PomodoroDurationMinutes}-minute focus session is complete.");
            _currentSessionWindow?.SyncPomodoroUI();
        }
        else if (PomodoroEndTime.HasValue)
        {
            _currentSessionWindow?.SyncPomodoroUI();
        }

        // 2. Check Calendar Reminders (every 5 seconds to minimize disk reads)
        if (DateTime.Now.Second % 5 == 0)
        {
            var settings = SettingsManager.LoadSettings();
            if (settings.Reminders != null && settings.Reminders.Count > 0)
            {
                bool changed = false;
                var now = DateTime.Now;
                foreach (var rem in settings.Reminders)
                {
                    if (!rem.Notified && now >= rem.DueDateTime)
                    {
                        rem.Notified = true;
                        changed = true;
                        NotificationWindow.ShowDarkCard("Reminder", rem.Title);
                    }
                }
                if (changed)
                {
                    SettingsManager.SaveSettings(settings);
                    _currentSessionWindow?.RefreshCalendarRemindersList();
                }
            }
        }
    }

    private PostCaptureWindow? _currentSessionWindow;

    private void HandleScreenshot()
    {
        bool buddyWasVisible = false;
        if (_buddyWindow != null && _buddyWindow.IsVisible)
        {
            buddyWasVisible = true;
            _buddyWindow.Hide();
            // Force UI update so it completely disappears before screen freeze
            System.Windows.Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            System.Threading.Thread.Sleep(50); 
        }

        try
        {
            var window = new RegionCaptureWindow();
            if (window.ShowDialog() == true)
            {
                using var bmp = CaptureEngine.CaptureRegion(window.SelectedRegion);
                var imageSource = BitmapToImageSource(bmp);
                
                if (_currentSessionWindow != null)
                {
                    _currentSessionWindow.Close();
                }

                _currentSessionWindow = new PostCaptureWindow(imageSource);
                _currentSessionWindow.Show();
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText("capture_error.log", $"Error during capture: {ex.Message}\n{ex.StackTrace}");
            System.Windows.MessageBox.Show($"Error during capture: {ex.Message}\n{ex.StackTrace}", "Capture Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (buddyWasVisible && _buddyWindow != null)
            {
                _buddyWindow.Show();
            }
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

    private void OnShowChatClicked(object? sender, EventArgs e)
    {
        if (_currentSessionWindow != null)
        {
            _currentSessionWindow.Show();
            _currentSessionWindow.Activate();
        }
        else
        {
            System.Windows.MessageBox.Show("No active chat. Take a screenshot first (Ctrl+Shift+Q).", "SumoSnap", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnNewScreenshotClicked(object? sender, EventArgs e)
    {
        HandleScreenshot();
    }

    private void OnToggleBuddyClicked(object? sender, EventArgs e)
    {
        var settings = SettingsManager.LoadSettings();
        settings.ShowFloatingBuddy = !settings.ShowFloatingBuddy;
        SettingsManager.SaveSettings(settings);

        if (_buddyWindow != null)
        {
            if (settings.ShowFloatingBuddy)
                _buddyWindow.Show();
            else
                _buddyWindow.Hide();
        }
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        new SettingsWindow().ShowDialog();
        SyncBuddyVisibility();
    }

    public void SyncBuddyVisibility()
    {
        var settings = SettingsManager.LoadSettings();
        if (_buddyWindow != null)
        {
            if (settings.ShowFloatingBuddy)
            {
                if (!_buddyWindow.IsVisible) _buddyWindow.Show();
                _buddyWindow.RefreshPulse();
            }
            else
            {
                if (_buddyWindow.IsVisible) _buddyWindow.Hide();
            }
        }
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

    private void CreateStartMenuShortcut()
    {
        try
        {
            string startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "SumoSnap.lnk");
            if (!File.Exists(startMenuPath))
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    object shell = Activator.CreateInstance(shellType)!;
                    object shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { startMenuPath })!;
                    
                    Type shortcutType = shortcut.GetType();
                    shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Environment.ProcessPath! });
                    shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(Environment.ProcessPath)! });
                    shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "AI-Powered Screenshot Utility" });
                    shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
                }
            }
        }
        catch
        {
            // Fail silently if shortcut creation fails
        }
    }
}
