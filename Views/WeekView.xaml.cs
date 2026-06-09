using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Dialogs;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Views;

// Planbook-style week view.
//
// Layout:
//   - One column per school day (configurable via School Settings)
//   - Each column shows colored subject blocks stacked vertically
//   - Only subjects whose schedule includes that day appear in that column
//   - A "+" button at the bottom of each column opens the AddClassDialog
//   - Clicking a subject block opens the LessonEditDialog
//
// Multi-student mode: pass multiple students to show columns grouped by student.
public partial class WeekView : UserControl
{
    private readonly DatabaseService _db;
    private List<Student> _students;
    private DateTime _weekStart;

    // ISO day number (Mon=1...Sun=7) -> display name
    private static readonly Dictionary<int, string> DayFullName = new()
    {
        {1, "Monday"}, {2, "Tuesday"}, {3, "Wednesday"},
        {4, "Thursday"}, {5, "Friday"}, {6, "Saturday"}, {7, "Sunday"}
    };
    private static readonly Dictionary<int, string> DayShortName = new()
    {
        {1, "Mon"}, {2, "Tue"}, {3, "Wed"},
        {4, "Thu"}, {5, "Fri"}, {6, "Sat"}, {7, "Sun"}
    };

    // Offset from Monday for each ISO day number
    private static int DayOffset(int isoDay) => (isoDay - 1 + 7) % 7; // Mon=0, Tue=1, ... Sun=6

    public WeekView(DatabaseService db, Student student) : this(db, new List<Student> { student }) { }

    public WeekView(DatabaseService db, List<Student> students)
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

    public void LoadWeek(DateTime date)
    {
        _weekStart = GetMonday(date);
        Render();
    }

    private void Render()
    {
        // Read school days from settings (ISO: Mon=1...Sun=7)
        var schoolDays = AppState.Settings.SchoolDayNumbers;
        if (schoolDays.Length == 0) schoolDays = new[] { 1, 2, 3, 4, 5 }; // fallback to Mon-Fri

        bool multiStudent = _students.Count > 1;

        // Pre-load all subjects and entries for the full 7-day week range
        var allSubjects = _students.ToDictionary(
            s => s.Id,
            s => _db.GetSubjects(s.Id));

        var startStr = _weekStart.ToString("yyyy-MM-dd");
        var endStr   = _weekStart.AddDays(6).ToString("yyyy-MM-dd"); // full week
        var allEntries = _students.ToDictionary(
            s => s.Id,
            s => _db.GetEntriesForRange(s.Id, startStr, endStr)
                    .ToDictionary(e => $"{e.SubjectId}:{e.LessonDate}", e => e));

        // --- Build header row ---
        HeaderRow.ColumnDefinitions.Clear();
        HeaderRow.Children.Clear();

        int headerColIdx = 0;
        foreach (var dayNum in schoolDays)
        {
            var dayDate = _weekStart.AddDays(DayOffset(dayNum));
            var isToday = dayDate.Date == DateTime.Today;

            for (int si = 0; si < _students.Count; si++)
                HeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var headerBorder = new Border
            {
                Background      = isToday ? new SolidColorBrush(Color.FromRgb(0xEE, 0xF4, 0xFF)) : Brushes.White,
                BorderBrush     = new SolidColorBrush(Color.FromRgb(0xD8, 0xDC, 0xE6)),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding         = new Thickness(10, 8, 10, 8)
            };

            var headerContent = new StackPanel();
            headerContent.Children.Add(new TextBlock
            {
                Text       = DayFullName.GetValueOrDefault(dayNum, ""),
                FontSize   = 11,
                Foreground = isToday
                    ? new SolidColorBrush(Color.FromRgb(0x4A, 0x7C, 0xB5))
                    : new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
            });
            headerContent.Children.Add(new TextBlock
            {
                Text       = dayDate.ToString("MMM d"),
                FontSize   = 15,
                FontWeight = isToday ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = isToday
                    ? new SolidColorBrush(Color.FromRgb(0x4A, 0x7C, 0xB5))
                    : new SolidColorBrush(Color.FromRgb(0x1C, 0x23, 0x33))
            });
            headerBorder.Child = headerContent;

            Grid.SetColumn(headerBorder, headerColIdx * _students.Count);
            Grid.SetColumnSpan(headerBorder, _students.Count);
            HeaderRow.Children.Add(headerBorder);
            headerColIdx++;
        }

        // --- Build day columns ---
        DayColumnsGrid.ColumnDefinitions.Clear();
        DayColumnsGrid.Children.Clear();
        DayColumnsGrid.RowDefinitions.Clear();
        DayColumnsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        int colIndex = 0;
        foreach (var dayNum in schoolDays)
        {
            var dayDate = _weekStart.AddDays(DayOffset(dayNum));
            var isToday = dayDate.Date == DateTime.Today;
            var dateStr = dayDate.ToString("yyyy-MM-dd");

            for (int si = 0; si < _students.Count; si++)
            {
                DayColumnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var student  = _students[si];
                var subjects = allSubjects[student.Id];
                var entryMap = allEntries[student.Id];

                // Subjects scheduled for this day
                var daySubjects = subjects.Where(s => s.IsScheduledOn(dayDate)).ToList();

                // Outer column border
                var colBorder = new Border
                {
                    Background      = isToday ? new SolidColorBrush(Color.FromRgb(0xF8, 0xFB, 0xFF)) : Brushes.White,
                    BorderBrush     = new SolidColorBrush(Color.FromRgb(0xD8, 0xDC, 0xE6)),
                    BorderThickness = new Thickness(0, 0, 1, 0)
                };

                var colStack = new StackPanel { Margin = new Thickness(6, 6, 6, 6) };

                // Student sub-header in multi-student mode
                if (multiStudent)
                {
                    var studentColor = ParseHexColor(student.Color);
                    colStack.Children.Add(new Border
                    {
                        Background   = new SolidColorBrush(studentColor) { Opacity = 0.15 },
                        CornerRadius = new CornerRadius(4),
                        Padding      = new Thickness(6, 3, 6, 3),
                        Margin       = new Thickness(0, 0, 0, 6),
                        Child        = new TextBlock
                        {
                            Text       = student.Name,
                            FontSize   = 11,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(studentColor)
                        }
                    });
                }

                // Subject blocks
                foreach (var subject in daySubjects)
                {
                    var subjectColor = ParseHexColor(subject.Color);
                    var key = $"{subject.Id}:{dateStr}";
                    entryMap.TryGetValue(key, out var entry);

                    var capturedSubject = subject;
                    var capturedStudent = student;
                    var capturedEntry   = entry;
                    var capturedDate    = dayDate;

                    var block = BuildSubjectBlock(subject, entry, subjectColor);
                    block.Margin = new Thickness(0, 0, 0, 4);
                    block.Cursor = Cursors.Hand;
                    block.MouseLeftButtonUp += (s, e) =>
                    {
                        var dlg = new LessonEditDialog(_db, capturedStudent, capturedSubject, capturedDate, capturedEntry);
                        dlg.Owner = Window.GetWindow(this);
                        if (dlg.ShowDialog() == true)
                            Render();
                        e.Handled = true;
                    };

                    colStack.Children.Add(block);
                }

                // "+" Add Class button at the bottom of each column
                var addBtn = new Border
                {
                    BorderBrush     = new SolidColorBrush(Color.FromRgb(0xD8, 0xDC, 0xE6)),
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(4),
                    Padding         = new Thickness(0, 6, 0, 6),
                    Cursor          = Cursors.Hand,
                    Margin          = new Thickness(0, 4, 0, 0),
                    Background      = Brushes.Transparent
                };
                addBtn.Child = new TextBlock
                {
                    Text               = "+ Add Class",
                    FontSize           = 12,
                    Foreground         = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAF)),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var capturedAddStudent = student;
                var capturedAddDate    = dayDate;
                addBtn.MouseLeftButtonUp += (s, e) =>
                {
                    OnAddClass(capturedAddStudent, capturedAddDate);
                    e.Handled = true;
                };
                addBtn.MouseEnter += (s, e) =>
                    ((Border)s).Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF4, 0xFB));
                addBtn.MouseLeave += (s, e) =>
                    ((Border)s).Background = Brushes.Transparent;

                colStack.Children.Add(addBtn);
                colBorder.Child = colStack;

                Grid.SetColumn(colBorder, colIndex);
                DayColumnsGrid.Children.Add(colBorder);
                colIndex++;
            }
        }
    }

    // Builds a single colored subject block (like the colored pills in Planbook)
    private static Border BuildSubjectBlock(Subject subject, LessonEntry? entry, Color subjectColor)
    {
        var block = new Border
        {
            Background   = new SolidColorBrush(subjectColor),
            CornerRadius = new CornerRadius(4),
            Padding      = new Thickness(10, 7, 10, 7),
            MinHeight    = 36
        };

        var panel = new StackPanel();

        // Subject name
        var nameText = new TextBlock
        {
            Text         = subject.Name,
            FontSize     = 13,
            FontWeight   = FontWeights.SemiBold,
            Foreground   = PickTextColor(subjectColor),
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(nameText);

        // Lesson title (if any content exists)
        if (entry != null && !string.IsNullOrWhiteSpace(entry.Title))
        {
            var titleText = new TextBlock
            {
                Text            = entry.Title,
                FontSize        = 11,
                Foreground      = PickTextColor(subjectColor) is SolidColorBrush b
                    ? new SolidColorBrush(Color.FromArgb(0xCC, b.Color.R, b.Color.G, b.Color.B))
                    : Brushes.White,
                TextWrapping    = TextWrapping.Wrap,
                Margin          = new Thickness(0, 2, 0, 0),
                TextDecorations = entry.IsComplete ? TextDecorations.Strikethrough : null
            };
            panel.Children.Add(titleText);
        }

        // Small checkmark if complete
        if (entry?.IsComplete == true)
        {
            panel.Children.Add(new TextBlock
            {
                Text     = "✓ Done",
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.White) { Opacity = 0.85 },
                Margin   = new Thickness(0, 2, 0, 0)
            });
        }

        block.Child = panel;
        return block;
    }

    private void OnAddClass(Student student, DateTime date)
    {
        var subjects = _db.GetSubjects(student.Id, activeOnly: false);
        var dlg = new AddClassDialog(_db, student, subjects, date);
        dlg.Owner = Window.GetWindow(this);
        if (dlg.ShowDialog() == true)
            Render();
    }

    // Pick white or dark text depending on background luminance
    private static Brush PickTextColor(Color bg)
    {
        double luminance = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255;
        return luminance > 0.55
            ? new SolidColorBrush(Color.FromRgb(0x1C, 0x23, 0x33))
            : Brushes.White;
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

    private static DateTime GetMonday(DateTime date)
    {
        int diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff).Date;
    }
}
