using System.Windows.Input;
using OcrOverlay.Interop;

namespace OcrOverlay.Hotkey;

public record HotkeyCombo(uint Modifiers, uint VirtualKey, string Display);

public static class HotkeyParser
{
    // Parse strings like "Ctrl+Shift+R", "Alt+F1", "Win+Space".
    public static HotkeyCombo? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        uint mods = 0;
        Key key = Key.None;
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var raw in parts)
        {
            var p = raw.ToLowerInvariant();
            switch (p)
            {
                case "ctrl":
                case "control":
                    mods |= NativeMethods.MOD_CONTROL; break;
                case "shift":
                    mods |= NativeMethods.MOD_SHIFT; break;
                case "alt":
                    mods |= NativeMethods.MOD_ALT; break;
                case "win":
                case "windows":
                    mods |= NativeMethods.MOD_WIN; break;
                default:
                    if (!Enum.TryParse<Key>(raw, true, out key)) return null;
                    break;
            }
        }

        if (key == Key.None) return null;
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        return new HotkeyCombo(mods, vk, Format(mods, key));
    }

    public static HotkeyCombo? FromKeyEvent(Key key, ModifierKeys modifiers)
    {
        // Ignore standalone modifier keys.
        if (key is Key.LeftCtrl or Key.RightCtrl
                or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt
                or Key.LWin or Key.RWin
                or Key.System or Key.None)
            return null;

        // Resolve System key (Alt-pressed) to its real key.
        if (key == Key.System) key = Key.None;

        uint mods = 0;
        if (modifiers.HasFlag(ModifierKeys.Control)) mods |= NativeMethods.MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mods |= NativeMethods.MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mods |= NativeMethods.MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mods |= NativeMethods.MOD_WIN;

        // Require at least one modifier so we don't grab plain letter keys globally.
        if (mods == 0) return null;

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        return new HotkeyCombo(mods, vk, Format(mods, key));
    }

    private static string Format(uint mods, Key key)
    {
        var parts = new List<string>();
        if ((mods & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mods & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((mods & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
        if ((mods & NativeMethods.MOD_WIN) != 0) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
