using System.Drawing;
using System.Drawing.Imaging;

namespace OcrOverlay.Capture;

public class ScreenCapture : IScreenCapture
{
    public Bitmap CaptureRegion(Rectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            throw new ArgumentException("Region must have positive dimensions.", nameof(region));

        var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(region.Location, Point.Empty, region.Size, CopyPixelOperation.SourceCopy);
        return bitmap;
    }
}
