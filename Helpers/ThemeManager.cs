using System.Windows;
using System.Windows.Media;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Helpers;

// Applies a theme by updating named brush resources in Application.Current.Resources.
// Any XAML element using {DynamicResource ...} will auto-update.
// Code-built views (WeekView, DayView, MonthView) read from ThemeColors after being rebuilt.
public static class ThemeManager
{
    public static void Apply(AppSettings settings)
    {
        var (bg, surface, accent, accentHover, border, textPrimary, textSecondary) = ResolveColors(settings);

        ThemeColors.Update(bg, surface, accent, accentHover, border, textPrimary, textSecondary);

        var res = Application.Current.Resources;

        // Update brush resources - {DynamicResource} bindings in XAML update automatically
        SetBrush(res, "AccentBrush",         accent);
        SetBrush(res, "AccentHoverBrush",     accentHover);
        SetBrush(res, "BackgroundBrush",      bg);
        SetBrush(res, "SurfaceBrush",         surface);
        SetBrush(res, "BorderBrush",          border);
        SetBrush(res, "TextPrimaryBrush",     textPrimary);
        SetBrush(res, "TextSecondaryBrush",   textSecondary);

        // Font family and size
        res["AppFontFamily"] = new FontFamily(settings.FontFamily);
        res["AppFontSize"]   = settings.FontSizeValue;

        // Push font onto all open windows so code-built elements pick it up too
        var ff   = new FontFamily(settings.FontFamily);
        var size = settings.FontSizeValue;
        foreach (Window w in Application.Current.Windows)
        {
            w.FontFamily = ff;
            w.FontSize   = size;
        }
    }

    private static (Color bg, Color surface, Color accent, Color accentHover,
                    Color border, Color textPrimary, Color textSecondary)
        ResolveColors(AppSettings s)
    {
        return s.Theme switch
        {
            "Dark" => (
                bg:          Parse("#1A1F2E"),
                surface:     Parse("#252D3D"),
                accent:      Parse("#5B9AD5"),
                accentHover: Parse("#4B8AC5"),
                border:      Parse("#3A4252"),
                textPrimary: Parse("#E8EAF0"),
                textSecondary: Parse("#9AA3AF")
            ),
            "Pride" => (
                bg:          Parse("#FFF5FF"),
                surface:     Parse("#FFFFFF"),
                accent:      Parse("#E040FB"),
                accentHover: Parse("#CC30E5"),
                border:      Parse("#F3E5F5"),
                textPrimary: Parse("#1A0033"),
                textSecondary: Parse("#7B1FA2")
            ),
            "Custom" => (
                bg:          Parse(s.CustomSecondaryColor),
                surface:     Colors.White,
                accent:      Parse(s.CustomPrimaryColor),
                accentHover: Darken(Parse(s.CustomPrimaryColor), 0.12f),
                border:      Parse("#D8DCE6"),
                textPrimary: Parse(s.CustomFontColor),
                textSecondary: Lighten(Parse(s.CustomFontColor), 0.35f)
            ),
            _ => // Light (default)
            (
                bg:          Parse("#F5F6FA"),
                surface:     Colors.White,
                accent:      Parse("#4A7CB5"),
                accentHover: Parse("#3A6CA5"),
                border:      Parse("#D8DCE6"),
                textPrimary: Parse("#1C2333"),
                textSecondary: Parse("#6B7280")
            )
        };
    }

    private static void SetBrush(ResourceDictionary res, string key, Color color)
        => res[key] = new SolidColorBrush(color);

    public static Color Parse(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return Color.FromRgb(r, g, b);
            }
        }
        catch { }
        return Color.FromRgb(0x4A, 0x7C, 0xB5);
    }

    // Darkens a color by a fraction (0-1)
    private static Color Darken(Color c, float fraction)
        => Color.FromRgb(
            (byte)Math.Max(0, c.R - c.R * fraction),
            (byte)Math.Max(0, c.G - c.G * fraction),
            (byte)Math.Max(0, c.B - c.B * fraction));

    // Lightens a color toward white by fraction
    private static Color Lighten(Color c, float fraction)
        => Color.FromRgb(
            (byte)Math.Min(255, c.R + (255 - c.R) * fraction),
            (byte)Math.Min(255, c.G + (255 - c.G) * fraction),
            (byte)Math.Min(255, c.B + (255 - c.B) * fraction));
}

// Static snapshot of current theme colors - read by code-built views (WeekView, DayView, etc.)
// Updated by ThemeManager.Apply() before views are rebuilt.
public static class ThemeColors
{
    public static Color Accent        { get; private set; } = ThemeManager.Parse("#4A7CB5");
    public static Color AccentHover   { get; private set; } = ThemeManager.Parse("#3A6CA5");
    public static Color Background    { get; private set; } = ThemeManager.Parse("#F5F6FA");
    public static Color Surface       { get; private set; } = Colors.White;
    public static Color Border        { get; private set; } = ThemeManager.Parse("#D8DCE6");
    public static Color TextPrimary   { get; private set; } = ThemeManager.Parse("#1C2333");
    public static Color TextSecondary { get; private set; } = ThemeManager.Parse("#6B7280");

    public static SolidColorBrush AccentBrush        => new(Accent);
    public static SolidColorBrush BackgroundBrush    => new(Background);
    public static SolidColorBrush SurfaceBrush       => new(Surface);
    public static SolidColorBrush BorderBrush        => new(Border);
    public static SolidColorBrush TextPrimaryBrush   => new(TextPrimary);
    public static SolidColorBrush TextSecondaryBrush => new(TextSecondary);

    public static void Update(Color bg, Color surface, Color accent, Color accentHover,
                              Color border, Color textPrimary, Color textSecondary)
    {
        Background    = bg;
        Surface       = surface;
        Accent        = accent;
        AccentHover   = accentHover;
        Border        = border;
        TextPrimary   = textPrimary;
        TextSecondary = textSecondary;
    }
}
