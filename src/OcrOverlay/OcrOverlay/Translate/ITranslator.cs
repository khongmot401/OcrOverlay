namespace OcrOverlay.Translate;

public interface ITranslator
{
    Task<string> TranslateAsync(string text, string sourceLang, string targetLang);
}
