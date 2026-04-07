namespace OcrOverlay.Core;

public class AppSettings
{
    public string SourceLanguage { get; set; } = "auto";
    public string TargetLanguage { get; set; } = "vi";
    public string HotkeyRegion { get; set; } = "Ctrl+Shift+R";
    public string HotkeyFullScreen { get; set; } = "Ctrl+Shift+T";
}
