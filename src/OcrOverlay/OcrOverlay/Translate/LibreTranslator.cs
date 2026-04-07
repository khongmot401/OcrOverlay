using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace OcrOverlay.Translate;

public class LibreTranslator : ITranslator
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string? _apiKey;
    private readonly TranslationCache _cache;

    public LibreTranslator(string endpoint, string? apiKey, TranslationCache cache, HttpClient? http = null)
    {
        _endpoint = endpoint;
        _apiKey = apiKey;
        _cache = cache;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var key = $"{sourceLang}|{targetLang}|{text}";
        if (_cache.TryGet(key, out var cached) && cached is not null)
            return cached;

        var payload = new TranslateRequest(text, sourceLang, targetLang, "text", _apiKey);
        using var resp = await _http.PostAsJsonAsync(_endpoint, payload);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<TranslateResponse>()
                     ?? throw new InvalidOperationException("Empty translate response");

        _cache.Set(key, result.TranslatedText);
        return result.TranslatedText;
    }

    private record TranslateRequest(
        [property: JsonPropertyName("q")] string Q,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("target")] string Target,
        [property: JsonPropertyName("format")] string Format,
        [property: JsonPropertyName("api_key")] string? ApiKey);

    private record TranslateResponse(
        [property: JsonPropertyName("translatedText")] string TranslatedText);
}
