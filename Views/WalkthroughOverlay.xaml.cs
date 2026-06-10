using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HomeschoolPlanner.Views;

public partial class WalkthroughOverlay : Window
{
    private readonly MainWindow _owner;
    private int _stepIndex;

    // -----------------------------------------------------------------------
    // Step definitions
    // -----------------------------------------------------------------------

    private record WalkthroughStep(
        string? ElementName,   // x:Name of the element to spotlight; null = no spotlight (centered card)
        string  Title,
        string  Message
    );

    private static readonly WalkthroughStep[] Steps =
    [
        new(null,
            "Welcome to Homeschool Planner!",
            "Let's take a quick tour of the key features. Use the buttons below to navigate, or skip at any time."),

        new("MainMenu",
            "Menu Bar",
            "Access School Settings here to configure your school year dates and which days count as school days. You'll also find Resources and Reports here."),

        new("StudentCombo",
            "Student Selector",
            "Switch between students using this dropdown. Click 'Students' next to it to add, edit, or remove students at any time."),

        new("SubjectsBtn",
            "Subjects",
            "Add and manage subjects for the selected student - set names, colors, and which days of the week they appear on the calendar."),

        new("WeekViewBtn",
            "Calendar Views",
            "Switch between Day, Week, and Month views. Day view is where you add lessons and mark subjects complete for a specific day."),

        new(null,
            "You're all set!",
            "Click any day on the calendar to open Day view and start adding lessons. You can re-open this tour any time from File > Preferences."),
    ];

    // -----------------------------------------------------------------------
    // Constructor & lifetime
    // -----------------------------------------------------------------------

    public WalkthroughOverlay(MainWindow owner)
    {
        InitializeComponent();
        _owner = owner;
        Owner  = owner;

        // Keep overlay aligned if the main window moves or resizes
        owner.LocationChanged += (_, _) => PositionToOwner();
        owner.SizeChanged     += (_, _) => { PositionToOwner(); RenderStep(); };
    }

    /// <summary>Show the overlay and start from step 0.</summary>
    public void Begin()
    {
        _stepIndex = 0;
        Show();
        // PositionToOwner + RenderStep happen in OnContentRendered once layout is ready
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        PositionToOwner();
        RenderStep();
    }

    // -----------------------------------------------------------------------
    // Positioning
    // -----------------------------------------------------------------------

    // ── Manual alignment tweaks ────────────────────────────────────────────
    // If the overlay is slightly off (e.g. shifted by a few pixels), adjust
    // these two constants. Positive X shifts right; positive Y shifts down.
    private const double OffsetX = 0;
    private const double OffsetY = 0;
    // ──────────────────────────────────────────────────────────────────────

    private void PositionToOwner()
    {
        // PointToScreen returns physical pixels; Window.Left/Top expect WPF
        // device-independent units (DIPs). On 100% DPI they match, but on
        // 125%/150%/200% displays they differ - so we convert via the
        // composition target matrix.
        var screenPt = _owner.PointToScreen(new Point(0, 0));

        var source = PresentationSource.FromVisual(_owner);
        if (source?.CompositionTarget != null)
        {
            // TransformFromDevice converts physical pixels -> WPF DIPs
            var dip = source.CompositionTarget.TransformFromDevice.Transform(screenPt);
            Left = dip.X + OffsetX;
            Top  = dip.Y + OffsetY;
        }
        else
        {
            // Fallback for unusual window states
            Left = screenPt.X + OffsetX;
            Top  = screenPt.Y + OffsetY;
        }

        Width  = _owner.ActualWidth;
        Height = _owner.ActualHeight;
    }

    // -----------------------------------------------------------------------
    // Rendering
    // -----------------------------------------------------------------------

    private void RenderStep()
    {
        if (!IsLoaded) return;

        var step = Steps[_stepIndex];

        // Card text
        StepTitle.Text    = step.Title;
        StepMessage.Text  = step.Message;
        StepCounter.Text  = $"{_stepIndex + 1} of {Steps.Length}";
        BackBtn.IsEnabled = _stepIndex > 0;
        NextBtn.Content   = (_stepIndex == Steps.Length - 1) ? "Done" : "Next →";

        // Spotlight
        Rect spotRect = GetSpotRect(step.ElementName);

        // Rebuild backdrop (remove old Path children; keep CalloutCard + CalloutArrow)
        for (int i = RootCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (RootCanvas.Children[i] is Path) RootCanvas.Children.RemoveAt(i);
        }
        RootCanvas.Children.Insert(0, BuildBackdrop(spotRect));

        // Position the callout card + arrow
        PositionCallout(spotRect);
    }

    /// <summary>
    /// Finds the named element in the owner window and returns its rect in
    /// overlay canvas coordinates, with a little padding.
    /// </summary>
    private Rect GetSpotRect(string? elementName)
    {
        if (string.IsNullOrEmpty(elementName)) return Rect.Empty;

        if (_owner.FindName(elementName) is not FrameworkElement el || !el.IsVisible)
            return Rect.Empty;

        var screenPt = el.PointToScreen(new Point(0, 0));
        var localPt  = RootCanvas.PointFromScreen(screenPt);

        const double pad = 10;
        return new Rect(
            localPt.X - pad,
            localPt.Y - pad,
            el.ActualWidth  + pad * 2,
            el.ActualHeight + pad * 2);
    }

    /// <summary>
    /// Builds the dark backdrop Path. When a spotlight rect is provided a
    /// transparent hole is punched so the target element shows through.
    /// </summary>
    private Path BuildBackdrop(Rect spotRect)
    {
        var outer = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));

        Geometry finalGeom;
        if (spotRect.IsEmpty)
        {
            finalGeom = outer;
        }
        else
        {
            // Rounded spotlight hole
            var inner = new RectangleGeometry(spotRect, 8, 8);
            finalGeom = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
        }

        return new Path
        {
            Data = finalGeom,
            Fill = new SolidColorBrush(Color.FromArgb(190, 0, 0, 0))
        };
    }

    /// <summary>
    /// Places the callout card near the spotlight rect. Prefers below, falls
    /// back to above, then right, then left to stay within the window bounds.
    /// </summary>
    private void PositionCallout(Rect spotRect)
    {
        // Force a layout pass so we have accurate card dimensions
        CalloutCard.Measure(new Size(310, double.PositiveInfinity));
        double cardW = Math.Max(CalloutCard.DesiredSize.Width, 310);
        double cardH = CalloutCard.DesiredSize.Height;
        if (cardH < 1) cardH = 160;

        double cW = ActualWidth;
        double cH = ActualHeight;

        double cardX, cardY;
        string arrowSide;

        const double gap    = 18;
        const double margin = 14;

        if (spotRect.IsEmpty)
        {
            // Centered card, no arrow
            cardX = (cW - cardW) / 2;
            cardY = (cH - cardH) / 2;
            arrowSide = "none";
        }
        else if (spotRect.Bottom + gap + cardH < cH - margin)
        {
            // Below spotlight
            cardY     = spotRect.Bottom + gap;
            cardX     = Math.Clamp(spotRect.Left + (spotRect.Width - cardW) / 2, margin, cW - cardW - margin);
            arrowSide = "top";
        }
        else if (spotRect.Top - gap - cardH > margin)
        {
            // Above spotlight
            cardY     = spotRect.Top - gap - cardH;
            cardX     = Math.Clamp(spotRect.Left + (spotRect.Width - cardW) / 2, margin, cW - cardW - margin);
            arrowSide = "bottom";
        }
        else if (spotRect.Right + gap + cardW < cW - margin)
        {
            // Right of spotlight
            cardX     = spotRect.Right + gap;
            cardY     = Math.Clamp(spotRect.Top + (spotRect.Height - cardH) / 2, margin, cH - cardH - margin);
            arrowSide = "left";
        }
        else
        {
            // Left of spotlight
            cardX     = spotRect.Left - gap - cardW;
            cardY     = Math.Clamp(spotRect.Top + (spotRect.Height - cardH) / 2, margin, cH - cardH - margin);
            arrowSide = "right";
        }

        Canvas.SetLeft(CalloutCard, cardX);
        Canvas.SetTop(CalloutCard,  cardY);

        DrawArrow(arrowSide, cardX, cardY, cardW, cardH, spotRect);
    }

    /// <summary>Draws a small triangle connecting the callout edge to the spotlight.</summary>
    private void DrawArrow(string side, double cx, double cy, double cw, double ch, Rect spot)
    {
        if (side == "none" || spot.IsEmpty)
        {
            CalloutArrow.Visibility = Visibility.Collapsed;
            return;
        }

        CalloutArrow.Visibility = Visibility.Visible;
        const double s = 11; // half-base

        double mx = Math.Clamp(spot.Left + spot.Width  / 2, cx + s + 4, cx + cw - s - 4);
        double my = Math.Clamp(spot.Top  + spot.Height / 2, cy + s + 4, cy + ch - s - 4);

        CalloutArrow.Points = side switch
        {
            "top"    => [new(mx - s, cy),       new(mx + s, cy),       new(mx, cy - s)],
            "bottom" => [new(mx - s, cy + ch),  new(mx + s, cy + ch),  new(mx, cy + ch + s)],
            "left"   => [new(cx, my - s),        new(cx, my + s),        new(cx - s, my)],
            _        => [new(cx + cw, my - s),   new(cx + cw, my + s),   new(cx + cw + s, my)],
        };
    }

    // -----------------------------------------------------------------------
    // Button handlers
    // -----------------------------------------------------------------------

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex >= Steps.Length - 1)
            Dismiss();
        else
        {
            _stepIndex++;
            RenderStep();
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex > 0)
        {
            _stepIndex--;
            RenderStep();
        }
    }

    private void Skip_Click(object sender, RoutedEventArgs e) => Dismiss();

    private void Dismiss()
    {
        WalkthroughCompleted?.Invoke(this, EventArgs.Empty);
        Close();
    }

    // -----------------------------------------------------------------------
    // Event raised when the user finishes or skips - caller saves the flag
    // -----------------------------------------------------------------------
    public event EventHandler? WalkthroughCompleted;
}
