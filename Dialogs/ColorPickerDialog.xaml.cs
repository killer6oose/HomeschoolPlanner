using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HomeschoolPlanner.Helpers;

namespace HomeschoolPlanner.Dialogs;

public partial class ColorPickerDialog : Window
{
    // HSV state (hue 0-360, sat 0-1, val 0-1)
    private double _hue        = 210;
    private double _saturation = 0.64;
    private double _brightness = 1.0;

    private bool _suppressHexUpdate   = false;
    private bool _draggingWheel       = false;
    private bool _draggingBrightness  = false;

    // The chosen hex string - read after ShowDialog() == true
    public string SelectedHex { get; private set; } = "#4A7CB5";

    private const int WheelSize   = 210;
    private const double WheelR   = WheelSize / 2.0 - 3;

    public ColorPickerDialog(string initialHex)
    {
        InitializeComponent();
        ParseHexToHsv(initialHex);
        SelectedHex = NormalizeHex(initialHex);

        Loaded += (_, _) =>
        {
            BrightnessCanvas.SizeChanged += (_, _) => RenderBrightnessTrack();

            RenderWheel();
            RenderBrightnessTrack();
            UpdateCrosshair();
            SyncHexFromHsv();

            // Add the standard swatches - clicking one updates HexInput which
            // triggers HexInput_TextChanged and syncs everything
            SwatchesHost.Children.Add(
                ColorPickerHelper.BuildSwatchPanel(HexInput, ColorPreviewBig));
        };
    }

    // -------------------------------------------------------------------------
    // Wheel rendering
    // -------------------------------------------------------------------------

    private void RenderWheel()
    {
        int size = WheelSize;
        double cx = size / 2.0, cy = size / 2.0;
        var pixels = new byte[size * size * 4];
        int stride = size * 4;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double dx = x - cx, dy = y - cy;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                int idx = y * stride + x * 4;
                if (dist > WheelR + 1.5)
                {
                    // transparent
                    continue;
                }

                double sat = Math.Min(dist / WheelR, 1.0);
                double hue = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                if (hue < 0) hue += 360.0;

                // Soft anti-alias at edge
                double alpha = dist > WheelR - 0.5
                    ? Math.Max(0, WheelR + 1.5 - dist)
                    : 1.0;

                var c = HsvToColor(hue, sat, _brightness);
                pixels[idx + 0] = (byte)(c.B * alpha);
                pixels[idx + 1] = (byte)(c.G * alpha);
                pixels[idx + 2] = (byte)(c.R * alpha);
                pixels[idx + 3] = (byte)(255 * alpha);
            }
        }

        var bmp = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
        bmp.WritePixels(new Int32Rect(0, 0, size, size), pixels, stride, 0);
        WheelImage.Source = bmp;
    }

    // -------------------------------------------------------------------------
    // Brightness strip rendering
    // -------------------------------------------------------------------------

    private void RenderBrightnessTrack()
    {
        double w = BrightnessCanvas.ActualWidth;
        if (w < 4) w = 258;

        BrightnessCanvas.Children.Clear();

        var satColor = HsvToColor(_hue, _saturation, 1.0);

        // Gradient rect: black -> full-sat colour
        var track = new Rectangle
        {
            Width   = w,
            Height  = 20,
            RadiusX = 4,
            RadiusY = 4,
            Fill    = new LinearGradientBrush(Colors.Black, satColor, 0)
        };
        Canvas.SetLeft(track, 0);
        Canvas.SetTop(track,  0);
        BrightnessCanvas.Children.Add(track);

        // White thumb indicator
        double thumbX = _brightness * (w - 6) + 3;
        var thumb = new Rectangle
        {
            Width   = 4,
            Height  = 20,
            RadiusX = 2,
            RadiusY = 2,
            Fill    = Brushes.White,
            Effect  = new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 0,
                Color       = Colors.Black,
                BlurRadius  = 4,
                Opacity     = 0.7
            }
        };
        Canvas.SetLeft(thumb, thumbX - 2);
        Canvas.SetTop(thumb,  0);
        BrightnessCanvas.Children.Add(thumb);
    }

    // -------------------------------------------------------------------------
    // Crosshair cursor on the wheel
    // -------------------------------------------------------------------------

    private void UpdateCrosshair()
    {
        WheelOverlay.Children.Clear();

        double cx = WheelSize / 2.0, cy = WheelSize / 2.0;
        double angle = _hue * Math.PI / 180.0;
        double r     = _saturation * WheelR;
        double px = cx + r * Math.Cos(angle);
        double py = cy + r * Math.Sin(angle);

        // White outer ring
        var outer = new Ellipse
        {
            Width           = 13,
            Height          = 13,
            Stroke          = Brushes.White,
            StrokeThickness = 2.5,
            Fill            = Brushes.Transparent
        };
        Canvas.SetLeft(outer, px - 6.5);
        Canvas.SetTop(outer,  py - 6.5);
        WheelOverlay.Children.Add(outer);

        // Dark inner ring for contrast on light colours
        var inner = new Ellipse
        {
            Width           = 9,
            Height          = 9,
            Stroke          = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            StrokeThickness = 1,
            Fill            = Brushes.Transparent
        };
        Canvas.SetLeft(inner, px - 4.5);
        Canvas.SetTop(inner,  py - 4.5);
        WheelOverlay.Children.Add(inner);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void UpdateAll()
    {
        RenderWheel();
        RenderBrightnessTrack();
        UpdateCrosshair();
        SyncHexFromHsv();
    }

    private void SyncHexFromHsv()
    {
        var c   = HsvToColor(_hue, _saturation, _brightness);
        var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        SelectedHex = hex;
        ColorPreviewBig.Background = new SolidColorBrush(c);
        _suppressHexUpdate = true;
        HexInput.Text      = hex;
        _suppressHexUpdate = false;
    }

    // -------------------------------------------------------------------------
    // Wheel mouse
    // -------------------------------------------------------------------------

    private void WheelContainer_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingWheel = true;
        WheelContainer.CaptureMouse();
        ApplyWheelPoint(e.GetPosition(WheelContainer));
    }

    private void WheelContainer_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_draggingWheel) return;
        ApplyWheelPoint(e.GetPosition(WheelContainer));
    }

    private void WheelContainer_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _draggingWheel = false;
        WheelContainer.ReleaseMouseCapture();
    }

    private void ApplyWheelPoint(Point p)
    {
        double cx = WheelSize / 2.0, cy = WheelSize / 2.0;
        double dx = p.X - cx, dy = p.Y - cy;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        _saturation = Math.Min(dist / WheelR, 1.0);
        double h = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        _hue = h < 0 ? h + 360.0 : h;
        UpdateAll();
    }

    // -------------------------------------------------------------------------
    // Brightness mouse
    // -------------------------------------------------------------------------

    private void Brightness_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingBrightness = true;
        BrightnessCanvas.CaptureMouse();
        ApplyBrightnessPoint(e.GetPosition(BrightnessCanvas));
    }

    private void Brightness_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_draggingBrightness) return;
        ApplyBrightnessPoint(e.GetPosition(BrightnessCanvas));
    }

    private void Brightness_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _draggingBrightness = false;
        BrightnessCanvas.ReleaseMouseCapture();
    }

    private void ApplyBrightnessPoint(Point p)
    {
        double w = BrightnessCanvas.ActualWidth;
        if (w <= 0) return;
        _brightness = Math.Max(0, Math.Min(1, p.X / w));
        UpdateAll();
    }

    // -------------------------------------------------------------------------
    // Hex text box
    // -------------------------------------------------------------------------

    private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressHexUpdate) return;
        var hex = HexInput.Text.Trim();
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (hex.Length == 7 && TryParseHex(hex, out var c))
        {
            RgbToHsv(c, out _hue, out _saturation, out _brightness);
            RenderWheel();
            RenderBrightnessTrack();
            UpdateCrosshair();
            ColorPreviewBig.Background = new SolidColorBrush(c);
            SelectedHex = hex.ToUpperInvariant();
        }
    }

    // -------------------------------------------------------------------------
    // Colour math
    // -------------------------------------------------------------------------

    private static Color HsvToColor(double h, double s, double v)
    {
        if (s <= 0) { var g = (byte)(v * 255); return Color.FromRgb(g, g, g); }
        h %= 360; if (h < 0) h += 360;
        double sec = h / 60.0;
        int    i   = (int)sec;
        double f   = sec - i;
        double p   = v * (1 - s);
        double q   = v * (1 - s * f);
        double t   = v * (1 - s * (1 - f));
        return i switch
        {
            0 => Rgb(v, t, p),
            1 => Rgb(q, v, p),
            2 => Rgb(p, v, t),
            3 => Rgb(p, q, v),
            4 => Rgb(t, p, v),
            _ => Rgb(v, p, q)
        };
    }

    private static Color Rgb(double r, double g, double b) =>
        Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));

    private static void RgbToHsv(Color c, out double h, out double s, out double v)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double d   = max - min;
        v = max;
        s = max == 0 ? 0 : d / max;
        if (d == 0)        { h = 0; return; }
        if (max == r)        h = 60 * (((g - b) / d) % 6);
        else if (max == g)   h = 60 * ((b - r) / d + 2);
        else                 h = 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
    }

    private void ParseHexToHsv(string hex)
    {
        if (TryParseHex(hex, out var c)) RgbToHsv(c, out _hue, out _saturation, out _brightness);
    }

    private static bool TryParseHex(string hex, out Color color)
    {
        color = Colors.SteelBlue;
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length != 6) return false;
            color = Color.FromRgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
            return true;
        }
        catch { return false; }
    }

    private static string NormalizeHex(string hex)
    {
        if (!hex.StartsWith("#")) hex = "#" + hex;
        return hex.Length == 7 ? hex.ToUpperInvariant() : "#4A7CB5";
    }

    // -------------------------------------------------------------------------
    // Buttons
    // -------------------------------------------------------------------------

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        SyncHexFromHsv();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
