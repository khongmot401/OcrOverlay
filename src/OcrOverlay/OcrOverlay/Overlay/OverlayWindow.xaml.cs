using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OcrOverlay.Core;

namespace OcrOverlay.Overlay;

public partial class OverlayWindow : Window
{
    private readonly IReadOnlyList<TranslatedLine> _lines;

    public OverlayWindow(IReadOnlyList<TranslatedLine> lines)
    {
        InitializeComponent();
        _lines = lines;

        // Cover entire virtual screen so we can place children at any screen pixel.
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Loaded += (_, _) => RenderLines();
    }

    private void RenderLines()
    {
        var source = PresentationSource.FromVisual(this);
        Matrix toDip = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

        foreach (var line in _lines)
        {
            if (string.IsNullOrWhiteSpace(line.Translated)) continue;

            var tl = toDip.Transform(new System.Windows.Point(line.ScreenBox.X, line.ScreenBox.Y));
            var br = toDip.Transform(new System.Windows.Point(line.ScreenBox.Right, line.ScreenBox.Bottom));

            // Position relative to this window (which sits at virtual-screen origin).
            var x = tl.X - Left;
            var y = tl.Y - Top;
            var w = br.X - tl.X;
            var h = br.Y - tl.Y;

            var border = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xF0, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0, 0xAA, 0xFF)),
                BorderThickness = new Thickness(1),
                MinWidth = w,
                MinHeight = h,
                Padding = new Thickness(2),
                Child = new TextBlock
                {
                    Text = line.Translated,
                    Foreground = System.Windows.Media.Brushes.White,
                    // Pick a font size that roughly matches the original line height.
                    FontSize = Math.Max(10, h * 0.7),
                    TextWrapping = TextWrapping.NoWrap,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
                }
            };

            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            RootCanvas.Children.Add(border);
        }
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        // Click anywhere to dismiss.
        Close();
        base.OnMouseDown(e);
    }
}
