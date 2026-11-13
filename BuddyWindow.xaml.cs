using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SumoSnap;

public partial class BuddyWindow : Window
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Win32Point { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out Win32Point pt);

    private readonly Action _onCaptureRequested;
    private readonly Action<string>? _onOpenModuleRequested;
    private readonly Action? _onSettingsRequested;

    private readonly DispatcherTimer _collapseDelayTimer;
    private readonly DispatcherTimer _fluidMotionTimer;

    private bool _dockedRight = true;
    private bool _isExpanded = false;
    private bool _isMouseDownForDrag = false;
    private bool _isDragging = false;
    private bool _isGlidingToEdge = false;

    private System.Windows.Point _mouseDownScreenPos;
    private System.Windows.Point _dragCursorOffset;
    private double _targetWindowLeft;
    private double _targetWindowTop;

    private const double CollapsedWidth = 5.0;
    private const double CollapsedHeight = 48.0;
    private const double ExpandedWidth = 40.0;
    private const double ExpandedHeight = 216.0;

    public BuddyWindow(
        Action onCaptureRequested,
        Action<string>? onOpenModuleRequested = null,
        Action? onSettingsRequested = null)
    {
        InitializeComponent();
        _onCaptureRequested = onCaptureRequested;
        _onOpenModuleRequested = onOpenModuleRequested;
        _onSettingsRequested = onSettingsRequested;

        _collapseDelayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(160)
        };
        _collapseDelayTimer.Tick += CollapseDelayTimer_Tick;

        _fluidMotionTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(14)
        };
        _fluidMotionTimer.Tick += FluidMotionTimer_Tick;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        _dockedRight = true;
        ApplyDockOrientation(dockRight: true);

        this.Left = workArea.Right - this.Width;
        this.Top = Math.Round(workArea.Top + ((workArea.Height - this.Height) * 0.5));
        _targetWindowLeft = this.Left;
        _targetWindowTop = this.Top;

        NotchChassis.Width = CollapsedWidth;
        NotchChassis.Height = CollapsedHeight;
        ButtonsStack.Opacity = 0;
        ButtonsStack.IsHitTestVisible = false;
        _isExpanded = false;
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        _collapseDelayTimer.Stop();
        _fluidMotionTimer.Stop();
    }

    public void RefreshPulse()
    {
        // Pure black monochrome notch
    }

    private void ApplyDockOrientation(bool dockRight)
    {
        _dockedRight = dockRight;
        if (_dockedRight)
        {
            NotchChassis.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            NotchChassis.CornerRadius = new CornerRadius(5, 0, 0, 5);
            NotchChassis.BorderThickness = new Thickness(1, 1, 0, 1);
            NotchChassis.Margin = new Thickness(0, 0, -1, 0);
        }
        else
        {
            NotchChassis.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            NotchChassis.CornerRadius = new CornerRadius(0, 5, 5, 0);
            NotchChassis.BorderThickness = new Thickness(0, 1, 1, 1);
            NotchChassis.Margin = new Thickness(-1, 0, 0, 0);
        }
    }

    private void AnimateNotchExpansion(bool expand)
    {
        if (_isExpanded == expand) return;
        _isExpanded = expand;

        if (expand)
        {
            HardwareAudio.PlayWheelTick(zoomIn: true);
            ButtonsStack.IsHitTestVisible = true;
        }
        else
        {
            ButtonsStack.IsHitTestVisible = false;
        }

        var dur = TimeSpan.FromMilliseconds(expand ? 190 : 210);
        var ease = new CubicEase
        {
            EasingMode = expand ? EasingMode.EaseOut : EasingMode.EaseInOut
        };

        var widthAnim = new DoubleAnimation
        {
            To = expand ? ExpandedWidth : CollapsedWidth,
            Duration = dur,
            EasingFunction = ease
        };

        var heightAnim = new DoubleAnimation
        {
            To = expand ? ExpandedHeight : CollapsedHeight,
            Duration = dur,
            EasingFunction = ease
        };

        var opacityAnim = new DoubleAnimation
        {
            To = expand ? 1.0 : 0.0,
            Duration = TimeSpan.FromMilliseconds(expand ? 170 : 110),
            EasingFunction = ease
        };

        NotchChassis.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
        NotchChassis.BeginAnimation(FrameworkElement.HeightProperty, heightAnim);
        ButtonsStack.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
    }

    private void NotchChassis_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _collapseDelayTimer.Stop();
        AnimateNotchExpansion(expand: true);
    }

    private void NotchChassis_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging || _isMouseDownForDrag) return;
        _collapseDelayTimer.Stop();
        _collapseDelayTimer.Start();
    }

    private void CollapseDelayTimer_Tick(object? sender, EventArgs e)
    {
        _collapseDelayTimer.Stop();
        if (!_isDragging && !_isMouseDownForDrag && !NotchChassis.IsMouseOver)
        {
            AnimateNotchExpansion(expand: false);
        }
    }

    private System.Windows.Point GetDpiAdjustedCursorPosition()
    {
        if (GetCursorPos(out Win32Point pt))
        {
            var source = PresentationSource.FromVisual(this);
            var transform = source?.CompositionTarget?.TransformFromDevice;
            double dpiX = transform.HasValue ? transform.Value.M11 : 1.0;
            double dpiY = transform.HasValue ? transform.Value.M22 : 1.0;
            return new System.Windows.Point(pt.X * dpiX, pt.Y * dpiY);
        }
        return new System.Windows.Point(this.Left, this.Top);
    }

    private void NotchChassis_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep && IsInsideButton(dep))
            return;

        _collapseDelayTimer.Stop();
        _isMouseDownForDrag = true;
        _isDragging = false;
        _isGlidingToEdge = false;

        _mouseDownScreenPos = GetDpiAdjustedCursorPosition();
        _dragCursorOffset = new System.Windows.Point(
            _mouseDownScreenPos.X - this.Left,
            _mouseDownScreenPos.Y - this.Top);

        NotchChassis.CaptureMouse();
        e.Handled = true;
    }

    private void NotchChassis_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isMouseDownForDrag) return;

        var screenPos = GetDpiAdjustedCursorPosition();
        double dx = screenPos.X - _mouseDownScreenPos.X;
        double dy = screenPos.Y - _mouseDownScreenPos.Y;

        if (!_isDragging && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4))
        {
            _isDragging = true;
            AnimateNotchExpansion(expand: true);
            if (!_fluidMotionTimer.IsEnabled)
                _fluidMotionTimer.Start();
        }

        if (_isDragging)
        {
            var workArea = SystemParameters.WorkArea;
            _targetWindowLeft = Math.Clamp(screenPos.X - _dragCursorOffset.X, workArea.Left - 10, workArea.Right - this.Width + 10);
            _targetWindowTop = Math.Clamp(screenPos.Y - _dragCursorOffset.Y, workArea.Top + 10, workArea.Bottom - this.Height - 10);
        }
    }

    private void NotchChassis_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMouseDownForDrag) return;

        _isMouseDownForDrag = false;
        if (NotchChassis.IsMouseCaptured)
            NotchChassis.ReleaseMouseCapture();

        if (_isDragging)
        {
            _isDragging = false;
            SnapToNearestScreenEdgeFluidly();
        }
        e.Handled = true;
    }

    private void SnapToNearestScreenEdgeFluidly()
    {
        var workArea = SystemParameters.WorkArea;
        double windowCenterX = this.Left + (this.Width / 2.0);
        double screenCenterX = workArea.Left + (workArea.Width / 2.0);

        bool shouldDockRight = windowCenterX >= screenCenterX;
        if (shouldDockRight != _dockedRight)
        {
            ApplyDockOrientation(shouldDockRight);
        }

        _targetWindowLeft = shouldDockRight
            ? workArea.Right - this.Width
            : workArea.Left;
        _targetWindowTop = Math.Clamp(this.Top, workArea.Top + 16, workArea.Bottom - this.Height - 16);

        _isGlidingToEdge = true;
        if (!_fluidMotionTimer.IsEnabled)
            _fluidMotionTimer.Start();
    }

    private void FluidMotionTimer_Tick(object? sender, EventArgs e)
    {
        double lerpFactor = _isDragging ? 0.42 : 0.25;

        double diffX = _targetWindowLeft - this.Left;
        double diffY = _targetWindowTop - this.Top;

        if (Math.Abs(diffX) > 0.4 || Math.Abs(diffY) > 0.4)
        {
            this.Left += diffX * lerpFactor;
            this.Top += diffY * lerpFactor;
        }
        else
        {
            this.Left = _targetWindowLeft;
            this.Top = _targetWindowTop;

            if (_isGlidingToEdge)
            {
                _isGlidingToEdge = false;
                _fluidMotionTimer.Stop();
                HardwareAudio.PlayWheelTick(zoomIn: false);

                if (!NotchChassis.IsMouseOver)
                {
                    _collapseDelayTimer.Stop();
                    _collapseDelayTimer.Start();
                }
            }
        }
    }

    private static bool IsInsideButton(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is System.Windows.Controls.Button)
                return true;
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private void CollapseImmediately()
    {
        _collapseDelayTimer.Stop();
        AnimateNotchExpansion(expand: false);
    }

    private void SnapButton_Click(object sender, RoutedEventArgs e)
    {
        HardwareAudio.PlayKeyClick();
        CollapseImmediately();
        _onCaptureRequested?.Invoke();
    }

    private void LogicButton_Click(object sender, RoutedEventArgs e)
    {
        HardwareAudio.PlayKeyClick();
        CollapseImmediately();
        _onOpenModuleRequested?.Invoke("Logic");
    }

    private void SudokuButton_Click(object sender, RoutedEventArgs e)
    {
        HardwareAudio.PlayKeyClick();
        CollapseImmediately();
        _onOpenModuleRequested?.Invoke("Sudoku");
    }

    private void PomodoroButton_Click(object sender, RoutedEventArgs e)
    {
        HardwareAudio.PlayKeyClick();
        CollapseImmediately();
        _onOpenModuleRequested?.Invoke("Pomodoro");
    }

    private void CalendarButton_Click(object sender, RoutedEventArgs e)
    {
        HardwareAudio.PlayKeyClick();
        CollapseImmediately();
        _onOpenModuleRequested?.Invoke("Calendar");
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        HardwareAudio.PlayKeyClick();
        CollapseImmediately();
        _onSettingsRequested?.Invoke();
    }
}
