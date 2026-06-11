using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Dialogs;

public partial class PreferencesDialog : Window
{
    private readonly DatabaseService _db;

    private static readonly string[] AvailableFonts =
    {
        "Segoe UI", "Arial", "Calibri", "Comic Sans MS",
        "Georgia", "Times New Roman", "Trebuchet MS", "Verdana"
    };

    public PreferencesDialog(DatabaseService db)
    {
        InitializeComponent();
        _db = db;

        // Populate font list
        FontCombo.ItemsSource   = AvailableFonts;
        FontCombo.SelectedItem  = AppState.Settings.FontFamily;
        if (FontCombo.SelectedIndex < 0) FontCombo.SelectedIndex = 0;

        // Wire swatch pickers for custom colors
        CustomPrimarySwatches.Children.Add(ColorPickerHelper.BuildSwatchPanel(CustomPrimaryBox, CustomPrimaryPreview));
        CustomBgSwatches.Children.Add(ColorPickerHelper.BuildSwatchPanel(CustomBgBox, CustomBgPreview));
        CustomFontSwatches.Children.Add(ColorPickerHelper.BuildSwatchPanel(CustomFontBox, CustomFontPreview));

        // Load current settings
        var s = AppState.Settings;
        switch (s.Theme)
        {
            case "Dark":       RadioDark.IsChecked      = true; break;
            case "Pride":      RadioPride.IsChecked     = true; break;
            case "Pride Dark": RadioPrideDark.IsChecked = true; break;
            case "Cherry":     RadioCherry.IsChecked    = true; break;
            case "Custom":     RadioCustom.IsChecked    = true; break;
            default:           RadioLight.IsChecked     = true; break;
        }
        switch (s.FontSize)
        {
            case "Small": RadioSmall.IsChecked = true; break;
            case "Large": RadioLarge.IsChecked = true; break;
            default:      RadioMedium.IsChecked = true; break;
        }
        CustomPrimaryBox.Text = s.CustomPrimaryColor;
        CustomBgBox.Text      = s.CustomSecondaryColor;
        CustomFontBox.Text    = s.CustomFontColor;

        ShowGradeTemplateCheck.IsChecked = s.ShowGradeTemplatePrompt;
    }

    private void Theme_Changed(object sender, RoutedEventArgs e)
    {
        if (CustomSection == null) return;
        CustomSection.Visibility = RadioCustom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CustomColor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box) return;
        var preview = (string)box.Tag switch
        {
            "Primary"    => CustomPrimaryPreview,
            "Background" => CustomBgPreview,
            "Font"       => CustomFontPreview,
            _            => null
        };
        if (preview == null) return;
        try
        {
            var hex = box.Text.Trim().TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                preview.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
            }
        }
        catch { }
    }

    private void Font_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Live preview: update FontCombo's own font
        if (FontCombo.SelectedItem is string fontName)
            FontCombo.FontFamily = new FontFamily(fontName);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var s = AppState.Settings;

        s.Theme = true switch
        {
            _ when RadioDark.IsChecked      == true => "Dark",
            _ when RadioPride.IsChecked     == true => "Pride",
            _ when RadioPrideDark.IsChecked == true => "Pride Dark",
            _ when RadioCherry.IsChecked    == true => "Cherry",
            _ when RadioCustom.IsChecked    == true => "Custom",
            _                                     => "Light"
        };

        s.FontSize = true switch
        {
            _ when RadioSmall.IsChecked == true => "Small",
            _ when RadioLarge.IsChecked == true => "Large",
            _                                   => "Medium"
        };

        s.FontFamily               = FontCombo.SelectedItem as string ?? "Segoe UI";
        s.CustomPrimaryColor       = NormalizeHex(CustomPrimaryBox.Text);
        s.CustomSecondaryColor     = NormalizeHex(CustomBgBox.Text);
        s.CustomFontColor          = NormalizeHex(CustomFontBox.Text);
        s.ShowGradeTemplatePrompt  = ShowGradeTemplateCheck.IsChecked == true;

        _db.SaveSettings(s);
        ThemeManager.Apply(s);

        DialogResult = true;
    }

    private void DeleteAllData_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Are you sure? This cannot be reversed.\n\nAll students, subjects, and lesson history will be permanently deleted.",
            "Delete All Data",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            _db.DeleteAllData();
            MessageBox.Show("All data has been deleted.", "Done",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static string NormalizeHex(string hex)
    {
        hex = hex.Trim();
        return hex.StartsWith("#") ? hex : "#" + hex;
    }
}
