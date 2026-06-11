using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;
using System.Windows.Input;

namespace HomeschoolPlanner.Dialogs;

public partial class ManageSubjectsDialog : Window
{
    private readonly DatabaseService _db;
    private readonly Student _student;
    private Subject? _editingSubject;

    public ManageSubjectsDialog(DatabaseService db, Student student)
    {
        InitializeComponent();
        _db      = db;
        _student = student;
        DialogTitle.Text = $"Subjects - {student.Name}";

        Loaded += (_, _) =>
        {
            ColorSwatches.Children.Add(ColorPickerHelper.BuildSwatchPanel(ColorBox, ColorPreview));
            ColorPickerHelper.AttachColorWheelPicker(ColorBox, ColorPreview);
        };

        RefreshList();
    }

    private void RefreshList()
    {
        var subjects = _db.GetSubjects(_student.Id, activeOnly: false);
        SubjectList.DisplayMemberPath = "Name";
        SubjectList.ItemsSource       = subjects;

        // Show the grade template preview only when the student has no subjects at all.
        // Once any subject exists (custom or from template), we never show the preview again.
        if (subjects.Count == 0 && !string.IsNullOrEmpty(_student.Grade))
        {
            var templates = _db.GetGradeClasses(_student.Grade);
            if (templates.Count > 0)
            {
                TemplatePreviewTitle.Text = $"Default subjects for {GradeHelper.KeyToDisplay(_student.Grade)}";
                BuildTemplatePreviewList(templates);
                TemplatePreviewPanel.Visibility = Visibility.Visible;
                return;
            }
        }

        // Either subjects exist or no template is available - keep the preview hidden
        TemplatePreviewPanel.Visibility = Visibility.Collapsed;
    }

    // Populates the WrapPanel with read-only subject name chips from the template
    private void BuildTemplatePreviewList(List<GradeClass> templates)
    {
        TemplatePreviewList.Children.Clear();
        foreach (var t in templates)
        {
            var chip = new Border
            {
                Background      = TryParseBrush(t.Color, 0.15),
                BorderBrush     = TryParseBrush(t.Color, 1.0),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Padding         = new Thickness(8, 3, 8, 3),
                Margin          = new Thickness(0, 0, 6, 6)
            };
            chip.Child = new TextBlock
            {
                Text       = t.Name,
                FontSize   = 12,
                Foreground = (Brush)FindResource("TextPrimaryBrush")
            };
            TemplatePreviewList.Children.Add(chip);
        }
    }

    // "Add these defaults" - apply the grade template and refresh
    private void UseDefaults_Click(object sender, RoutedEventArgs e)
    {
        _db.ApplyGradeTemplate(_student.Id, _student.Grade);
        RefreshList();
    }

    // "I'll make my own" - dismiss the preview so the user can add subjects manually
    private void DismissTemplate_Click(object sender, MouseButtonEventArgs e)
    {
        TemplatePreviewPanel.Visibility = Visibility.Collapsed;
    }

    // Returns a brush from a hex color with optional alpha (0.0-1.0)
    private static Brush TryParseBrush(string hex, double alpha = 1.0)
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            color.A = (byte)(alpha * 255);
            return new SolidColorBrush(color);
        }
        catch
        {
            return Brushes.Gray;
        }
    }

    private void SubjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _editingSubject = SubjectList.SelectedItem as Subject;
        if (_editingSubject == null) return;

        FormTitle.Text = "Edit Subject";
        NameBox.Text   = _editingSubject.Name;
        ColorBox.Text  = _editingSubject.Color;
        DeleteBtn.Visibility = Visibility.Visible;

        // Set schedule radio
        switch (_editingSubject.ScheduleType)
        {
            case "EveryDay":
                RadioEveryDay.IsChecked = true;
                break;
            case "DaysOfWeek":
                RadioDaysOfWeek.IsChecked = true;
                LoadDayCheckboxes(_editingSubject.ScheduleDays);
                break;
            case "SpecificDates":
                RadioSpecific.IsChecked = true;
                SpecificDatesBox.Text   = _editingSubject.ScheduleDates.Replace(",", "\n");
                break;
            default:
                RadioNone.IsChecked = true;
                break;
        }
    }

    private void LoadDayCheckboxes(string scheduleDays)
    {
        var days = scheduleDays.Split(',').Select(d => d.Trim()).ToList();
        ChkMon.IsChecked = days.Contains("1");
        ChkTue.IsChecked = days.Contains("2");
        ChkWed.IsChecked = days.Contains("3");
        ChkThu.IsChecked = days.Contains("4");
        ChkFri.IsChecked = days.Contains("5");
    }

    private void ScheduleType_Changed(object sender, RoutedEventArgs e)
    {
        if (DaysOfWeekPanel == null || SpecificDatesPanel == null) return;
        DaysOfWeekPanel.Visibility    = RadioDaysOfWeek.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SpecificDatesPanel.Visibility = RadioSpecific.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name  = NameBox.Text.Trim();
        var color = ColorBox.Text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Enter a subject name.", "Name required", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            return;
        }
        if (!color.StartsWith("#")) color = "#" + color;

        string scheduleType, scheduleDays = "", scheduleDates = "";

        if (RadioEveryDay.IsChecked == true)
        {
            scheduleType = "EveryDay";
        }
        else if (RadioDaysOfWeek.IsChecked == true)
        {
            var dayNums = new List<string>();
            if (ChkMon.IsChecked == true) dayNums.Add("1");
            if (ChkTue.IsChecked == true) dayNums.Add("2");
            if (ChkWed.IsChecked == true) dayNums.Add("3");
            if (ChkThu.IsChecked == true) dayNums.Add("4");
            if (ChkFri.IsChecked == true) dayNums.Add("5");
            if (dayNums.Count == 0)
            {
                MessageBox.Show("Select at least one day.", "No days selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            scheduleType = "DaysOfWeek";
            scheduleDays = string.Join(",", dayNums);
        }
        else if (RadioSpecific.IsChecked == true)
        {
            var rawLines  = SpecificDatesBox.Text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l));
            var validated = new List<string>();
            var bad       = new List<string>();
            foreach (var line in rawLines)
            {
                if (DateTime.TryParse(line, out var parsed))
                    validated.Add(parsed.ToString("yyyy-MM-dd"));
                else
                    bad.Add(line);
            }
            if (bad.Count > 0)
            {
                MessageBox.Show($"Invalid dates:\n{string.Join("\n", bad)}", "Bad dates", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            scheduleType  = "SpecificDates";
            scheduleDates = string.Join(",", validated.Distinct().OrderBy(d => d));
        }
        else
        {
            scheduleType = "None";
        }

        if (_editingSubject == null)
        {
            _db.AddSubject(new Subject
            {
                StudentId     = _student.Id,
                Name          = name,
                Color         = color,
                ScheduleType  = scheduleType,
                ScheduleDays  = scheduleDays,
                ScheduleDates = scheduleDates,
                // Stamp with the student's current grade so reports can separate by grade level
                GradeKey      = _student.Grade
            });
        }
        else
        {
            _editingSubject.Name          = name;
            _editingSubject.Color         = color;
            _editingSubject.ScheduleType  = scheduleType;
            _editingSubject.ScheduleDays  = scheduleDays;
            _editingSubject.ScheduleDates = scheduleDates;
            _db.UpdateSubject(_editingSubject);
        }

        Clear_Click(sender, e);
        RefreshList();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingSubject == null) return;
        var result = MessageBox.Show(
            $"Delete '{_editingSubject.Name}'? This removes all lesson history for this subject.",
            "Delete Subject",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _db.DeleteSubject(_editingSubject.Id);
            Clear_Click(sender, e);
            RefreshList();
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _editingSubject       = null;
        FormTitle.Text        = "Add Subject";
        NameBox.Text          = "";
        ColorBox.Text         = "#4A7CB5";
        SpecificDatesBox.Text = "";
        RadioEveryDay.IsChecked  = true;
        DeleteBtn.Visibility     = Visibility.Collapsed;
        SubjectList.SelectedItem = null;
    }

    private void ColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ColorPreview == null) return;
        try
        {
            var hex = ColorBox.Text.Trim().TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                ColorPreview.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
            }
        }
        catch { }
    }
}
