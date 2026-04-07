using System.Drawing;

namespace OcrOverlay.Capture;

public interface IScreenCapture
{
    Bitmap CaptureRegion(Rectangle region);
}
