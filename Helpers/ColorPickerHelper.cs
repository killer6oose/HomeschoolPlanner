using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HomeschoolPlanner.Helpers;

// Builds a color swatch grid + wires it to a hex TextBox and preview Border.
// Call BuildSwatchPanel() and add the returned WrapPanel to your dialog's layout.
public static class ColorPickerHelper
{
    // 30 common colors - covers most subject color needs
    private static readonly string[] Swatches =
    {
        "#F44336", "#E91E63", "#9C27B0", "#673AB7", "#3F51B5", "#2196F3",
        "#03A9F4", "#00BCD4", "#009688", "#4CAF50", "#8BC34A", "#CDDC39",
        "#FFEB3B", "#FFC107", "#FF9800", "#FF5722", "#795548", "#607D8B",
        "#9E9E9E", "#000000", "#FFFFFF", "#4A7CB5", "#43A047", "#FB8C00",
        "#E53935", "#AD1457", "#6A1B9A", "#1565C0", "#00838F", "#2E7D32",
    };

    // Returns a WrapPanel of color swatches.
    // Clicking a swatch sets hexInput.Text to that hex and updates previewBorder.Background.
    public static WrapPanel BuildSwatchPanel(TextBox hexInput, Border previewBorder)
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(0, 6, 0, 0)
        };

        foreach (var hex in Swatches)
        {
            var color = ParseHex(hex);
            var swatch = new Border
            {
                Width           = 22,
                Height          = 22,
                CornerRadius    = new CornerRadius(3),
                Background      = new SolidColorBrush(color),
                Margin          = new Thickness(0, 0, 4, 4),
                Cursor          = Cursors.Hand,
                ToolTip         = hex,
                BorderThickness = new Thickness(1),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0))
            };

            // Capture for the closure
            var capturedHex    = hex;
            var capturedColor  = color;

            swatch.MouseEnter += (s, e) =>
                ((Border)s).BorderBrush = new SolidColorBrush(Colors.DimGray);
            swatch.MouseLeave += (s, e) =>
                ((Border)s).BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
            swatch.MouseLeftButtonUp += (s, e) =>
            {
                hexInput.Text           = capturedHex;
                previewBorder.Background = new SolidColorBrush(capturedColor);
            };

            panel.Children.Add(swatch);
        }

        return panel;
    }

    private static Color ParseHex(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return Color.FromRgb(r, g, b);
        }
        catch { return Color.FromRgb(0x4A, 0x7C, 0xB5); }
    }
}
