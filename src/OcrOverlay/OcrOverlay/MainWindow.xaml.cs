using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using OcrOverlay.Capture;
using OcrOverlay.Core;
using OcrOverlay.Hotkey;
using OcrOverlay.Interop;
using OcrOverlay.Ocr;
using OcrOverlay.Overlay;
using OcrOverlay.Resources;
using OcrOverlay.Translate;

namespace OcrOverlay;

public partial class MainWindow : Window
{
    private const int HotkeyIdRegion = 1;
    private const int HotkeyIdFullScreen = 2;

    private readonly AppSettings _settings = App.Settings;
    private readonly IScreenCapture _capture = new ScreenCapture();
    private readonly IOcrEngine _ocr = new WindowsOcrEngine();
    private readonly TranslationCache _cache = new();
    private readonly Dictionary<string, ITranslator> _providers;
    private OcrPipeline _pipeline;
    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private HotkeyCombo? _activeRegionHotkey;
    private HotkeyCombo? _activeFullScreenHotkey;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _forceExit;

    public MainWindow()
    {
        InitializeComponent();
        _providers = new Dictionary<string, ITranslator>
        {
            ["Google"] = new GoogleTranslator(_cache),
        };
        _pipeline = new OcrPipeline(_capture, _ocr, _providers["Google"], _settings);
        InitLanguageBoxes();
        InitProviderBox();
        RegionHotkeyBox.Text = _settings.HotkeyRegion;
        FullScreenHotkeyBox.Text = _settings.HotkeyFullScreen;
        InitTrayIcon();
        Icon = AppIcon.GetImageSource();
    }

    // ---------- Tray icon ----------

    private void InitTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => RestoreFromTray());
        menu.Items.Add("Translate full screen", null, (_, _) => FullScreenTranslateButton_Click(this, new RoutedEventArgs()));
        menu.Items.Add("Select region & translate", null, (_, _) => SelectRegionButton_Click(this, new RoutedEventArgs()));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => { _forceExit = true; Close(); });

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = AppIcon.GetIcon(),
            Text = "OcrOverlay",
            Visible = false,
            ContextMenuStrip = menu,
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void HideToTray()
    {
        if (_trayIcon is null) return;
        _trayIcon.Visible = true;
        Hide();
        ShowInTaskbar = false;
        _trayIcon.ShowBalloonTip(1500, "OcrOverlay", "Still running in the tray. Double-click the icon to restore.", System.Windows.Forms.ToolTipIcon.Info);
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        ShowInTaskbar = true;
        Activate();
        if (_trayIcon is not null) _trayIcon.Visible = false;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_forceExit)
        {
            base.OnClosing(e);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            this,
            "Do you want to exit OcrOverlay?\n\nYes  — Exit completely\nNo   — Minimize to tray (keep hotkeys active)\nCancel — Stay open",
            "Close OcrOverlay",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        switch (result)
        {
            case MessageBoxResult.Yes:
                _forceExit = true;
                break;
            case MessageBoxResult.No:
                e.Cancel = true;
                HideToTray();
                break;
            default:
                e.Cancel = true;
                break;
        }
        base.OnClosing(e);
    }

    // ---------- Hotkey ----------

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);

        _activeRegionHotkey = TryRegister(HotkeyIdRegion, _settings.HotkeyRegion, "Region select");
        _activeFullScreenHotkey = TryRegister(HotkeyIdFullScreen, _settings.HotkeyFullScreen, "Full screen");
        UpdateHotkeyLabels();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_activeRegionHotkey is not null) NativeMethods.UnregisterHotKey(_hwnd, HotkeyIdRegion);
        if (_activeFullScreenHotkey is not null) NativeMethods.UnregisterHotKey(_hwnd, HotkeyIdFullScreen);
        _hwndSource?.RemoveHook(WndProc);
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.OnClosed(e);
    }

    private HotkeyCombo? TryRegister(int id, string text, string label)
    {
        var combo = HotkeyParser.Parse(text);
        if (combo is null)
        {
            Log($"[{label}] Invalid hotkey '{text}'.");
            return null;
        }
        if (!NativeMethods.RegisterHotKey(_hwnd, id, combo.Modifiers, combo.VirtualKey))
        {
            Log($"[{label}] Hotkey '{combo.Display}' is already in use by another app.");
            return null;
        }
        Log($"[{label}] Registered {combo.Display}.");
        return combo;
    }

    private void Rebind(int id, ref HotkeyCombo? current, HotkeyCombo desired, string label)
    {
        if (current is not null) NativeMethods.UnregisterHotKey(_hwnd, id);
        if (!NativeMethods.RegisterHotKey(_hwnd, id, desired.Modifiers, desired.VirtualKey))
        {
            // Re-register the previous one to keep the app usable.
            if (current is not null) NativeMethods.RegisterHotKey(_hwnd, id, current.Modifiers, current.VirtualKey);
            System.Windows.MessageBox.Show(this,
                $"Hotkey '{desired.Display}' is already in use by another application.\nThe previous hotkey was kept.",
                "Hotkey conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
            Log($"[{label}] Conflict — kept '{current?.Display ?? "(none)"}'.");
            return;
        }
        current = desired;
        Log($"[{label}] Hotkey changed to {desired.Display}.");
    }

    private void UpdateHotkeyLabels()
    {
        RegionHotkeyLabel.Text = _activeRegionHotkey is null ? "(unset)" : $"({_activeRegionHotkey.Display})";
        FullScreenHotkeyLabel.Text = _activeFullScreenHotkey is null ? "(unset)" : $"({_activeFullScreenHotkey.Display})";
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (id == HotkeyIdRegion) { handled = true; SelectRegionButton_Click(this, new RoutedEventArgs()); }
            else if (id == HotkeyIdFullScreen) { handled = true; FullScreenTranslateButton_Click(this, new RoutedEventArgs()); }
        }
        return IntPtr.Zero;
    }

    // ---------- Hotkey TextBox capture ----------

    private void HotkeyBox_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb)
            tb.Background = System.Windows.Media.Brushes.LightYellow;
    }

    private void HotkeyBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb)
            tb.Background = System.Windows.Media.Brushes.White;
    }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        if (sender is not System.Windows.Controls.TextBox tb) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var combo = HotkeyParser.FromKeyEvent(key, Keyboard.Modifiers);
        if (combo is null) return;

        tb.Text = combo.Display;
        if (tb == RegionHotkeyBox)
        {
            Rebind(HotkeyIdRegion, ref _activeRegionHotkey, combo, "Region select");
            _settings.HotkeyRegion = combo.Display;
        }
        else if (tb == FullScreenHotkeyBox)
        {
            Rebind(HotkeyIdFullScreen, ref _activeFullScreenHotkey, combo, "Full screen");
            _settings.HotkeyFullScreen = combo.Display;
        }
        UpdateHotkeyLabels();
    }

    // ---------- Logging ----------

    private void Log(string message)
    {
        LogText.Text += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        LogScroll.ScrollToEnd();
    }

    // ---------- Provider / language ----------

    private record LangItem(string Code, string Display)
    {
        public override string ToString() => Display;
    }

    private void InitLanguageBoxes()
    {
        var sourceLangs = new[]
        {
            new LangItem("auto", "Auto detect"),
            new LangItem("en", "English"),
            new LangItem("vi", "Vietnamese"),
            new LangItem("zh", "Chinese"),
            new LangItem("ja", "Japanese"),
            new LangItem("ko", "Korean"),
            new LangItem("fr", "French"),
            new LangItem("de", "German"),
            new LangItem("es", "Spanish"),
            new LangItem("ru", "Russian"),
        };
        var targetLangs = sourceLangs.Where(l => l.Code != "auto").ToArray();

        SourceLangBox.ItemsSource = sourceLangs;
        TargetLangBox.ItemsSource = targetLangs;
        SourceLangBox.SelectedItem = sourceLangs.FirstOrDefault(l => l.Code == _settings.SourceLanguage) ?? sourceLangs[0];
        TargetLangBox.SelectedItem = targetLangs.FirstOrDefault(l => l.Code == _settings.TargetLanguage) ?? targetLangs[0];
    }

    private void InitProviderBox()
    {
        ProviderBox.ItemsSource = _providers.Keys.ToArray();
        ProviderBox.SelectedItem = "Google";
    }

    private void ProviderBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProviderBox.SelectedItem is string name && _providers.TryGetValue(name, out var translator))
        {
            _pipeline = new OcrPipeline(_capture, _ocr, translator, _settings);
            Log($"Provider: {name}");
        }
    }

    private void SourceLangBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SourceLangBox.SelectedItem is LangItem item)
        {
            _settings.SourceLanguage = item.Code;
            Log($"Source: {item.Display}");
        }
    }

    private void TargetLangBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TargetLangBox.SelectedItem is LangItem item)
        {
            _settings.TargetLanguage = item.Code;
            Log($"Target: {item.Display}");
        }
    }

    // ---------- Actions ----------

    private async void SelectRegionButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
        try
        {
            await Task.Delay(200);

            var selector = new RegionSelectorWindow();
            if (selector.ShowDialog() != true || selector.SelectedRegion is not Rectangle region)
            {
                Log("Selection cancelled.");
                return;
            }

            Log($"Region {region.Width}x{region.Height} @ ({region.X},{region.Y}) — processing...");
            var result = await _pipeline.RunOnceAsync(region);
            if (result.Lines.Count == 0)
            {
                Log("No text detected.");
                return;
            }

            Log($"OCR {result.Lines.Count} lines, translated. Overlay shown.");
            new OverlayWindow(result.Lines).Show();
        }
        catch (Exception ex)
        {
            Log("Error: " + ex.Message);
        }
    }

    private async void FullScreenTranslateButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
        try
        {
            await Task.Delay(250);

            var bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                         ?? new Rectangle(0, 0, 1920, 1080);

            var result = await _pipeline.RunOnceAsync(bounds);
            if (result.Lines.Count == 0)
            {
                Log("No text detected on screen.");
                WindowState = WindowState.Normal;
                return;
            }

            Log($"Full screen: {result.Lines.Count} lines translated.");
            new OverlayWindow(result.Lines).Show();
        }
        catch (Exception ex)
        {
            Log("Error: " + ex.Message);
            WindowState = WindowState.Normal;
        }
    }
}
