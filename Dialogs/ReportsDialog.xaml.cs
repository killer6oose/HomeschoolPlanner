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
    private List<Student> _allStudents = new();
    private readonly HashSet<int> _checkedStudentIds = new();
    private readonly HashSet<int> _checkedSubjectIds = new();

    public ReportsDialog(DatabaseService db)
    {
        _db = db;
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _allStudents = _db.GetStudents();

        // Default date range from school year settings
        var settings = AppState.Settings;
        StartPicker.SelectedDate = DateTime.TryParse(settings.SchoolYearStart, out var start)
            ? start : DateTime.Today;
        EndPicker.SelectedDate = DateTime.TryParse(settings.SchoolYearEnd, out var end)
            ? end : DateTime.Today.AddYears(1).AddDays(-1);

        PopulateStudents();
    }

    // -------------------------------------------------------------------------
    // Student checkboxes
    // -------------------------------------------------------------------------

    private void PopulateStudents()
    {
        StudentPanel.Children.Clear();
        _checkedStudentIds.Clear();

        if (_allStudents.Count == 0)
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

        foreach (var student in _allStudents)
        {
            _checkedStudentIds.Add(student.Id);

            var dot = new Border
            {
                Width           = 8,
                Height          = 8,
                CornerRadius    = new CornerRadius(4),
                Background      = TryParseBrush(student.Color),
                Margin          = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new TextBlock
            {
                Text              = student.Name,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground        = (Brush)FindResource("TextPrimaryBrush")
            };

            var inner = new StackPanel { Orientation = Orientation.Horizontal };
            inner.Children.Add(dot);
            inner.Children.Add(label);

            var capturedStudent = student;
            var cb = new CheckBox
            {
                Content   = inner,
                Tag       = student.Id,
                IsChecked = true,
                Margin    = new Thickness(0, 0, 16, 4)
            };
            cb.Checked   += (_, _) => { _checkedStudentIds.Add((int)cb.Tag);    PopulateSubjects(); };
            cb.Unchecked += (_, _) => { _checkedStudentIds.Remove((int)cb.Tag); PopulateSubjects(); };
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
        // PopulateSubjects() fires via the Checked/Unchecked events above
    }

    // -------------------------------------------------------------------------
    // Subject checkboxes
    // -------------------------------------------------------------------------

    private void PopulateSubjects()
    {
        SubjectPanel.Children.Clear();
        _checkedSubjectIds.Clear();

        var checkedStudents = _allStudents.Where(s => _checkedStudentIds.Contains(s.Id)).ToList();
        bool multiStudent   = checkedStudents.Count > 1;
        SubjectGroupHint.Visibility = multiStudent ? Visibility.Visible : Visibility.Collapsed;

        if (checkedStudents.Count == 0)
        {
            SubjectPanel.Children.Add(new TextBlock
            {
                Text       = "Select at least one student.",
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin     = new Thickness(4)
            });
            return;
        }

        foreach (var student in checkedStudents)
        {
            var subjects = _db.GetSubjects(student.Id, activeOnly: false);
            if (subjects.Count == 0) continue;

            // Group header when multiple students are selected
            if (multiStudent)
            {
                var header = new TextBlock
                {
                    Text       = student.Name,
                    FontWeight = FontWeights.SemiBold,
                    FontSize   = 11,
                    Foreground = TryParseBrush(student.Color),
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

        var students = _allStudents.Where(s => _checkedStudentIds.Contains(s.Id)).ToList();
        if (students.Count == 0)
        {
            MessageBox.Show("Select at least one student.", "Nothing to report",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Collect subjects for checked students that are also checked in the subject list
        var subjects = students
            .SelectMany(s => _db.GetSubjects(s.Id, activeOnly: false))
            .Where(s => _checkedSubjectIds.Contains(s.Id))
            .ToList();
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
                $"Report saved.\n\nOpen it now?",
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

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Brush TryParseBrush(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(color);
        }
        catch
        {
            return Brushes.Gray;
        }
    }
}
