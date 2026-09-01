using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SumoSnap;

public partial class RegionCaptureWindow : Window
{
    private bool _isDragging = false;
    private System.Windows.Point _startPoint;
    public Rect SelectedRegion { get; private set; }

    public RegionCaptureWindow()
    {
        InitializeComponent();
        Loaded += (s, e) => Focus();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDragging = true;
            _startPoint = e.GetPosition(CaptureCanvas);
            SelectionRectangle.Visibility = Visibility.Visible;
            CaptureCanvas.CaptureMouse();
        }
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging)
        {
            var currentPoint = e.GetPosition(CaptureCanvas);
            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            var width = Math.Max(_startPoint.X, currentPoint.X) - x;
            var height = Math.Max(_startPoint.Y, currentPoint.Y) - y;

            Canvas.SetLeft(SelectionRectangle, x);
            Canvas.SetTop(SelectionRectangle, y);
            SelectionRectangle.Width = width;
            SelectionRectangle.Height = height;
        }
    }

    private void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            CaptureCanvas.ReleaseMouseCapture();

            var currentPoint = e.GetPosition(CaptureCanvas);
            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            var width = Math.Max(_startPoint.X, currentPoint.X) - x;
            var height = Math.Max(_startPoint.Y, currentPoint.Y) - y;

            if (width > 5 && height > 5)
            {
                // Convert WPF logical coordinates (DIPs) to physical screen pixels for System.Drawing
                var topLeftPhysical = CaptureCanvas.PointToScreen(new System.Windows.Point(x, y));
                var bottomRightPhysical = CaptureCanvas.PointToScreen(new System.Windows.Point(x + width, y + height));
                
                var physicalWidth = bottomRightPhysical.X - topLeftPhysical.X;
                var physicalHeight = bottomRightPhysical.Y - topLeftPhysical.Y;

                SelectedRegion = new Rect(topLeftPhysical.X, topLeftPhysical.Y, physicalWidth, physicalHeight);
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
            Close();
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
