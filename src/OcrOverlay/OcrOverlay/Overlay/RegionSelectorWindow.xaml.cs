using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OcrOverlay.Overlay;

public partial class RegionSelectorWindow : Window
{
    private System.Windows.Point _start;
    private bool _dragging;

    // Selected region in screen pixels (null if cancelled).
    public Rectangle? SelectedRegion { get; private set; }

    public RegionSelectorWindow()
    {
        InitializeComponent();

        // Cover the entire virtual screen (multi-monitor aware). Values are in DIPs.
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SelectedRegion = null;
            DialogResult = false;
            Close();
        }
        base.OnKeyDown(e);
    }

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        _start = e.GetPosition(RootCanvas);
        _dragging = true;
        SelectionRect.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRect, _start.X);
        Canvas.SetTop(SelectionRect, _start.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        CaptureMouse();
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetPosition(RootCanvas);
        var x = Math.Min(_start.X, p.X);
        var y = Math.Min(_start.Y, p.Y);
        var w = Math.Abs(p.X - _start.X);
        var h = Math.Abs(p.Y - _start.Y);
        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;
    }

    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();

        var end = e.GetPosition(RootCanvas);
        var x = Math.Min(_start.X, end.X);
        var y = Math.Min(_start.Y, end.Y);
        var w = Math.Abs(end.X - _start.X);
        var h = Math.Abs(end.Y - _start.Y);

        if (w < 5 || h < 5)
        {
            SelectedRegion = null;
            DialogResult = false;
            Close();
            return;
        }

        // Canvas coordinates are in DIPs relative to the window's top-left,
        // which sits at VirtualScreen origin. Convert to absolute screen pixels.
        var topLeftDip = new System.Windows.Point(Left + x, Top + y);
        var bottomRightDip = new System.Windows.Point(Left + x + w, Top + y + h);

        var source = PresentationSource.FromVisual(this);
        Matrix toDevice = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var tlPx = toDevice.Transform(topLeftDip);
        var brPx = toDevice.Transform(bottomRightDip);

        SelectedRegion = new Rectangle(
            (int)Math.Round(tlPx.X),
            (int)Math.Round(tlPx.Y),
            (int)Math.Round(brPx.X - tlPx.X),
            (int)Math.Round(brPx.Y - tlPx.Y));

        DialogResult = true;
        Close();
    }
}
