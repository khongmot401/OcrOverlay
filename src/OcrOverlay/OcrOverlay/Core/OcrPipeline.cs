using System.Drawing;
using System.Windows;
using OcrOverlay.Capture;
using OcrOverlay.Ocr;
using OcrOverlay.Translate;

namespace OcrOverlay.Core;

// ScreenBox is in absolute screen pixels (already offset by region origin).
public record TranslatedLine(Rect ScreenBox, string Original, string Translated);

public record PipelineResult(
    Rectangle Region,
    IReadOnlyList<TranslatedLine> Lines);

public class OcrPipeline
{
    private readonly IScreenCapture _capture;
    private readonly IOcrEngine _ocr;
    private readonly ITranslator _translator;
    private readonly AppSettings _settings;

    public OcrPipeline(IScreenCapture capture, IOcrEngine ocr, ITranslator translator, AppSettings settings)
    {
        _capture = capture;
        _ocr = ocr;
        _translator = translator;
        _settings = settings;
    }

    public async Task<PipelineResult> RunOnceAsync(Rectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            throw new ArgumentException("Region is empty.", nameof(region));

        using var bmp = _capture.CaptureRegion(region);
        var ocrLines = await _ocr.RecognizeAsync(bmp, _settings.SourceLanguage);

        // Translate each line individually so we can place it on top of the original.
        // TranslationCache inside the translator dedupes repeated phrases.
        var tasks = ocrLines.Select(async l =>
        {
            var translated = string.IsNullOrWhiteSpace(l.Text)
                ? string.Empty
                : await _translator.TranslateAsync(l.Text, _settings.SourceLanguage, _settings.TargetLanguage);
            var screenBox = new Rect(
                region.X + l.BoundingBox.X,
                region.Y + l.BoundingBox.Y,
                l.BoundingBox.Width,
                l.BoundingBox.Height);
            return new TranslatedLine(screenBox, l.Text, translated);
        });

        var results = await Task.WhenAll(tasks);
        return new PipelineResult(region, results);
    }
}
