# OcrOverlay

Windows desktop tool: capture screen → OCR → translate → overlay translated text on top of the original.

## Stack

- **.NET 10** (LTS) + **WPF** + WinForms interop
- **Windows.Media.Ocr** — built-in, no Tesseract required
- **Translator**: Google (unofficial public endpoint, no key)
- TFM: `net10.0-windows10.0.19041.0`

## Project layout

```
src/OcrOverlay/
├── OcrOverlay.sln
└── OcrOverlay/
    ├── OcrOverlay.csproj
    ├── app.manifest                # asInvoker
    ├── appsettings.json
    ├── Capture/                    # Graphics.CopyFromScreen
    ├── Ocr/                        # Windows.Media.Ocr wrapper, auto language
    ├── Translate/                  # GoogleTranslator, cache
    ├── Hotkey/                     # Global hotkey (planned)
    ├── Overlay/                    # RegionSelectorWindow, OverlayWindow
    ├── Core/                       # OcrPipeline, AppSettings
    └── Interop/                    # P/Invoke
```

See [plan_csharp.md](plan_csharp.md) for the full roadmap and [DEVLOG.md](DEVLOG.md) for change history.

## Prerequisites

- Windows 10 (build 19041+) or Windows 11
- **.NET 10 SDK** — verify with `dotnet --version` (should report `10.x`)
- At least one Windows OCR language pack installed
  - Settings → Time & Language → Language → Add a language → install the language(s) you need
  - For "Auto detect" to work well, install only the languages you actually use

## Develop

```bash
cd src/OcrOverlay/OcrOverlay
dotnet run
```

The dev MainWindow exposes test buttons:

| Button | What it does |
|---|---|
| Test capture | Saves a full primary-screen PNG to `%TEMP%` |
| Test OCR | Captures + recognizes the full screen, lists detected lines |
| Select region & translate | Hides main window, lets you drag a region, overlays the translation in place |
| Translate full screen | Minimizes to taskbar, captures the whole primary screen, overlays the translation |

Top of the window:
- **Provider** — `Google` or `SangTacViet`
- **Source** — `Auto detect` or a specific language
- **Target** — translation target language

## Build

Debug build:
```bash
cd src/OcrOverlay
dotnet build
```

## Release

There are three flavors. Pick one based on how the app will be distributed.

### 1. Single-file self-contained (recommended for end users)

One `.exe` that runs on any Windows 10/11 x64 machine **without** installing the .NET runtime. Larger file (~70–90 MB).

```bash
cd src/OcrOverlay/OcrOverlay
dotnet publish -c Release -r win-x64 --self-contained=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Output: `bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/OcrOverlay.exe`

Optional flags:
- `-p:EnableCompressionInSingleFile=true` — shrink the exe (~30 % smaller, slightly slower first launch)
- `-p:DebugType=None -p:DebugSymbols=false` — strip the `.pdb` file from output

### 2. Framework-dependent (smallest, requires .NET 10 Desktop Runtime on target)

```bash
cd src/OcrOverlay/OcrOverlay
dotnet publish -c Release -r win-x64 --self-contained=false
```

Output folder contains `OcrOverlay.exe` plus a few DLLs (~5 MB total). Target machine must have **.NET 10 Desktop Runtime (x64)** installed: https://dotnet.microsoft.com/download/dotnet/10.0

### 3. Trimmed self-contained (smaller single-file, more risk)

Adds IL trimming on top of #1. Cuts ~40 % of the size but can break reflection-heavy code (WPF data binding, JSON serialization). Test thoroughly.

```bash
cd src/OcrOverlay/OcrOverlay
dotnet publish -c Release -r win-x64 --self-contained=true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimMode=partial
```

### Release checklist

1. **Bump version** — set `<Version>1.0.0</Version>` in `OcrOverlay.csproj` (under `<PropertyGroup>`)
2. **Build** with one of the commands above
3. **Smoke test** the published exe on a clean folder (no `bin/obj` siblings):
   - Launch → main window appears with the app icon
   - `Ctrl+Shift+R` → region selection works
   - `Ctrl+Shift+T` → full screen translate works
   - Close → prompt appears, "No" minimizes to tray, double-click tray icon restores
4. **Test on a clean machine** (or VM) without the .NET SDK installed — confirms self-contained packaging is correct
5. **Zip the output** as `OcrOverlay-vX.Y.Z-win-x64.zip` for distribution

### Notes

- `appsettings.json` is copied next to the exe; users can edit hotkeys/languages there for persistent defaults
- There is no installer — the app is portable. Users can place the exe anywhere and double-click to run
- Code signing is recommended for public distribution (avoids SmartScreen warnings) but optional. Use `signtool sign /fd SHA256 /a OcrOverlay.exe` with an Authenticode certificate
- App is configured `asInvoker` (no UAC elevation needed)

## Notes

- The app uses `WindowState=Minimized` (not hidden) before full-screen capture so the window does not appear in the screenshot. Click the taskbar icon to restore.
- `OverlayWindow` is dismissed by **ESC** or any mouse click.
- `RegionSelectorWindow` is cancelled by **ESC** or by clicking without dragging.
- The `TranslationCache` deduplicates repeated phrases across calls (in-memory only, lost on exit).
- DPI scaling is handled via `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` and DIPs↔pixels conversion through `PresentationSource.TransformToDevice` / `TransformFromDevice`.

## Known issues

- The unofficial Google Translate endpoint is rate-limited and not guaranteed stable — fine for personal use, not production.
- "Auto detect" OCR runs the engine once per installed language pack; install only what you need to keep latency low.

## Roadmap (next)

- Global hotkey to trigger region select / full-screen translate without focusing the main window
- Real-time loop with diff detection and click-through overlay
- Tray icon + persistent settings
