using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HomeschoolPlanner.Dialogs;

namespace HomeschoolPlanner.Helpers;

// Builds a colour swatch grid + wires it to a hex TextBox and preview Border.
// Call BuildSwatchPanel() and add the returned WrapPanel to your dialog's layout.
// Call AttachColorWheelPicker() to open the full colour wheel when the hex box or
// preview border is clicked.
public static class ColorPickerHelper
{
    // 30 common colors - covers most subject colour needs
    private static readonly string[] Swatches =
    {
        "#F44336", "#E91E63", "#9C27B0", "#673AB7", "#3F51B5", "#2196F3",
        "#03A9F4", "#00BCD4", "#009688", "#4CAF50", "#8BC34A", "#CDDC39",
        "#FFEB3B", "#FFC107", "#FF9800", "#FF5722", "#795548", "#607D8B",
        "#9E9E9E", "#000000", "#FFFFFF", "#4A7CB5", "#43A047", "#FB8C00",
        "#E53935", "#AD1457", "#6A1B9A", "#1565C0", "#00838F", "#2E7D32",
    };

    // Returns a WrapPanel of colour swatches.
    // Clicking a swatch sets hexInput.Text and updates previewBorder.Background.
    public static WrapPanel BuildSwatchPanel(TextBox hexInput, Border previewBorder)
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(0, 6, 0, 0)
        };

        foreach (var hex in Swatches)
        {
            var color  = ParseHex(hex);
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

            var capturedHex   = hex;
            var capturedColor = color;

            swatch.MouseEnter        += (s, _) => ((Border)s).BorderBrush = new SolidColorBrush(Colors.DimGray);
            swatch.MouseLeave        += (s, _) => ((Border)s).BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
            swatch.MouseLeftButtonUp += (_, _) =>
            {
                hexInput.Text            = capturedHex;
                previewBorder.Background = new SolidColorBrush(capturedColor);
            };

            panel.Children.Add(swatch);
        }

        return panel;
    }

    // Wires a hex TextBox + preview Border so that:
    //   - clicking the preview border always opens the colour wheel
    //   - clicking the hex box when it is NOT already focused opens the colour wheel
    //     (subsequent clicks while focused allow normal text editing)
    public static void AttachColorWheelPicker(TextBox hexInput, Border previewBorder)
    {
        // Preview border acts as a "open picker" button
        previewBorder.Cursor = Cursors.Hand;
        previewBorder.MouseLeftButtonDown += (_, _) => OpenPicker(hexInput, previewBorder);

        // Hex box: first click (when not focused) opens the picker
        hexInput.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!hexInput.IsKeyboardFocused)
            {
                e.Handled = true;
                OpenPicker(hexInput, previewBorder);
            }
        };
    }

    private static void OpenPicker(TextBox hexInput, Border previewBorder)
    {
        var owner = Window.GetWindow(hexInput);
        var dlg   = new ColorPickerDialog(hexInput.Text) { Owner = owner };
        if (dlg.ShowDialog() == true)
        {
            hexInput.Text            = dlg.SelectedHex;
            previewBorder.Background = ParseBrush(dlg.SelectedHex);
        }
    }

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    private static Color ParseHex(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            return Color.FromRgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
        }
        catch { return Color.FromRgb(0x4A, 0x7C, 0xB5); }
    }

    private static SolidColorBrush ParseBrush(string hex) =>
        new SolidColorBrush(ParseHex(hex));
}
