using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Views;

// Month view: a calendar grid where each day shows colored dots for scheduled subjects.
// Clicking a day navigates the parent window to Day view for that date.
public partial class MonthView : UserControl
{
    private readonly DatabaseService _db;
    private List<Student> _students;
    private DateTime _currentMonth;

    public MonthView(DatabaseService db, Student student) : this(db, new List<Student> { student }) { }

    public MonthView(DatabaseService db, List<Student> students)
    {
        InitializeComponent();
        _db = db;
        _students = students;
    }

    public void SetStudents(List<Student> students)
    {
        _students = students;
        Render();
    }

    public void LoadMonth(DateTime date)
    {
        _currentMonth = new DateTime(date.Year, date.Month, 1);
        Render();
    }

    // Allow the parent (MainWindow) to subscribe to day-click events
    public event Action<DateTime>? DayClicked;

    private void Render()
    {
        CalendarGrid.Children.Clear();

        var firstDay = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
        var lastDay  = firstDay.AddMonths(1).AddDays(-1);

        // Load entries for all students for this whole month
        var startStr = firstDay.ToString("yyyy-MM-dd");
        var endStr   = lastDay.ToString("yyyy-MM-dd");

        // Map: date string -> list of (subjectColor) for dots
        var dayDots = new Dictionary<string, List<Color>>();

        foreach (var student in _students)
        {
            var subjects = _db.GetSubjects(student.Id);
            // For each day in the month, add dots for scheduled subjects
            for (var d = firstDay; d <= lastDay; d = d.AddDays(1))
            {
                var dateStr = d.ToString("yyyy-MM-dd");
                var daySubjects = subjects.Where(s => s.IsScheduledOn(d)).ToList();
                if (daySubjects.Count == 0) continue;

                if (!dayDots.ContainsKey(dateStr))
                    dayDots[dateStr] = new List<Color>();

                // Add up to 3 dots per student (first 3 subjects' colors)
                foreach (var sub in daySubjects.Take(3))
                    dayDots[dateStr].Add(ParseHexColor(sub.Color));
            }
        }

        // How many rows does this month need? Offset the first day to Monday-start
        int startOffset = ((int)firstDay.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        int totalCells  = startOffset + lastDay.Day;
        int rows        = (int)Math.Ceiling(totalCells / 7.0);

        CalendarGrid.Rows = rows;

        // Compute theme brushes once for this render pass
        var accentColor = ThemeColors.Accent;
        var todayBg   = new SolidColorBrush(Color.FromArgb(40, accentColor.R, accentColor.G, accentColor.B));
        var surfaceBg = ThemeColors.SurfaceBrush;
        var borderBr  = ThemeColors.BorderBrush;
        var hoverBg   = Application.Current.Resources["HoverBrush"] as SolidColorBrush
                        ?? new SolidColorBrush(Colors.LightGray);

        // Fill blank cells before month start
        for (int i = 0; i < startOffset; i++)
            CalendarGrid.Children.Add(new Border { Background = Brushes.Transparent });

        // Day cells
        for (int day = 1; day <= lastDay.Day; day++)
        {
            var date    = new DateTime(_currentMonth.Year, _currentMonth.Month, day);
            var dateStr = date.ToString("yyyy-MM-dd");
            var isToday = date.Date == DateTime.Today;
            var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

            dayDots.TryGetValue(dateStr, out var dots);

            var cellBg = isToday ? todayBg : surfaceBg;
            var cell = new Border
            {
                Background      = cellBg,
                BorderBrush     = borderBr,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding         = new Thickness(6, 4, 6, 4),
                Cursor          = Cursors.Hand
            };

            var cellContent = new StackPanel();

            // Day number
            var dayLabel = new TextBlock
            {
                Text       = day.ToString(),
                FontSize   = 13,
                FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isToday
                    ? ThemeColors.AccentBrush
                    : isWeekend
                        ? ThemeColors.TextSecondaryBrush
                        : ThemeColors.TextPrimaryBrush,
                Margin = new Thickness(0, 0, 0, 4)
            };
            cellContent.Children.Add(dayLabel);

            // Colored subject dots
            if (dots != null && dots.Count > 0)
            {
                var dotRow = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var dotColor in dots.Take(6))
                {
                    dotRow.Children.Add(new Border
                    {
                        Width        = 8,
                        Height       = 8,
                        CornerRadius = new CornerRadius(4),
                        Background   = new SolidColorBrush(dotColor),
                        Margin       = new Thickness(0, 0, 2, 2)
                    });
                }
                cellContent.Children.Add(dotRow);
            }

            cell.Child = cellContent;

            var capturedDate = date;
            var cap_bg = cellBg;
            cell.MouseLeftButtonUp += (s, e) => DayClicked?.Invoke(capturedDate);
            cell.MouseEnter += (s, e) => ((Border)s).Background = hoverBg;
            cell.MouseLeave += (s, e) => ((Border)s).Background = cap_bg;

            CalendarGrid.Children.Add(cell);
        }

        // Fill remaining cells to complete the grid
        int remaining = (rows * 7) - startOffset - lastDay.Day;
        for (int i = 0; i < remaining; i++)
            CalendarGrid.Children.Add(new Border
            {
                Background      = ThemeColors.BackgroundBrush,
                BorderBrush     = borderBr,
                BorderThickness = new Thickness(0, 0, 1, 1)
            });
    }

    private static Color ParseHexColor(string hex)
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
}
