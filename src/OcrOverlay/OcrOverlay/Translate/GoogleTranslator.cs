using System.Net.Http;
using System.Text.Json;

namespace OcrOverlay.Translate;

// Uses Google Translate's unofficial public endpoint (no API key).
// Suitable for development/personal use; not for production traffic.
public class GoogleTranslator : ITranslator
{
    private const string Endpoint = "https://translate.googleapis.com/translate_a/single";
    private readonly HttpClient _http;
    private readonly TranslationCache _cache;

    public GoogleTranslator(TranslationCache cache, HttpClient? http = null)
    {
        _cache = cache;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var key = $"{sourceLang}|{targetLang}|{text}";
        if (_cache.TryGet(key, out var cached) && cached is not null)
            return cached;

        var url = $"{Endpoint}?client=gtx&sl={Uri.EscapeDataString(sourceLang)}&tl={Uri.EscapeDataString(targetLang)}&dt=t&q={Uri.EscapeDataString(text)}";
        using var resp = await _http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();

        // Response shape: [[["translated","original",null,null,1],...],null,"en",...]
        using var doc = JsonDocument.Parse(json);
        var sb = new System.Text.StringBuilder();
        foreach (var seg in doc.RootElement[0].EnumerateArray())
        {
            if (seg.GetArrayLength() > 0 && seg[0].ValueKind == JsonValueKind.String)
                sb.Append(seg[0].GetString());
        }
        var translated = sb.ToString();

        _cache.Set(key, translated);
        return translated;
    }
}
