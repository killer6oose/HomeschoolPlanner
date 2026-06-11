using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Dialogs;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Views;

public partial class WeekView : UserControl
{
    private readonly DatabaseService _db;
    private List<Student> _students;
    private DateTime _weekStart;

    // Tracks which subject blocks are expanded: "{subjectId}:{dateStr}"
    private readonly HashSet<string> _expandedBlocks = new();

    private static readonly Dictionary<int, string> DayFullName = new()
    {
        {1,"Monday"},{2,"Tuesday"},{3,"Wednesday"},
        {4,"Thursday"},{5,"Friday"},{6,"Saturday"},{7,"Sunday"}
    };

    private static int DayOffset(int isoDay) => (isoDay - 1 + 7) % 7;

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
        var schoolDays = AppState.Settings.SchoolDayNumbers;
        if (schoolDays.Length == 0) schoolDays = new[] { 1, 2, 3, 4, 5 };

        bool multiStudent = _students.Count > 1;

        // Font sizes
        double baseFs  = AppState.Settings.FontSizeValue;
        double fsSmall = baseFs - 2;
        double fsLarge = baseFs + 2;

        // Theme colors
        var surfaceBrush  = ThemeColors.SurfaceBrush;
        var borderBrush   = ThemeColors.BorderBrush;
        var textPrimary   = ThemeColors.TextPrimaryBrush;
        var textSecondary = ThemeColors.TextSecondaryBrush;
        var accentBrush   = ThemeColors.AccentBrush;
        var accentColor   = ThemeColors.Accent;
        // Cherry theme uses a stronger tint so the today column is distinct but the image still shows through.
        // Non-today columns use the surface RGB at ~55% opacity so the cherry image shows at roughly
        // half the visibility level of the today column (today is ~18% opaque -> cherry at 82%;
        // other days at ~55% opaque -> cherry at 45%, which is about half).
        var surfaceColor = ThemeColors.Surface;
        var todayHeaderBg = ThemeManager.IsCherry
            ? new SolidColorBrush(Color.FromArgb(70,  accentColor.R, accentColor.G, accentColor.B))
            : new SolidColorBrush(Color.FromArgb(30,  accentColor.R, accentColor.G, accentColor.B));
        var todayColBg = ThemeManager.IsCherry
            ? new SolidColorBrush(Color.FromArgb(45,  accentColor.R, accentColor.G, accentColor.B))
            : new SolidColorBrush(Color.FromArgb(15,  accentColor.R, accentColor.G, accentColor.B));
        // Default (non-today) cell background - semi-transparent in Cherry so the image shows through
        var colDefaultBg    = ThemeManager.IsCherry
            ? new SolidColorBrush(Color.FromArgb(140, surfaceColor.R, surfaceColor.G, surfaceColor.B))
            : surfaceBrush;
        var headerDefaultBg = ThemeManager.IsCherry
            ? new SolidColorBrush(Color.FromArgb(155, surfaceColor.R, surfaceColor.G, surfaceColor.B))
            : surfaceBrush;

        var allSubjects = _students.ToDictionary(s => s.Id, s => _db.GetSubjects(s.Id));
        var startStr    = _weekStart.ToString("yyyy-MM-dd");
        var endStr      = _weekStart.AddDays(6).ToString("yyyy-MM-dd");
        var allEntries  = _students.ToDictionary(
            s => s.Id,
            s => _db.GetEntriesForRange(s.Id, startStr, endStr)
                    .ToDictionary(e => $"{e.SubjectId}:{e.LessonDate}", e => e));

        // --- Header row ---
        HeaderRow.ColumnDefinitions.Clear();
        HeaderRow.Children.Clear();

        int headerColIdx = 0;
        foreach (var dayNum in schoolDays)
        {
            var dayDate = _weekStart.AddDays(DayOffset(dayNum));
            var isToday = dayDate.Date == DateTime.Today;
            bool prideToday  = isToday && ThemeManager.IsPride;
            bool cherryToday = isToday && ThemeManager.IsCherry;

            for (int si = 0; si < _students.Count; si++)
                HeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var headerBorder = new Border
            {
                Background      = isToday ? todayHeaderBg : headerDefaultBg,
                // Pride: 2px rainbow border. Cherry: 2px accent border. Otherwise: normal right+bottom separator.
                BorderBrush     = prideToday  ? ThemeManager.BuildRainbowGradient()
                                : cherryToday ? accentBrush
                                :               borderBrush,
                BorderThickness = (prideToday || cherryToday) ? new Thickness(2) : new Thickness(0, 0, 1, 1),
                Padding         = new Thickness(10, 8, 10, 8)
            };
            var hContent = new StackPanel();
            hContent.Children.Add(new TextBlock
            {
                Text       = DayFullName.GetValueOrDefault(dayNum, ""),
                FontSize   = fsSmall,
                Foreground = isToday ? accentBrush : textSecondary
            });
            hContent.Children.Add(new TextBlock
            {
                Text       = dayDate.ToString("MMM d"),
                FontSize   = fsLarge,
                FontWeight = isToday ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = isToday ? accentBrush : textPrimary
            });
            headerBorder.Child = hContent;
            Grid.SetColumn(headerBorder, headerColIdx * _students.Count);
            Grid.SetColumnSpan(headerBorder, _students.Count);
            HeaderRow.Children.Add(headerBorder);
            headerColIdx++;
        }

        // --- Day columns ---
        DayColumnsGrid.ColumnDefinitions.Clear();
        DayColumnsGrid.Children.Clear();
        DayColumnsGrid.RowDefinitions.Clear();
        DayColumnsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        int colIndex = 0;
        foreach (var dayNum in schoolDays)
        {
            var dayDate = _weekStart.AddDays(DayOffset(dayNum));
            var isToday      = dayDate.Date == DateTime.Today;
            bool cherryTodayCol = isToday && ThemeManager.IsCherry;
            var dateStr = dayDate.ToString("yyyy-MM-dd");

            for (int si = 0; si < _students.Count; si++)
            {
                DayColumnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var student  = _students[si];
                var subjects = allSubjects[student.Id];
                var entryMap = allEntries[student.Id];
                var daySubjects = subjects.Where(s => s.IsScheduledOn(dayDate)).ToList();

                var colBorder = new Border
                {
                    Background      = isToday ? todayColBg : colDefaultBg,
                    // Cherry today: 2px accent border on left/right/bottom to complete the header's outline
                    BorderBrush     = cherryTodayCol ? accentBrush : borderBrush,
                    BorderThickness = cherryTodayCol  ? new Thickness(2, 0, 2, 2) : new Thickness(0, 0, 1, 0)
                };
                var colStack = new StackPanel { Margin = new Thickness(6, 6, 6, 6) };

                // Student sub-header in multi-student mode (shows Name - Grade)
                if (multiStudent)
                {
                    var sc = ParseHexColor(student.Color);
                    var gradeDisplay = GradeHelper.KeyToDisplay(student.Grade);
                    colStack.Children.Add(new Border
                    {
                        Background   = new SolidColorBrush(sc) { Opacity = 0.15 },
                        CornerRadius = new CornerRadius(4),
                        Padding      = new Thickness(6, 3, 6, 3),
                        Margin       = new Thickness(0, 0, 0, 6),
                        Child        = new TextBlock
                        {
                            Text       = $"{student.Name} - {gradeDisplay}",
                            FontSize   = fsSmall,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(sc)
                        }
                    });
                }

                foreach (var subject in daySubjects)
                {
                    var subjectColor = ParseHexColor(subject.Color);
                    var blockKey     = $"{subject.Id}:{dateStr}";
                    entryMap.TryGetValue(blockKey, out var entry);
                    bool expanded = _expandedBlocks.Contains(blockKey);

                    var cap_subject = subject;
                    var cap_student = student;
                    var cap_entry   = entry;
                    var cap_date    = dayDate;
                    var cap_key     = blockKey;

                    var block = BuildSubjectBlock(
                        subject, entry, subjectColor, expanded,
                        baseFs, fsSmall,
                        // toggle expand
                        () =>
                        {
                            if (_expandedBlocks.Contains(cap_key)) _expandedBlocks.Remove(cap_key);
                            else _expandedBlocks.Add(cap_key);
                            Render();
                        },
                        // mark all complete
                        () =>
                        {
                            var e2 = cap_entry ?? _db.EnsureEntry(cap_subject.Id, cap_student.Id, cap_date.ToString("yyyy-MM-dd"));
                            _db.SetEntryComplete(e2.Id, !e2.IsComplete);
                            Render();
                        },
                        // delete this occurrence
                        () =>
                        {
                            var res = MessageBox.Show(
                                $"Remove '{cap_subject.Name}' on {cap_date:MMM d}?\n\nYes = this date only\nNo = remove subject entirely",
                                "Remove Subject",
                                MessageBoxButton.YesNoCancel,
                                MessageBoxImage.Question);
                            if (res == MessageBoxResult.Yes)
                            {
                                _db.AddExcludedDate(cap_subject, cap_date.ToString("yyyy-MM-dd"));
                                if (cap_entry != null) _db.DeleteEntry(cap_entry.Id);
                                Render();
                            }
                            else if (res == MessageBoxResult.No)
                            {
                                _db.DeleteSubject(cap_subject.Id);
                                Render();
                            }
                        },
                        // mark single item complete
                        (itemId, nowComplete) =>
                        {
                            _db.SetLessonItemComplete(itemId, nowComplete);
                            Render();
                        },
                        // open lesson editor
                        () =>
                        {
                            var freshEntry = _db.GetEntry(cap_subject.Id, cap_student.Id, cap_date.ToString("yyyy-MM-dd"));
                            var dlg = new LessonEditDialog(_db, cap_student, cap_subject, cap_date, freshEntry);
                            dlg.Owner = Window.GetWindow(this);
                            if (dlg.ShowDialog() == true) Render();
                        });

                    block.Margin = new Thickness(0, 0, 0, 4);
                    colStack.Children.Add(block);
                }

                // "+ Add Subject" button
                var addBtn = new Border
                {
                    BorderBrush     = borderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(4),
                    Padding         = new Thickness(0, 5, 0, 5),
                    Cursor          = Cursors.Hand,
                    Margin          = new Thickness(0, 4, 0, 0),
                    Background      = Brushes.Transparent
                };
                addBtn.Child = new TextBlock
                {
                    Text                = "+ Add Subject",
                    FontSize            = fsSmall,
                    Foreground          = textSecondary,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var cap_addStudent = student;
                var cap_addDate    = dayDate;
                addBtn.MouseLeftButtonUp += (s, e) => { OnAddSubject(cap_addStudent, cap_addDate); e.Handled = true; };
                addBtn.MouseEnter += (s, e) =>
                    ((Border)s).Background = new SolidColorBrush(Color.FromArgb(20, accentColor.R, accentColor.G, accentColor.B));
                addBtn.MouseLeave += (s, e) => ((Border)s).Background = Brushes.Transparent;

                colStack.Children.Add(addBtn);
                colBorder.Child = colStack;
                Grid.SetColumn(colBorder, colIndex);
                DayColumnsGrid.Children.Add(colBorder);
                colIndex++;
            }
        }
    }

    private static Border BuildSubjectBlock(
        Subject subject,
        LessonEntry? entry,
        Color subjectColor,
        bool expanded,
        double baseFs,
        double fsSmall,
        Action onToggleExpand,
        Action onToggleComplete,
        Action onDeleteOccurrence,
        Action<int, bool> onItemComplete,
        Action onOpenEditor)
    {
        var textColor = PickTextColor(subjectColor);
        var dimColor  = textColor is SolidColorBrush b
            ? new SolidColorBrush(Color.FromArgb(0xBB, b.Color.R, b.Color.G, b.Color.B))
            : textColor;

        bool blockDone = entry?.IsComplete == true;

        var block = new Border
        {
            Background   = new SolidColorBrush(subjectColor),
            CornerRadius = new CornerRadius(4),
            Padding      = new Thickness(8, 6, 8, 6),
            MinHeight    = 34
        };

        var outer = new StackPanel();

        // ---- Top row: subject name + action buttons ----
        var topRow = new Grid();
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameText = new TextBlock
        {
            Text                = subject.Name,
            FontSize            = baseFs,
            FontWeight          = FontWeights.SemiBold,
            Foreground          = textColor,
            TextWrapping        = TextWrapping.Wrap,
            TextDecorations     = blockDone ? TextDecorations.Strikethrough : null,
            VerticalAlignment   = VerticalAlignment.Center
        };
        Grid.SetColumn(nameText, 0);
        topRow.Children.Add(nameText);

        // Action buttons (✓ delete expand)
        var btnPanel = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(4, 0, 0, 0)
        };

        // Check/uncheck whole block
        var checkBtn = MakeIconButton(blockDone ? "✓" : "○", textColor, fsSmall, onToggleComplete);
        checkBtn.ToolTip = blockDone ? "Mark incomplete" : "Mark complete";
        btnPanel.Children.Add(checkBtn);

        // Delete occurrence
        var delBtn = MakeIconButton("✕", textColor, fsSmall, onDeleteOccurrence);
        delBtn.ToolTip = "Remove from this day";
        btnPanel.Children.Add(delBtn);

        // Expand/collapse toggle
        var expandBtn = MakeIconButton(expanded ? "▲" : "▼", textColor, fsSmall, onToggleExpand);
        expandBtn.ToolTip = expanded ? "Collapse" : "Expand lessons";
        btnPanel.Children.Add(expandBtn);

        Grid.SetColumn(btnPanel, 1);
        topRow.Children.Add(btnPanel);

        outer.Children.Add(topRow);

        // ---- Click on the name area opens the editor ----
        topRow.Cursor = Cursors.Hand;
        nameText.MouseLeftButtonUp += (_, e) => { onOpenEditor(); e.Handled = true; };

        // ---- Lesson items ----
        var items = entry?.Items ?? new List<LessonItem>();

        if (!expanded)
        {
            // Collapsed: name row only - no lesson preview
            // Show a subtle item count hint if there are lessons
            if (items.Count > 0)
                outer.Children.Add(new TextBlock
                {
                    Text       = $"{items.Count} lesson{(items.Count == 1 ? "" : "s")}",
                    FontSize   = fsSmall - 1,
                    Foreground = dimColor,
                    Margin     = new Thickness(0, 2, 0, 0)
                });
        }
        else
        {
            // Expanded: each lesson with its subtitle, check and remove buttons
            foreach (var item in items)
            {
                var cap_item = item;
                var itemPanel = new Grid { Margin = new Thickness(0, 3, 0, 0) };
                itemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                itemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var itemText = new StackPanel();
                itemText.Children.Add(new TextBlock
                {
                    Text            = $"• {item.Title}",
                    FontSize        = fsSmall,
                    Foreground      = textColor,
                    TextWrapping    = TextWrapping.Wrap,
                    FontWeight      = FontWeights.Medium,
                    TextDecorations = item.IsComplete ? TextDecorations.Strikethrough : null
                });
                if (!string.IsNullOrWhiteSpace(item.SubTitle))
                    itemText.Children.Add(new TextBlock
                    {
                        Text         = $"    {item.SubTitle}",
                        FontSize     = Math.Max(9, fsSmall - 1),
                        Foreground   = dimColor,
                        TextWrapping = TextWrapping.Wrap,
                        TextDecorations = item.IsComplete ? TextDecorations.Strikethrough : null
                    });
                Grid.SetColumn(itemText, 0);
                itemPanel.Children.Add(itemText);

                // Per-item check + remove
                var itemBtns = new StackPanel { Orientation = Orientation.Horizontal };
                var itemCheck = MakeIconButton(item.IsComplete ? "✓" : "○", dimColor, Math.Max(9, fsSmall - 1),
                    () => onItemComplete(cap_item.Id, !cap_item.IsComplete));
                var itemDel = MakeIconButton("✕", dimColor, Math.Max(9, fsSmall - 1), () =>
                {
                    // Remove just this item: reload entry and re-save without this item
                    // We'll handle via the editor for now - just open editor
                    onOpenEditor();
                });
                itemBtns.Children.Add(itemCheck);
                itemBtns.Children.Add(itemDel);
                Grid.SetColumn(itemBtns, 1);
                itemPanel.Children.Add(itemBtns);

                outer.Children.Add(itemPanel);
            }

            // Notes preview
            if (!string.IsNullOrWhiteSpace(entry?.Notes))
                outer.Children.Add(new TextBlock
                {
                    Text            = entry.Notes,
                    FontSize        = fsSmall,
                    Foreground      = dimColor,
                    TextWrapping    = TextWrapping.Wrap,
                    Margin          = new Thickness(0, 4, 0, 0),
                    FontStyle       = FontStyles.Italic
                });
        }

        block.Child = outer;
        return block;
    }

    private static TextBlock MakeIconButton(string text, Brush foreground, double fontSize, Action onClick)
    {
        var tb = new TextBlock
        {
            Text      = text,
            FontSize  = fontSize,
            Foreground = foreground,
            Margin    = new Thickness(3, 0, 0, 0),
            Cursor    = Cursors.Hand,
            Padding   = new Thickness(2, 0, 2, 0)
        };
        tb.MouseLeftButtonUp += (_, e) => { onClick(); e.Handled = true; };
        tb.MouseEnter += (_, _) => tb.Opacity = 0.7;
        tb.MouseLeave += (_, _) => tb.Opacity = 1.0;
        return tb;
    }

    private void OnAddSubject(Student student, DateTime date)
    {
        var subjects = _db.GetSubjects(student.Id, activeOnly: false);
        var dlg = new AddClassDialog(_db, student, subjects, date);
        dlg.Owner = Window.GetWindow(this);
        if (dlg.ShowDialog() == true) Render();
    }

    private static Brush PickTextColor(Color bg)
    {
        double lum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255;
        return lum > 0.55
            ? new SolidColorBrush(Color.FromRgb(0x1C, 0x23, 0x33))
            : Brushes.White;
    }

    private static Color ParseHexColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
                return Color.FromRgb(
                    Convert.ToByte(hex[..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..], 16));
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
