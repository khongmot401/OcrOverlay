using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinOcr = Windows.Media.Ocr;

namespace OcrOverlay.Ocr;

public class WindowsOcrEngine : IOcrEngine
{
    public async Task<IReadOnlyList<OcrLine>> RecognizeAsync(Bitmap bitmap, string language)
    {
        var softwareBitmap = await ConvertToSoftwareBitmapAsync(bitmap);

        if (string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase))
            return await RecognizeAutoAsync(softwareBitmap);

        var engine = WinOcr.OcrEngine.TryCreateFromLanguage(new Language(language))
                     ?? WinOcr.OcrEngine.TryCreateFromUserProfileLanguages()
                     ?? throw new InvalidOperationException(
                         $"No OCR engine available for '{language}'. Install the language pack in Windows Settings.");

        var result = await engine.RecognizeAsync(softwareBitmap);
        return ToLines(result);
    }

    // Try every installed OCR language; return the result with the most recognized words.
    private static async Task<IReadOnlyList<OcrLine>> RecognizeAutoAsync(SoftwareBitmap bitmap)
    {
        var languages = WinOcr.OcrEngine.AvailableRecognizerLanguages;
        if (languages.Count == 0)
            throw new InvalidOperationException("No OCR language packs installed.");

        IReadOnlyList<OcrLine> best = Array.Empty<OcrLine>();
        int bestWordCount = -1;

        foreach (var lang in languages)
        {
            var engine = WinOcr.OcrEngine.TryCreateFromLanguage(lang);
            if (engine is null) continue;

            var result = await engine.RecognizeAsync(bitmap);
            var wordCount = result.Lines.Sum(l => l.Words.Count);
            if (wordCount > bestWordCount)
            {
                bestWordCount = wordCount;
                best = ToLines(result);
            }
        }
        return best;
    }

    private static List<OcrLine> ToLines(WinOcr.OcrResult result)
    {
        var lines = new List<OcrLine>(result.Lines.Count);
        foreach (var line in result.Lines)
        {
            Rect box = Rect.Empty;
            foreach (var word in line.Words)
            {
                var r = word.BoundingRect;
                var wordRect = new Rect(r.X, r.Y, r.Width, r.Height);
                box = box.IsEmpty ? wordRect : Rect.Union(box, wordRect);
            }
            lines.Add(new OcrLine(line.Text, box));
        }
        return lines;
    }

    private static async Task<SoftwareBitmap> ConvertToSoftwareBitmapAsync(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Bmp);
        ms.Position = 0;

        using var randomAccessStream = new InMemoryRandomAccessStream();
        await randomAccessStream.WriteAsync(ms.ToArray().AsBuffer());
        randomAccessStream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        return await decoder.GetSoftwareBitmapAsync();
    }
}
