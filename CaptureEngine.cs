using System.Drawing;
using System.Windows.Forms;

namespace SumoSnap;

public static class CaptureEngine
{
    public static Bitmap CaptureFullScreen()
    {
        var bounds = Screen.PrimaryScreen.Bounds;
        return CaptureRegion(new System.Windows.Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }

    public static Bitmap CaptureRegion(System.Windows.Rect region)
    {
        var bitmap = new Bitmap((int)region.Width, (int)region.Height);
        
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen((int)region.X, (int)region.Y, 0, 0, new Size((int)region.Width, (int)region.Height));
        }
        
        return bitmap;
    }
}
