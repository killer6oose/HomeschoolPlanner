using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Dialogs;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Views;

// Day view: shows every subject scheduled for the selected day as colored blocks.
// Supports single or multi-student layout (stacked sections).
public partial class DayView : UserControl
{
    private readonly DatabaseService _db;
    private List<Student> _students;
    private DateTime _currentDate;

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

        var dateStr      = _currentDate.ToString("yyyy-MM-dd");
        bool multiStudent = _students.Count > 1;

        foreach (var student in _students)
        {
            var subjects = _db.GetSubjects(student.Id);
            var daySubjects = subjects.Where(s => s.IsScheduledOn(_currentDate)).ToList();
            var entries  = _db.GetEntriesForRange(student.Id, dateStr, dateStr);
            var entryMap = entries.ToDictionary(e => e.SubjectId, e => e);

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
                    Text               = multiStudent ? $"No classes scheduled for {student.Name} today." : "No classes scheduled for today.",
                    FontSize           = 13,
                    Foreground         = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xAF)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin             = new Thickness(0, 10, 0, 16),
                    FontStyle          = FontStyles.Italic
                });
            }
            else
            {
                foreach (var subject in daySubjects)
                {
                    var subjectColor = ParseHexColor(subject.Color);
                    entryMap.TryGetValue(subject.Id, out var entry);

                    var capturedSubject = subject;
                    var capturedStudent = student;
                    var capturedEntry   = entry;

                    var card = new Border
                    {
                        Background      = Brushes.White,
                        BorderBrush     = new SolidColorBrush(Color.FromRgb(0xD8, 0xDC, 0xE6)),
                        BorderThickness = new Thickness(1),
                        CornerRadius    = new CornerRadius(6),
                        Margin          = new Thickness(0, 0, 0, 8),
                        Cursor          = Cursors.Hand
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
                    content.Children.Add(new TextBlock
                    {
                        Text       = subject.Name,
                        FontSize   = 13,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(subjectColor),
                        Margin     = new Thickness(0, 0, 0, 3)
                    });

                    if (entry != null && !string.IsNullOrWhiteSpace(entry.Title))
                    {
                        content.Children.Add(new TextBlock
                        {
                            Text            = entry.Title,
                            FontSize        = 13,
                            Foreground      = new SolidColorBrush(Color.FromRgb(0x1C, 0x23, 0x33)),
                            TextWrapping    = TextWrapping.Wrap,
                            TextDecorations = entry.IsComplete ? TextDecorations.Strikethrough : null
                        });
                    }
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.Notes))
                    {
                        content.Children.Add(new TextBlock
                        {
                            Text         = entry.Notes,
                            FontSize     = 12,
                            Foreground   = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
                            TextWrapping = TextWrapping.Wrap,
                            Margin       = new Thickness(0, 3, 0, 0)
                        });
                    }
                    if (entry == null || (string.IsNullOrWhiteSpace(entry.Title) && string.IsNullOrWhiteSpace(entry.Notes)))
                    {
                        content.Children.Add(new TextBlock
                        {
                            Text      = "Click to add lesson notes",
                            FontSize  = 12,
                            Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xB0, 0xBB)),
                            FontStyle = FontStyles.Italic
                        });
                    }
                    Grid.SetColumn(content, 1);
                    innerGrid.Children.Add(content);

                    // Completion dot
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.Title))
                    {
                        var dotColor = entry.IsComplete
                            ? Color.FromRgb(0x22, 0xC5, 0x5E)
                            : Color.FromRgb(0xCC, 0xD0, 0xD8);
                        var dot = new Border
                        {
                            Width               = 18,
                            Height              = 18,
                            CornerRadius        = new CornerRadius(9),
                            Background          = new SolidColorBrush(dotColor),
                            VerticalAlignment   = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin              = new Thickness(0, 0, 14, 0)
                        };
                        Grid.SetColumn(dot, 2);
                        innerGrid.Children.Add(dot);
                    }

                    card.Child = innerGrid;
                    card.MouseLeftButtonUp += (s, e) =>
                    {
                        var dlg = new LessonEditDialog(_db, capturedStudent, capturedSubject, _currentDate, capturedEntry);
                        dlg.Owner = Window.GetWindow(this);
                        if (dlg.ShowDialog() == true)
                            Render();
                    };

                    SubjectList.Children.Add(card);
                }
            }

            // "Add Class" link at the bottom of each student's section
            var addLink = new Border
            {
                Cursor          = Cursors.Hand,
                Padding         = new Thickness(0, 4, 0, 4),
                Margin          = new Thickness(0, 0, 0, multiStudent ? 20 : 0)
            };
            var addText = new TextBlock
            {
                Text       = "+ Add Class to This Day",
                FontSize   = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x7C, 0xB5)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            addLink.Child = addText;
            var capturedAddStudent = student;
            addLink.MouseLeftButtonUp += (s, e) =>
            {
                var allSubjects = _db.GetSubjects(capturedAddStudent.Id, activeOnly: false);
                var dlg = new AddClassDialog(_db, capturedAddStudent, allSubjects, _currentDate);
                dlg.Owner = Window.GetWindow(this);
                if (dlg.ShowDialog() == true)
                    Render();
            };
            SubjectList.Children.Add(addLink);
        }
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
