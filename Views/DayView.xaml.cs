using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Dialogs;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Views;

// Day view: shows every subject scheduled for the selected day as colored blocks.
// Supports single or multi-student layout (stacked sections).
public partial class DayView : UserControl
{
    private readonly DatabaseService _db;
    private List<Student> _students;
    private DateTime _currentDate;

    // Tracks which subject cards are expanded: "{subjectId}"
    private readonly HashSet<int> _expandedSubjects = new();

    public DayView(DatabaseService db, Student student) : this(db, new List<Student> { student }) { }

    public DayView(DatabaseService db, List<Student> students)
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

    public void LoadDay(DateTime date)
    {
        _currentDate = date;
        Render();
    }

    private void Render()
    {
        SubjectList.Children.Clear();

        var dateStr       = _currentDate.ToString("yyyy-MM-dd");
        bool multiStudent = _students.Count > 1;

        foreach (var student in _students)
        {
            var subjects    = _db.GetSubjects(student.Id);
            var daySubjects = subjects.Where(s => s.IsScheduledOn(_currentDate)).ToList();
            var entries     = _db.GetEntriesForRange(student.Id, dateStr, dateStr);
            var entryMap    = entries.ToDictionary(e => e.SubjectId, e => e);

            // Student section header in multi-student mode
            if (multiStudent)
            {
                var studentColor = ParseHexColor(student.Color);
                SubjectList.Children.Add(new Border
                {
                    Background   = new SolidColorBrush(studentColor) { Opacity = 0.12 },
                    CornerRadius = new CornerRadius(6),
                    Padding      = new Thickness(12, 8, 12, 8),
                    Margin       = new Thickness(0, 0, 0, 8),
                    Child        = new TextBlock
                    {
                        Text       = student.Name,
                        FontSize   = 15,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(studentColor)
                    }
                });
            }

            if (daySubjects.Count == 0)
            {
                SubjectList.Children.Add(new TextBlock
                {
                    Text                = multiStudent ? $"No classes scheduled for {student.Name} today." : "No classes scheduled for today.",
                    FontSize            = 13,
                    Foreground          = new SolidColorBrush(ThemeColors.TextSecondary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin              = new Thickness(0, 10, 0, 16),
                    FontStyle           = FontStyles.Italic
                });
            }
            else
            {
                // "Complete This Day" banner - only shown when there are subjects to complete
                var capturedStudent   = student;
                var capturedDaySubjects = daySubjects;
                var capturedEntryMap  = entryMap;
                var capturedDate      = _currentDate;

                bool allDone = daySubjects.All(s =>
                    entryMap.TryGetValue(s.Id, out var e) && e.IsComplete);

                var dayBar = new Border
                {
                    Background      = allDone
                        ? new SolidColorBrush(Color.FromArgb(30, 0x22, 0xC5, 0x5E))
                        : new SolidColorBrush(Color.FromArgb(12, ThemeColors.Accent.R, ThemeColors.Accent.G, ThemeColors.Accent.B)),
                    BorderBrush     = allDone
                        ? new SolidColorBrush(Color.FromArgb(80, 0x22, 0xC5, 0x5E))
                        : ThemeColors.BorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius    = new CornerRadius(6),
                    Padding         = new Thickness(12, 7, 12, 7),
                    Margin          = new Thickness(0, 0, 0, 10),
                    Cursor          = Cursors.Hand
                };
                var dayBarRow = new Grid();
                dayBarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                dayBarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                dayBarRow.Children.Add(new TextBlock
                {
                    Text              = allDone ? "Day complete!" : "Complete this day",
                    FontSize          = 12,
                    FontWeight        = allDone ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground        = allDone
                        ? new SolidColorBrush(Color.FromRgb(0x16, 0x91, 0x46))
                        : ThemeColors.TextSecondaryBrush,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var completeDayBtn = new Button
                {
                    Content             = allDone ? "Undo" : "Mark All Done",
                    Style               = (Style)Application.Current.Resources["NavButtonStyle"],
                    Padding             = new Thickness(10, 4, 10, 4),
                    FontSize            = 11,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(completeDayBtn, 1);
                dayBarRow.Children.Add(completeDayBtn);
                dayBar.Child = dayBarRow;

                completeDayBtn.Click += (_, _) =>
                {
                    var newState = !allDone;
                    foreach (var sub in capturedDaySubjects)
                    {
                        var entry = _db.EnsureEntry(sub.Id, capturedStudent.Id, capturedDate.ToString("yyyy-MM-dd"));
                        _db.SetEntryCompleteWithItems(entry.Id, newState);
                    }
                    Render();
                };

                SubjectList.Children.Add(dayBar);

                foreach (var subject in daySubjects)
                {
                    var subjectColor = ParseHexColor(subject.Color);
                    entryMap.TryGetValue(subject.Id, out var entry);
                    bool expanded = _expandedSubjects.Contains(subject.Id);

                    var capturedSubject = subject;
                    var capturedEntry   = entry;
                    var capturedId      = subject.Id;

                    var card = new Border
                    {
                        Background      = new SolidColorBrush(ThemeColors.Surface),
                        BorderBrush     = new SolidColorBrush(ThemeColors.Border),
                        BorderThickness = new Thickness(1),
                        CornerRadius    = new CornerRadius(6),
                        Margin          = new Thickness(0, 0, 0, 8)
                    };

                    var innerGrid = new Grid();
                    innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
                    innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    // Color accent bar
                    var bar = new Border
                    {
                        Background   = new SolidColorBrush(subjectColor),
                        CornerRadius = new CornerRadius(6, 0, 0, 6)
                    };
                    Grid.SetColumn(bar, 0);
                    innerGrid.Children.Add(bar);

                    // Content
                    var content = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };

                    // Header row: subject name + expand toggle
                    var headerRow = new Grid();
                    headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var nameBlock = new TextBlock
                    {
                        Text              = subject.Name,
                        FontSize          = 13,
                        FontWeight        = FontWeights.SemiBold,
                        Foreground        = new SolidColorBrush(subjectColor),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextDecorations   = entry?.IsComplete == true ? TextDecorations.Strikethrough : null
                    };
                    Grid.SetColumn(nameBlock, 0);
                    headerRow.Children.Add(nameBlock);

                    // Expand/collapse button
                    var expandLabel = expanded ? "▲" : "▼";
                    var expandHint  = entry?.Items.Count > 0
                        ? $"{entry.Items.Count} lesson{(entry.Items.Count == 1 ? "" : "s")}"
                        : "";
                    var expandPanel = new StackPanel
                    {
                        Orientation       = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor            = Cursors.Hand,
                        Margin            = new Thickness(8, 0, 0, 0)
                    };
                    if (!string.IsNullOrEmpty(expandHint))
                        expandPanel.Children.Add(new TextBlock
                        {
                            Text              = expandHint,
                            FontSize          = 11,
                            Foreground        = new SolidColorBrush(ThemeColors.TextSecondary),
                            Margin            = new Thickness(0, 0, 4, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                    expandPanel.Children.Add(new TextBlock
                    {
                        Text              = expandLabel,
                        FontSize          = 11,
                        Foreground        = new SolidColorBrush(ThemeColors.TextSecondary),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    expandPanel.MouseLeftButtonUp += (_, ev) =>
                    {
                        if (_expandedSubjects.Contains(capturedId)) _expandedSubjects.Remove(capturedId);
                        else _expandedSubjects.Add(capturedId);
                        ev.Handled = true;
                        Render();
                    };
                    Grid.SetColumn(expandPanel, 1);
                    headerRow.Children.Add(expandPanel);

                    // Click the name row to open editor
                    nameBlock.Cursor = Cursors.Hand;
                    nameBlock.MouseLeftButtonUp += (_, ev) =>
                    {
                        var dlg = new LessonEditDialog(_db, capturedStudent, capturedSubject, _currentDate, capturedEntry);
                        dlg.Owner = Window.GetWindow(this);
                        if (dlg.ShowDialog() == true) Render();
                        ev.Handled = true;
                    };

                    content.Children.Add(headerRow);

                    // Expanded content
                    if (expanded)
                    {
                        if (entry?.Items.Count > 0)
                        {
                            foreach (var lessonItem in entry.Items)
                            {
                                content.Children.Add(new TextBlock
                                {
                                    Text            = $"• {lessonItem.Title}",
                                    FontSize        = 12,
                                    Foreground      = new SolidColorBrush(ThemeColors.TextPrimary),
                                    TextWrapping    = TextWrapping.Wrap,
                                    TextDecorations = lessonItem.IsComplete ? TextDecorations.Strikethrough : null,
                                    Margin          = new Thickness(0, 3, 0, 1)
                                });
                                if (!string.IsNullOrWhiteSpace(lessonItem.SubTitle))
                                    content.Children.Add(new TextBlock
                                    {
                                        Text         = $"    {lessonItem.SubTitle}",
                                        FontSize     = 11,
                                        Foreground   = new SolidColorBrush(ThemeColors.TextSecondary),
                                        TextWrapping = TextWrapping.Wrap,
                                        Margin       = new Thickness(0, 0, 0, 1)
                                    });
                            }
                        }
                        if (entry != null && !string.IsNullOrWhiteSpace(entry.Notes))
                            content.Children.Add(new TextBlock
                            {
                                Text         = entry.Notes,
                                FontSize     = 12,
                                Foreground   = new SolidColorBrush(ThemeColors.TextSecondary),
                                TextWrapping = TextWrapping.Wrap,
                                FontStyle    = FontStyles.Italic,
                                Margin       = new Thickness(0, 4, 0, 0)
                            });
                        if (entry == null || (entry.Items.Count == 0 && string.IsNullOrWhiteSpace(entry.Notes)))
                            content.Children.Add(new TextBlock
                            {
                                Text       = "Click subject name to add lesson notes",
                                FontSize   = 12,
                                Foreground = new SolidColorBrush(ThemeColors.TextSecondary),
                                FontStyle  = FontStyles.Italic,
                                Margin     = new Thickness(0, 4, 0, 0)
                            });
                    }

                    Grid.SetColumn(content, 1);
                    innerGrid.Children.Add(content);

                    // Action buttons column: ✓ complete + ✕ delete
                    var actionPanel = new StackPanel
                    {
                        Orientation       = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin            = new Thickness(0, 0, 10, 0)
                    };

                    bool blockDone = entry?.IsComplete == true;

                    // Complete toggle
                    var completeBtn = MakeActionButton(
                        blockDone ? "✓" : "○",
                        blockDone ? Color.FromRgb(0x22, 0xC5, 0x5E) : ThemeColors.TextSecondary,
                        blockDone ? "Mark incomplete" : "Mark complete",
                        () =>
                        {
                            var e2 = capturedEntry ?? _db.EnsureEntry(capturedSubject.Id, capturedStudent.Id, _currentDate.ToString("yyyy-MM-dd"));
                            bool markDone = !(capturedEntry?.IsComplete ?? false);
                            _db.SetEntryCompleteWithItems(e2.Id, markDone);
                            Render();
                        });
                    actionPanel.Children.Add(completeBtn);

                    // Delete occurrence
                    var deleteBtn = MakeActionButton(
                        "✕",
                        Color.FromRgb(0xCC, 0x22, 0x22),
                        "Remove from this day",
                        () =>
                        {
                            var res = MessageBox.Show(
                                $"Remove '{capturedSubject.Name}' on {_currentDate:MMM d}?\n\nYes = this date only\nNo = remove subject entirely",
                                "Remove Subject",
                                MessageBoxButton.YesNoCancel,
                                MessageBoxImage.Question);
                            if (res == MessageBoxResult.Yes)
                            {
                                _db.AddExcludedDate(capturedSubject, _currentDate.ToString("yyyy-MM-dd"));
                                if (capturedEntry != null) _db.DeleteEntry(capturedEntry.Id);
                                Render();
                            }
                            else if (res == MessageBoxResult.No)
                            {
                                _db.DeleteSubject(capturedSubject.Id);
                                Render();
                            }
                        });
                    actionPanel.Children.Add(deleteBtn);

                    Grid.SetColumn(actionPanel, 2);
                    innerGrid.Children.Add(actionPanel);

                    card.Child = innerGrid;
                    SubjectList.Children.Add(card);
                }
            }

            // "Add Subject" button at the bottom of each student's section
            var capturedAddStudent = student;
            var addBtn = new Button
            {
                Content             = "+ Add Subject to This Day",
                Style               = (Style)Application.Current.Resources["PrimaryButtonStyle"],
                Margin              = new Thickness(0, 8, 0, multiStudent ? 20 : 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            addBtn.Click += (s, e) =>
            {
                var allSubjects = _db.GetSubjects(capturedAddStudent.Id, activeOnly: false);
                var dlg = new AddClassDialog(_db, capturedAddStudent, allSubjects, _currentDate);
                dlg.Owner = Window.GetWindow(this);
                if (dlg.ShowDialog() == true)
                    Render();
            };
            SubjectList.Children.Add(addBtn);
        }
    }

    private static FrameworkElement MakeActionButton(string text, Color color, string tooltip, Action onClick)
    {
        var tb = new TextBlock
        {
            Text                = text,
            FontSize            = 14,
            Foreground          = new SolidColorBrush(color),
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var bg = ThemeColors.Background;
        bool isDark = bg.R * 0.299f + bg.G * 0.587f + bg.B * 0.114f < 128;
        var hoverBg = new SolidColorBrush(isDark
            ? Color.FromArgb(55, 255, 255, 255)
            : Color.FromArgb(55, 0,   0,   0));

        var btn = new Border
        {
            Child        = tb,
            Padding      = new Thickness(6, 3, 6, 3),
            Margin       = new Thickness(2, 0, 0, 0),
            CornerRadius = new CornerRadius(4),
            Background   = Brushes.Transparent,
            Cursor       = Cursors.Hand,
            ToolTip      = tooltip
        };

        btn.MouseLeftButtonUp += (_, e) => { onClick(); e.Handled = true; };
        btn.MouseEnter += (_, _) => btn.Background = hoverBg;
        btn.MouseLeave += (_, _) => btn.Background = Brushes.Transparent;
        return btn;
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
