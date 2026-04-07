using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace OcrOverlay.Resources;

// Generates the app icon at runtime so we don't need an .ico file in the repo.
// Replace with a real .ico (set in csproj <ApplicationIcon> and Window.Icon) anytime.
public static class AppIcon
{
    private static Icon? _icon;
    private static System.Windows.Media.ImageSource? _imageSource;

    public static Icon GetIcon() => _icon ??= BuildIcon();
    public static System.Windows.Media.ImageSource GetImageSource() => _imageSource ??= BuildImageSource();

    private static Icon BuildIcon()
    {
        using var bmp = DrawBitmap(64);
        var hIcon = bmp.GetHicon();
        try
        {
            // Clone so we can destroy the GDI handle right away.
            using var temp = Icon.FromHandle(hIcon);
            using var ms = new MemoryStream();
            temp.Save(ms);
            ms.Position = 0;
            return new Icon(ms);
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static System.Windows.Media.ImageSource BuildImageSource()
    {
        using var bmp = DrawBitmap(64);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Position = 0;

        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }

    private static Bitmap DrawBitmap(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // Background: rounded square with blue gradient.
        using var bg = new LinearGradientBrush(
            new Rectangle(0, 0, size, size),
            Color.FromArgb(0x21, 0x96, 0xF3),
            Color.FromArgb(0x0D, 0x47, 0xA1),
            45f);
        using var path = RoundedRect(new Rectangle(2, 2, size - 4, size - 4), size / 6);
        g.FillPath(bg, path);

        // Letters "OT".
        using var font = new Font("Segoe UI", size * 0.42f, FontStyle.Bold, GraphicsUnit.Pixel);
        var text = "OT";
        var sz = g.MeasureString(text, font);
        g.DrawString(text, font, Brushes.White, (size - sz.Width) / 2, (size - sz.Height) / 2 - 1);

        return bmp;
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
