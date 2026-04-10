using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OcrOverlay.Core;

namespace OcrOverlay.Overlay;

public partial class OverlayWindow : Window
{
    private readonly IReadOnlyList<TranslatedLine> _lines;
    private readonly bool _sidebarMode;
    private readonly int _minWidth;

    public OverlayWindow(IReadOnlyList<TranslatedLine> lines)
    {
        InitializeComponent();
        _lines = lines;
        _sidebarMode = App.Settings.SidebarMode;
        _minWidth = App.Settings.OverlayMinWidth;

        // Cover entire virtual screen so we can place children at any screen pixel.
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Loaded += (_, _) => RenderLines();
    }

    private record LineDip(double X, double Y, double W, double H, string Text);

    private void RenderLines()
    {
        var source = PresentationSource.FromVisual(this);
        Matrix toDip = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;

        // Convert all lines to DIP coordinates.
        var dipLines = new List<LineDip>();
        foreach (var line in _lines)
        {
            if (string.IsNullOrWhiteSpace(line.Translated)) continue;
            var tl = toDip.Transform(new System.Windows.Point(line.ScreenBox.X, line.ScreenBox.Y));
            var br = toDip.Transform(new System.Windows.Point(line.ScreenBox.Right, line.ScreenBox.Bottom));
            dipLines.Add(new LineDip(tl.X - Left, tl.Y - Top, br.X - tl.X, br.Y - tl.Y, line.Translated));
        }

        var groups = MergeNearbyLines(dipLines);

        foreach (var g in groups)
        {
            var avgH = g.Sum(l => l.H) / g.Count;
            var fontSize = Math.Max(10, avgH * 0.7);

            var groupX = g.Min(l => l.X);
            var groupY = g.Min(l => l.Y);
            var groupRight = g.Max(l => l.X + l.W);
            var groupBottom = g.Max(l => l.Y + l.H);
            var groupW = groupRight - groupX;
            var groupH = groupBottom - groupY;

            var effectiveMinWidth = Math.Max(groupW, _minWidth);
            var posX = _sidebarMode ? groupRight + 4 : groupX;
            var combinedText = string.Join("\n", g.Select(l => l.Text));

            var border = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xF0, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0, 0xAA, 0xFF)),
                BorderThickness = new Thickness(1),
                MinWidth = _sidebarMode ? effectiveMinWidth : groupW,
                MinHeight = groupH,
                Padding = new Thickness(2),
                Child = new TextBlock
                {
                    Text = combinedText,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = fontSize,
                    TextWrapping = _sidebarMode ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
                }
            };

            Canvas.SetLeft(border, posX);
            Canvas.SetTop(border, groupY);
            RootCanvas.Children.Add(border);
        }
    }

    /// <summary>
    /// Merge lines whose bounding boxes overlap or are very close vertically/horizontally
    /// into a single group so they render as one box.
    /// </summary>
    private static List<List<LineDip>> MergeNearbyLines(List<LineDip> lines)
    {
        if (lines.Count == 0) return [];

        var sorted = lines.OrderBy(l => l.Y).ThenBy(l => l.X).ToList();
        var groups = new List<List<LineDip>> { new() { sorted[0] } };

        for (int i = 1; i < sorted.Count; i++)
        {
            var cur = sorted[i];
            bool merged = false;

            for (int g = 0; g < groups.Count; g++)
            {
                if (ShouldMerge(groups[g], cur))
                {
                    groups[g].Add(cur);
                    merged = true;
                    break;
                }
            }

            if (!merged)
                groups.Add(new List<LineDip> { cur });
        }

        return groups;
    }

    private static bool ShouldMerge(List<LineDip> group, LineDip candidate)
    {
        var gTop = group.Min(l => l.Y);
        var gBottom = group.Max(l => l.Y + l.H);
        var gLeft = group.Min(l => l.X);
        var gRight = group.Max(l => l.X + l.W);
        var avgH = group.Average(l => l.H);

        // Gap threshold: if the space between boxes is less than one line height,
        // they are close enough to merge.
        var threshold = avgH;

        bool verticallyClose = candidate.Y <= gBottom + threshold && candidate.Y + candidate.H >= gTop - threshold;
        bool horizontallyClose = candidate.X <= gRight + threshold && candidate.X + candidate.W >= gLeft - threshold;

        return verticallyClose && horizontallyClose;
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
