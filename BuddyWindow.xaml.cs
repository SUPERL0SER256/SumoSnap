using System;
using System.Windows;
using System.Windows.Input;

namespace SumoSnap;

public partial class BuddyWindow : Window
{
    private bool _isDragging = false;
    private System.Windows.Point _clickPosition;
    private readonly Action _onClicked;

    public BuddyWindow(Action onClicked)
    {
        InitializeComponent();
        _onClicked = onClicked;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Position at bottom right, just above the taskbar
        var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
        this.Left = desktopWorkingArea.Right - this.Width - 30;
        this.Top = desktopWorkingArea.Bottom - this.Height - 30;
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        this.Opacity = 1.0;
        this.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        this.Opacity = 0.5;
        this.Cursor = System.Windows.Input.Cursors.Arrow;
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
