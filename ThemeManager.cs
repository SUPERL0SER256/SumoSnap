using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SumoSnap;

public static class ThemeManager
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static void ApplyDarkTitleBar(Window window)
    {
        var helper = new WindowInteropHelper(window);
        IntPtr hwnd = helper.Handle;
        
        // If the window hasn't been drawn yet, wait for it
        if (hwnd == IntPtr.Zero)
        {
            window.SourceInitialized += (s, e) => ApplyDarkTitleBar(window);
            return;
        }

        int useImmersiveDarkMode = 1;
        
        // Try Windows 11 API first
        int result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
        
        if (result != 0)
        {
            // Fallback for older Windows 10 versions
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useImmersiveDarkMode, sizeof(int));
        }
    }
}
