# OcrOverlay

Windows desktop tool: capture screen → OCR → translate → overlay translated text on top of the original.

## Features

- **Region or full-screen** capture with one hotkey
- **Auto-detect** source language via Windows built-in OCR (no Tesseract needed)
- **Sidebar mode** for manga / vertical text — shows translation beside the original instead of on top
- **Merge nearby lines** automatically to prevent overlapping boxes
- **Customizable hotkeys** — rebind directly in the app
- **System tray** — minimize and keep hotkeys active in the background
- Supports 10 languages: English, Vietnamese, Chinese, Japanese, Korean, French, German, Spanish, Russian

## Quick start

### Requirements

- Windows 10 (build 19041+) or Windows 11
- At least one Windows OCR language pack installed
  - Settings → Time & Language → Language → Add a language → install the language(s) you need

### Usage

1. Download `OcrOverlay.exe` from [Releases](../../releases) (single-file, no install needed)
2. Run the exe — see [Windows security warnings](#windows-security-warnings-on-first-launch) if prompted
3. Use the default hotkeys:

| Hotkey | Action |
|---|---|
| `Ctrl+Shift+R` | Select a region and translate |
| `Ctrl+Shift+T` | Translate the full screen |
| `ESC` or click | Dismiss the overlay |

### Main window controls

| Control | Description |
|---|---|
| **Provider** | Translation provider (`Google` by default) |
| **Source / Target** | Source language (`Auto detect` or specific) and target language |
| **Sidebar mode** | Show translation beside original text (useful for manga / vertical text) |
| **Hotkey boxes** | Click a box then press a key combo to rebind |

### Configuration

Edit `appsettings.json` (next to the exe) for persistent defaults:

```json
{
  "SourceLanguage": "en",
  "TargetLanguage": "vi",
  "HotkeyRegion": "Ctrl+Shift+R",
  "HotkeyFullScreen": "Ctrl+Shift+T",
  "SidebarMode": true,
  "OverlayMinWidth": 200
}
```

## Sidebar mode (for manga / vertical text)

When reading comics or manga with vertical or densely packed text, the default overlay can obscure the original. Enable **Sidebar mode** to:

- Show translated text **beside** the original instead of on top
- Apply a minimum width (`OverlayMinWidth`, default 160px) so narrow columns are still readable
- Enable text wrapping inside the translation box
- Automatically **merge nearby lines** into a single box to prevent overlapping

Toggle via the checkbox in the main window or in `appsettings.json`.

## Windows security warnings on first launch

The app is not code-signed, so Windows may show warnings on first run.

### SmartScreen ("Windows protected your PC")

1. Click **"More info"**
2. Click **"Run anyway"**

This only happens once — Windows remembers your choice.

### "Unknown publisher"

Click **"Yes"**. The app runs as a normal user and does not require admin privileges.

### Antivirus false positive

Some antivirus may flag self-contained .NET executables. If blocked:

1. **Windows Security** → Virus & threat protection → Protection history → **Allow on device**
2. Or add the app folder to **Exclusions**

## Known issues

- The Google Translate endpoint is rate-limited — suitable for personal use, not heavy production traffic.
- "Auto detect" OCR tries all installed language packs; install only what you need to keep it fast.

---

## Development

### Stack

- .NET 10 + WPF + WinForms interop
- Windows.Media.Ocr (built-in, no Tesseract)
- Google Translate (public endpoint, no API key)

### Project layout

```
src/OcrOverlay/
├── OcrOverlay.slnx
└── OcrOverlay/
    ├── OcrOverlay.csproj
    ├── app.manifest                # asInvoker
    ├── appsettings.json
    ├── Capture/                    # Graphics.CopyFromScreen
    ├── Core/                       # OcrPipeline, AppSettings
    ├── Hotkey/                     # Global hotkey parsing & registration
    ├── Interop/                    # P/Invoke (RegisterHotKey)
    ├── Ocr/                        # Windows.Media.Ocr wrapper, auto language
    ├── Overlay/                    # RegionSelectorWindow, OverlayWindow
    ├── Resources/                  # Runtime-generated app icon
    └── Translate/                  # GoogleTranslator, LibreTranslator, cache
```

### Prerequisites (dev)

- Windows 10 (build 19041+) or Windows 11
- .NET 10 SDK — verify with `dotnet --version`
- At least one Windows OCR language pack

### Run

```bash
cd src/OcrOverlay/OcrOverlay
dotnet run
```

### Build & publish

```bash
# Debug build
cd src/OcrOverlay
dotnet build

# Self-contained single-file (recommended for distribution)
cd src/OcrOverlay/OcrOverlay
dotnet publish -c Release -r win-x64 --self-contained=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Output: `bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/OcrOverlay.exe`

Optional flags:
- `-p:EnableCompressionInSingleFile=true` — ~30% smaller exe
- `-p:DebugType=None -p:DebugSymbols=false` — strip .pdb

### Technical notes

- DPI scaling: `PerMonitorV2` + `TransformFromDevice` for multi-monitor support
- Overlay covers entire virtual screen for pixel-perfect positioning
- `TranslationCache` deduplicates repeated phrases (in-memory, lost on exit)
- App is portable — no installer, no registry, no UAC elevation

## Roadmap

- Real-time loop with diff detection and click-through overlay
- Persistent settings (save changes back to JSON on edit)
