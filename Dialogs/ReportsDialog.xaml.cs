using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;
using Microsoft.Win32;

namespace HomeschoolPlanner.Dialogs;

public partial class ReportsDialog : Window
{
    private readonly DatabaseService _db;

    // Students mode - uses (student, grade) pairs instead of bare students
    private List<StudentGradeGroup> _allGroups = new();
    private readonly HashSet<string> _checkedGroupKeys  = new(); // key = "studentId:gradeKey"
    private readonly HashSet<int>    _checkedSubjectIds = new();

    public ReportsDialog(DatabaseService db)
    {
        _db = db;
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Populate grade level combo for grade mode
        GradeLevelCombo.ItemsSource   = GradeHelper.Grades.Select(g => g.Display).ToArray();
        GradeLevelCombo.SelectedIndex = 0;

        // Default date range from school year settings
        var settings = AppState.Settings;
        StartPicker.SelectedDate = DateTime.TryParse(settings.SchoolYearStart, out var start)
            ? start : DateTime.Today;
        EndPicker.SelectedDate = DateTime.TryParse(settings.SchoolYearEnd, out var end)
            ? end : DateTime.Today.AddYears(1).AddDays(-1);

        PopulateStudents();
    }

    // -------------------------------------------------------------------------
    // Mode toggle
    // -------------------------------------------------------------------------

    private void ReportMode_Changed(object sender, RoutedEventArgs e)
    {
        if (GradeLevelPanel == null) return; // fires before InitializeComponent finishes

        bool gradeMode = ModeGradeRadio.IsChecked == true;

        // Students mode controls
        StudentsLabelRow.Visibility  = gradeMode ? Visibility.Collapsed : Visibility.Visible;
        StudentsBorder.Visibility    = gradeMode ? Visibility.Collapsed : Visibility.Visible;
        SubjectsLabelRow.Visibility  = gradeMode ? Visibility.Collapsed : Visibility.Visible;
        SubjectGroupHint.Visibility  = Visibility.Collapsed; // managed by PopulateSubjects
        SubjectsBorder.Visibility    = gradeMode ? Visibility.Collapsed : Visibility.Visible;
        DateRangePanel.Visibility    = gradeMode ? Visibility.Collapsed : Visibility.Visible;
        ReportTypePanel.Visibility   = gradeMode ? Visibility.Collapsed : Visibility.Visible;
        FormatPanel.Visibility       = gradeMode ? Visibility.Collapsed : Visibility.Visible;

        // Grade level mode controls
        GradeLevelPanel.Visibility = gradeMode ? Visibility.Visible : Visibility.Collapsed;

        GenerateBtn.Content = gradeMode ? "Export CSV" : "Generate Report";
    }

    // -------------------------------------------------------------------------
    // Student-grade pair checkboxes
    // -------------------------------------------------------------------------

    private void PopulateStudents()
    {
        StudentPanel.Children.Clear();
        _checkedGroupKeys.Clear();

        _allGroups = _db.GetStudentGradePairs();

        if (_allGroups.Count == 0)
        {
            StudentPanel.Children.Add(new TextBlock
            {
                Text       = "No students found.",
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin     = new Thickness(4)
            });
            PopulateSubjects();
            return;
        }

        foreach (var group in _allGroups)
        {
            _checkedGroupKeys.Add(group.Key);

            var dot = new Border
            {
                Width             = 8,
                Height            = 8,
                CornerRadius      = new CornerRadius(4),
                Background        = TryParseBrush(group.StudentColor),
                Margin            = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new TextBlock
            {
                Text              = group.Label,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground        = (Brush)FindResource("TextPrimaryBrush")
            };

            var inner = new StackPanel { Orientation = Orientation.Horizontal };
            inner.Children.Add(dot);
            inner.Children.Add(label);

            var cb = new CheckBox
            {
                Content   = inner,
                Tag       = group.Key,
                IsChecked = true,
                Margin    = new Thickness(0, 0, 16, 4)
            };
            cb.Checked   += (_, _) => { _checkedGroupKeys.Add((string)cb.Tag);    PopulateSubjects(); };
            cb.Unchecked += (_, _) => { _checkedGroupKeys.Remove((string)cb.Tag); PopulateSubjects(); };
            StudentPanel.Children.Add(cb);
        }

        PopulateSubjects();
    }

    private void SelectAllStudentsLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        bool allChecked = StudentPanel.Children.OfType<CheckBox>().All(cb => cb.IsChecked == true);
        bool target     = !allChecked;
        SelectAllStudentsLink.Text = target ? "Deselect all" : "Select all";
        foreach (var cb in StudentPanel.Children.OfType<CheckBox>())
            cb.IsChecked = target;
    }

    // -------------------------------------------------------------------------
    // Subject checkboxes (scoped to each selected student + grade pair)
    // -------------------------------------------------------------------------

    private void PopulateSubjects()
    {
        SubjectPanel.Children.Clear();
        _checkedSubjectIds.Clear();

        var checkedGroups = _allGroups.Where(g => _checkedGroupKeys.Contains(g.Key)).ToList();
        bool multiGroup   = checkedGroups.Count > 1;
        SubjectGroupHint.Visibility = multiGroup ? Visibility.Visible : Visibility.Collapsed;

        if (checkedGroups.Count == 0)
        {
            SubjectPanel.Children.Add(new TextBlock
            {
                Text       = "Select at least one student.",
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin     = new Thickness(4)
            });
            return;
        }

        // We need the student's current grade for the empty-GradeKey fallback
        var allStudents   = _db.GetStudents().ToDictionary(s => s.Id);

        foreach (var group in checkedGroups)
        {
            var currentGrade = allStudents.TryGetValue(group.StudentId, out var stu) ? stu.Grade : group.GradeKey;
            var subjects     = _db.GetSubjectsForGroup(group.StudentId, group.GradeKey, currentGrade);

            if (subjects.Count == 0) continue;

            if (multiGroup)
            {
                var header = new TextBlock
                {
                    Text       = group.Label,
                    FontWeight = FontWeights.SemiBold,
                    FontSize   = 11,
                    Foreground = TryParseBrush(group.StudentColor),
                    Margin     = new Thickness(0, SubjectPanel.Children.Count == 0 ? 4 : 8, 0, 2)
                };
                SubjectPanel.Children.Add(header);
            }

            var wrap = new WrapPanel { Orientation = Orientation.Horizontal };

            foreach (var subject in subjects)
            {
                _checkedSubjectIds.Add(subject.Id);

                var dot = new Border
                {
                    Width             = 8,
                    Height            = 8,
                    CornerRadius      = new CornerRadius(4),
                    Background        = TryParseBrush(subject.Color),
                    Margin            = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var lbl = new TextBlock
                {
                    Text              = subject.Name,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground        = (Brush)FindResource("TextPrimaryBrush")
                };

                var inner = new StackPanel { Orientation = Orientation.Horizontal };
                inner.Children.Add(dot);
                inner.Children.Add(lbl);

                var cb = new CheckBox
                {
                    Content   = inner,
                    Tag       = subject.Id,
                    IsChecked = true,
                    Margin    = new Thickness(0, 0, 14, 4)
                };
                cb.Checked   += (_, _) => _checkedSubjectIds.Add((int)cb.Tag);
                cb.Unchecked += (_, _) => _checkedSubjectIds.Remove((int)cb.Tag);
                wrap.Children.Add(cb);
            }

            SubjectPanel.Children.Add(wrap);
        }

        if (SubjectPanel.Children.Count == 0)
        {
            SubjectPanel.Children.Add(new TextBlock
            {
                Text       = "No subjects found for selected students.",
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin     = new Thickness(4)
            });
        }
    }

    private void SelectAllSubjectsLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var allCbs      = SubjectPanel.Children.OfType<WrapPanel>()
                                      .SelectMany(wp => wp.Children.OfType<CheckBox>())
                                      .ToList();
        bool allChecked = allCbs.All(cb => cb.IsChecked == true);
        bool target     = !allChecked;
        SelectAllSubjectsLink.Text = target ? "Deselect all" : "Select all";
        foreach (var cb in allCbs)
            cb.IsChecked = target;
    }

    // -------------------------------------------------------------------------
    // Generate
    // -------------------------------------------------------------------------

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (ModeGradeRadio.IsChecked == true)
        {
            GenerateGradeLevelCsv();
            return;
        }
        GenerateStudentReport();
    }

    private void GenerateStudentReport()
    {
        if (StartPicker.SelectedDate == null || EndPicker.SelectedDate == null)
        {
            MessageBox.Show("Please select a start and end date.", "Date required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (StartPicker.SelectedDate > EndPicker.SelectedDate)
        {
            MessageBox.Show("Start date must be on or before end date.", "Invalid range",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var checkedGroups = _allGroups.Where(g => _checkedGroupKeys.Contains(g.Key)).ToList();
        if (checkedGroups.Count == 0)
        {
            MessageBox.Show("Select at least one student.", "Nothing to report",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Build the student list (unique by Id) and the filtered subjects
        var allStudents  = _db.GetStudents().ToDictionary(s => s.Id);
        var studentsSeen = new HashSet<int>();
        var students     = new List<Student>();
        var subjects     = new List<Subject>();

        foreach (var group in checkedGroups)
        {
            if (studentsSeen.Add(group.StudentId) && allStudents.TryGetValue(group.StudentId, out var stu))
                students.Add(stu);

            var currentGrade = allStudents.TryGetValue(group.StudentId, out var s2) ? s2.Grade : group.GradeKey;
            var groupSubjects = _db.GetSubjectsForGroup(group.StudentId, group.GradeKey, currentGrade)
                                   .Where(s => _checkedSubjectIds.Contains(s.Id));
            subjects.AddRange(groupSubjects);
        }

        if (subjects.Count == 0)
        {
            MessageBox.Show("Select at least one subject.", "Nothing to report",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var opts = new ReportOptions
        {
            Students     = students,
            Subjects     = subjects,
            StartDate    = StartPicker.SelectedDate!.Value,
            EndDate      = EndPicker.SelectedDate!.Value,
            ReportType   = SummaryRadio.IsChecked == true ? "Summary" : "Log",
            OutputFormat = PdfRadio.IsChecked == true ? "PDF" : "CSV"
        };

        bool   isPdf       = opts.OutputFormat == "PDF";
        string filter      = isPdf ? "PDF files (*.pdf)|*.pdf" : "CSV files (*.csv)|*.csv";
        string ext         = isPdf ? ".pdf" : ".csv";
        string typePart    = opts.ReportType == "Summary" ? "Summary" : "DailyLog";
        string dateTag     = $"{opts.StartDate:yyyyMMdd}-{opts.EndDate:yyyyMMdd}";
        string defaultName = $"HomeschoolReport_{typePart}_{dateTag}{ext}";

        var dlg = new SaveFileDialog
        {
            Title        = "Save Report",
            Filter       = filter,
            FileName     = defaultName,
            DefaultExt   = ext,
            AddExtension = true
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            ReportBuilder.Generate(opts, _db, dlg.FileName);
            var result = MessageBox.Show(
                "Report saved.\n\nOpen it now?",
                "Report saved",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = dlg.FileName,
                    UseShellExecute = true
                });

            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Report generation failed:\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Exports a CSV roster of all subjects for the selected grade level
    private void GenerateGradeLevelCsv()
    {
        var gradeDisplay = GradeLevelCombo.SelectedItem as string ?? "";
        if (string.IsNullOrEmpty(gradeDisplay))
        {
            MessageBox.Show("Select a grade level.", "Grade required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var gradeKey = GradeHelper.DisplayToKey(gradeDisplay);
        var rows     = _db.GetSubjectsByGrade(gradeKey);

        if (rows.Count == 0)
        {
            MessageBox.Show($"No subjects found for {gradeDisplay}.", "Nothing to export",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title        = "Export Grade Roster",
            Filter       = "CSV files (*.csv)|*.csv",
            FileName     = $"SubjectRoster_{gradeKey}_{DateTime.Today:yyyyMMdd}.csv",
            DefaultExt   = ".csv",
            AddExtension = true
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var csv = BuildGradeRosterCsv(gradeDisplay, rows);
            File.WriteAllText(dlg.FileName, csv, Encoding.UTF8);

            var result = MessageBox.Show(
                "Roster exported.\n\nOpen it now?",
                "Export complete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = dlg.FileName,
                    UseShellExecute = true
                });

            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed:\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Builds the CSV content for the grade-level subject roster
    private static string BuildGradeRosterCsv(string gradeDisplay, List<(string StudentName, Subject Subject)> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Subject Roster - {gradeDisplay}");
        sb.AppendLine($"Exported,{DateTime.Today:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("Student,Subject,Schedule,Active");

        foreach (var (studentName, subject) in rows)
        {
            var schedule = subject.ScheduleType switch
            {
                "EveryDay"    => "Every school day",
                "DaysOfWeek"  => $"Days: {ExpandDayNumbers(subject.ScheduleDays)}",
                "SpecificDates" => "Specific dates",
                "Monthly"     => $"Monthly: {subject.ScheduleMonthly}",
                _             => "None"
            };

            sb.AppendLine($"{CsvEscape(studentName)},{CsvEscape(subject.Name)},{CsvEscape(schedule)},{(subject.IsActive ? "Yes" : "No")}");
        }

        return sb.ToString();
    }

    // Converts ISO day numbers (1=Mon ... 7=Sun) to readable abbreviations
    private static string ExpandDayNumbers(string scheduleDays)
    {
        if (string.IsNullOrEmpty(scheduleDays)) return "";
        var map = new Dictionary<string, string>
        {
            ["1"] = "Mon", ["2"] = "Tue", ["3"] = "Wed",
            ["4"] = "Thu", ["5"] = "Fri", ["6"] = "Sat", ["7"] = "Sun"
        };
        return string.Join(", ", scheduleDays.Split(',')
            .Select(d => map.TryGetValue(d.Trim(), out var name) ? name : d.Trim()));
    }

    // Escapes a value for CSV - wraps in quotes if it contains a comma, quote, or newline
    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Brush TryParseBrush(string hex)
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(color);
        }
        catch
        {
            return Brushes.Gray;
        }
    }
}
