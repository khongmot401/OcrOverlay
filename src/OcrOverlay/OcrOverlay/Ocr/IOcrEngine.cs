using System.Drawing;

namespace OcrOverlay.Ocr;

public interface IOcrEngine
{
    Task<IReadOnlyList<OcrLine>> RecognizeAsync(Bitmap bitmap, string language);
}
