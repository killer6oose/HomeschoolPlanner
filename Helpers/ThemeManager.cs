using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Helpers;

public static class ThemeManager
{
    private static string _currentTheme = "Light";

    /// <summary>True while any Pride variant is active - drives rainbow gradient effects in code-built views.</summary>
    public static bool IsPride  => _currentTheme == "Pride" || _currentTheme == "Pride Dark";

    /// <summary>True while the Cherry theme is active - used to apply image background.</summary>
    public static bool IsCherry => _currentTheme == "Cherry";

    public static void Apply(AppSettings settings)
    {
        _currentTheme = settings.Theme;

        var (bg, surface, accent, accentHover, border, textPrimary, textSecondary) = ResolveColors(settings);

        ThemeColors.Update(bg, surface, accent, accentHover, border, textPrimary, textSecondary);

        var res = Application.Current.Resources;

        res["BackgroundBrush"]       = new SolidColorBrush(bg);
        res["DialogBackgroundBrush"] = new SolidColorBrush(bg);  // always solid - overridden for Cherry below
        res["AccentBrush"]           = new SolidColorBrush(accent);
        res["AccentHoverBrush"]   = new SolidColorBrush(accentHover);
        res["SurfaceBrush"]       = new SolidColorBrush(surface);
        res["BorderBrush"]        = new SolidColorBrush(border);
        res["TextPrimaryBrush"]   = new SolidColorBrush(textPrimary);
        res["TextSecondaryBrush"] = new SolidColorBrush(textSecondary);

        // Cherry theme: replace solid bg with the image brush and use
        // a semi-transparent surface so cards stay readable over the pattern
        if (settings.Theme == "Cherry")
        {
            try
            {
                var uri    = new Uri("pack://application:,,,/Assets/pinkBackground.png");
                var bitmap = new BitmapImage(uri);
                res["BackgroundBrush"] = new ImageBrush(bitmap)
                {
                    Stretch    = Stretch.UniformToFill,
                    Opacity    = 0.90,
                    TileMode   = TileMode.None,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };
            }
            catch { /* leave solid pink fallback */ }

            // Dialogs always get a solid background so content stays readable
            res["DialogBackgroundBrush"] = new SolidColorBrush(Parse("#FFF0F5"));
        }

        // Hover/press computed from background brightness
        float brightness = bg.R * 0.299f + bg.G * 0.587f + bg.B * 0.114f;
        bool  isDark     = brightness < 128;
        Color hover      = isDark ? Lighten(surface, 0.18f) : Darken(surface, 0.05f);
        Color pressed    = isDark ? Lighten(surface, 0.30f) : Darken(surface, 0.10f);
        res["HoverBrush"]   = new SolidColorBrush(hover);
        res["PressedBrush"] = new SolidColorBrush(pressed);

        // Pride-specific: show/hide animated gradient overlay on primary buttons,
        // and supply a rainbow border brush for secondary (nav) buttons.
        bool isPride = settings.Theme == "Pride" || settings.Theme == "Pride Dark";
        res["PrideGradientVisibility"] = isPride ? Visibility.Visible : Visibility.Collapsed;
        res["NavButtonBorderBrush"]    = isPride ? (Brush)BuildRainbowGradient() : new SolidColorBrush(border);

        // Override SystemColors so WPF built-in Menu/ComboBox/ListBox popups use theme colors.
        res[SystemColors.MenuBrushKey]          = new SolidColorBrush(surface);
        res[SystemColors.MenuTextBrushKey]       = new SolidColorBrush(textPrimary);
        res[SystemColors.MenuHighlightBrushKey]  = new SolidColorBrush(accent);
        res[SystemColors.WindowBrushKey]         = new SolidColorBrush(surface);
        res[SystemColors.WindowTextBrushKey]     = new SolidColorBrush(textPrimary);
        res[SystemColors.HighlightBrushKey]      = new SolidColorBrush(accent);
        res[SystemColors.HighlightTextBrushKey]  = new SolidColorBrush(Colors.White);
        res[SystemColors.ControlBrushKey]        = new SolidColorBrush(surface);
        res[SystemColors.ControlTextBrushKey]    = new SolidColorBrush(textPrimary);
        res[SystemColors.ControlLightBrushKey]   = new SolidColorBrush(border);
        res[SystemColors.GrayTextBrushKey]       = new SolidColorBrush(textSecondary);
        res[SystemColors.InactiveSelectionHighlightBrushKey]     = new SolidColorBrush(hover);
        res[SystemColors.InactiveSelectionHighlightTextBrushKey] = new SolidColorBrush(textPrimary);

        // Calendar popup uses ActiveCaption for today's highlight and InactiveCaption for hover
        res[SystemColors.ActiveCaptionBrushKey]      = new SolidColorBrush(accent);
        res[SystemColors.ActiveCaptionTextBrushKey]  = new SolidColorBrush(Colors.White);
        res[SystemColors.InactiveCaptionBrushKey]    = new SolidColorBrush(hover);
        res[SystemColors.InactiveCaptionTextBrushKey]= new SolidColorBrush(textPrimary);

        // Danger Zone card - tinted surface + border that stays readable in every theme
        var (dangerSurface, dangerBorder) = settings.Theme switch
        {
            "Dark"       => (Color.FromRgb(0x2D, 0x15, 0x15), Color.FromRgb(0x8B, 0x33, 0x33)),
            "Cherry"     => (Color.FromRgb(0xFF, 0xE8, 0xEE), Color.FromRgb(0xE8, 0x8A, 0xAA)),
            "Pride"      => (Color.FromRgb(0xFD, 0xF0, 0xFF), Color.FromRgb(0xCC, 0x88, 0xEE)),
            "Pride Dark" => (Color.FromRgb(0x2A, 0x15, 0x28), Color.FromRgb(0x88, 0x33, 0x77)),
            _            => (Color.FromRgb(0xFF, 0xF5, 0xF5), Color.FromRgb(0xFF, 0xCC, 0xCC))
        };
        res["DangerSurfaceBrush"] = new SolidColorBrush(dangerSurface);
        res["DangerBorderBrush"]  = new SolidColorBrush(dangerBorder);

        // Font resources
        res["AppFontFamily"] = new FontFamily(settings.FontFamily);
        res["AppFontSize"]   = settings.FontSizeValue;

        // Push font onto all currently open windows so code-built elements inherit correctly
        var ff   = new FontFamily(settings.FontFamily);
        var size = settings.FontSizeValue;
        foreach (Window w in Application.Current.Windows)
        {
            w.FontFamily = ff;
            w.FontSize   = size;
        }
    }

    /// <summary>
    /// Full-spectrum rainbow gradient (red -> violet). Used for nav button borders,
    /// today-date column outlines, and other non-animated pride accents.
    /// The animated 200%-wide sliding version for primary buttons lives in the XAML template.
    /// </summary>
    public static LinearGradientBrush BuildRainbowGradient()
    {
        var g = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        g.GradientStops.Add(new GradientStop(Color.FromRgb(0xE0, 0x50, 0x50), 0.00));
        g.GradientStops.Add(new GradientStop(Color.FromRgb(0xD4, 0x83, 0x2A), 0.20));
        g.GradientStops.Add(new GradientStop(Color.FromRgb(0xA8, 0xA8, 0x28), 0.40));
        g.GradientStops.Add(new GradientStop(Color.FromRgb(0x28, 0xA0, 0x60), 0.60));
        g.GradientStops.Add(new GradientStop(Color.FromRgb(0x28, 0x64, 0xD0), 0.80));
        g.GradientStops.Add(new GradientStop(Color.FromRgb(0x8C, 0x28, 0xC8), 1.00));
        return g;
    }

    private static (Color bg, Color surface, Color accent, Color accentHover,
                    Color border, Color textPrimary, Color textSecondary)
        ResolveColors(AppSettings s)
    {
        return s.Theme switch
        {
            // Cherry blossom / pink gingham
            "Cherry" => (
                bg:            Parse("#FFF0F5"),                    // solid fallback if image fails
                surface:       Color.FromArgb(218, 255, 245, 248), // 85% opaque so image bleeds through cards
                accent:        Parse("#C2185B"),                    // deep rose/cherry for buttons
                accentHover:   Parse("#A3154D"),
                border:        Parse("#F8BBD9"),                    // light pink border
                textPrimary:   Parse("#3B0A1E"),                    // deep plum
                textSecondary: Parse("#9C4A6E")                     // muted rose
            ),
            "Dark" => (
                bg:            Parse("#22283A"),   // slightly lighter than original #1A1F2E
                surface:       Parse("#2A3245"),
                accent:        Parse("#5B9AD5"),
                accentHover:   Parse("#4B8AC5"),
                border:        Parse("#3A4252"),
                textPrimary:   Parse("#E8EAF0"),
                textSecondary: Parse("#8A93A8")
            ),
            // Muted off-white background; rainbow comes from button overlays and borders
            "Pride" => (
                bg:            Parse("#F8F7FF"),   // slight violet tint
                surface:       Colors.White,
                accent:        Parse("#7B2FBE"),   // vibrant purple
                accentHover:   Parse("#6A28A0"),
                border:        Parse("#E0D8F0"),   // soft purple-gray
                textPrimary:   Parse("#1A0033"),   // deep purple-black
                textSecondary: Parse("#7260A0")    // muted purple
            ),
            // Pride palette on a dark background - not as dark as "Dark" theme
            "Pride Dark" => (
                bg:            Parse("#1E1A2E"),   // deep purple-black
                surface:       Parse("#2A2442"),   // dark purple surface
                accent:        Parse("#A06AE8"),   // brighter purple to pop on dark bg
                accentHover:   Parse("#9058D8"),
                border:        Parse("#3D3560"),   // dark purple border
                textPrimary:   Parse("#E8E0F5"),   // light lavender
                textSecondary: Parse("#9080C0")    // muted purple
            ),
            "Custom" => (
                bg:            Parse(s.CustomSecondaryColor),
                surface:       Colors.White,
                accent:        Parse(s.CustomPrimaryColor),
                accentHover:   Darken(Parse(s.CustomPrimaryColor), 0.12f),
                border:        Parse("#D8DCE6"),
                textPrimary:   Parse(s.CustomFontColor),
                textSecondary: Lighten(Parse(s.CustomFontColor), 0.35f)
            ),
            _ => // Light
            (
                bg:            Parse("#F5F6FA"),
                surface:       Colors.White,
                accent:        Parse("#4A7CB5"),
                accentHover:   Parse("#3A6CA5"),
                border:        Parse("#D8DCE6"),
                textPrimary:   Parse("#1C2333"),
                textSecondary: Parse("#6B7280")
            )
        };
    }

    public static Color Parse(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
                return Color.FromRgb(
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16));
        }
        catch { }
        return Color.FromRgb(0x4A, 0x7C, 0xB5);
    }

    private static Color Darken(Color c, float f)
        => Color.FromRgb(
            (byte)Math.Max(0,   c.R - c.R * f),
            (byte)Math.Max(0,   c.G - c.G * f),
            (byte)Math.Max(0,   c.B - c.B * f));

    private static Color Lighten(Color c, float f)
        => Color.FromRgb(
            (byte)Math.Min(255, c.R + (255 - c.R) * f),
            (byte)Math.Min(255, c.G + (255 - c.G) * f),
            (byte)Math.Min(255, c.B + (255 - c.B) * f));
}

// Static snapshot of current theme colors for use in code-built views.
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
