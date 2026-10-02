using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SumoSnap;

public partial class BuddyWindow : Window
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Win32Point { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out Win32Point pt);

    private bool _isDragging = false;
    private System.Windows.Point _clickPosition;
    private readonly Action _onClicked;
    private DispatcherTimer _proximityTimer;

    public BuddyWindow(Action onClicked)
    {
        InitializeComponent();
        _onClicked = onClicked;

        _proximityTimer = new DispatcherTimer();
        _proximityTimer.Interval = TimeSpan.FromMilliseconds(30);
        _proximityTimer.Tick += CheckProximity;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
        this.Left = desktopWorkingArea.Right - this.Width + 10;
        this.Top = desktopWorkingArea.Bottom - this.Height + 5;
        
        _proximityTimer.Start();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        _proximityTimer.Stop();
    }

    private void CheckProximity(object? sender, EventArgs e)
    {
        if (GetCursorPos(out Win32Point mousePos))
        {
            var m = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
            double dpiX = m.HasValue ? m.Value.M11 : 1.0;
            double dpiY = m.HasValue ? m.Value.M22 : 1.0;

            double cursorX = mousePos.X * dpiX;
            double cursorY = mousePos.Y * dpiY;

            double centerX = this.Left + (this.Width / 2);
            double centerY = this.Top + (this.Height / 2);

            double distance = Math.Sqrt(Math.Pow(cursorX - centerX, 2) + Math.Pow(cursorY - centerY, 2));

            double targetOpacity = 0.05; 
            double targetScale = 0.4; 
            
            if (distance < 300) 
            {
                double factor = 1.0 - (distance / 300.0);
                
                targetOpacity = 0.05 + (factor * 0.95);
                targetScale = 0.4 + (factor * 0.4); 
                
                if (targetOpacity > 1.0) targetOpacity = 1.0;
                if (targetScale > 0.8) targetScale = 0.8;
            }

            if (Math.Abs(this.Opacity - targetOpacity) > 0.01)
            {
                this.Opacity += (targetOpacity - this.Opacity) * 0.2;
            }

            if (Math.Abs(BuddyScale.ScaleX - targetScale) > 0.005)
            {
                double newScale = BuddyScale.ScaleX + ((targetScale - BuddyScale.ScaleX) * 0.2);
                BuddyScale.ScaleX = newScale;
                BuddyScale.ScaleY = newScale;
            }
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _clickPosition = e.GetPosition(this);
        this.CaptureMouse();
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (this.IsMouseCaptured)
        {
            System.Windows.Point currentPosition = e.GetPosition(this);
            if (Math.Abs(currentPosition.X - _clickPosition.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(currentPosition.Y - _clickPosition.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isDragging = true;
                this.ReleaseMouseCapture();
                this.DragMove();
            }
        }
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (this.IsMouseCaptured)
        {
            this.ReleaseMouseCapture();
            if (!_isDragging)
            {
                _onClicked?.Invoke();
            }
        }
    }
}
