using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace AIScreenshotUtility;

public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int HOTKEY_ID_REGION  = 9001;
    private const int HOTKEY_ID_ALT     = 9002;
    private const uint MOD_NONE         = 0x0000;
    private const uint MOD_CONTROL      = 0x0002;
    private const uint MOD_SHIFT        = 0x0004;
    private const uint VK_SNAPSHOT      = 0x2C; // Print Screen
    private const uint VK_Q             = 0x51; // Q key
    private const int WM_HOTKEY         = 0x0312;

    public event Action? OnPrintScreenPressed;

    private bool _disposed = false;

    public HotkeyManager()
    {
        // ComponentDispatcher is the WPF-native way to hook into the application
        // message pump — no fragile hidden window needed.
        ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;

        // Try plain Print Screen first; fall back to Ctrl+PrtSc if unavailable.
        bool ok = RegisterHotKey(IntPtr.Zero, HOTKEY_ID_REGION, MOD_NONE, VK_SNAPSHOT);
        if (!ok)
        {
            RegisterHotKey(IntPtr.Zero, HOTKEY_ID_REGION, MOD_CONTROL, VK_SNAPSHOT);
        }

        // Bulletproof backup hotkey: Ctrl + Shift + Q
        RegisterHotKey(IntPtr.Zero, HOTKEY_ID_ALT, MOD_CONTROL | MOD_SHIFT, VK_Q);
    }

    private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
    {
        if (msg.message == WM_HOTKEY && ((int)msg.wParam == HOTKEY_ID_REGION || (int)msg.wParam == HOTKEY_ID_ALT))
        {
            handled = true;
            OnPrintScreenPressed?.Invoke();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
            UnregisterHotKey(IntPtr.Zero, HOTKEY_ID_REGION);
            UnregisterHotKey(IntPtr.Zero, HOTKEY_ID_ALT);
        }
    }
}
