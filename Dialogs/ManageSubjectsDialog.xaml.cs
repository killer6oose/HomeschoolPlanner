using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;

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
        };

        RefreshList();
    }

    private void RefreshList()
    {
        var subjects = _db.GetSubjects(_student.Id, activeOnly: false);
        SubjectList.DisplayMemberPath = "Name";
        SubjectList.ItemsSource       = subjects;
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
        DaysOfWeekPanel.Visibility   = RadioDaysOfWeek.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
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
                ScheduleDates = scheduleDates
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
        _editingSubject      = null;
        FormTitle.Text       = "Add Subject";
        NameBox.Text         = "";
        ColorBox.Text        = "#4A7CB5";
        SpecificDatesBox.Text = "";
        RadioEveryDay.IsChecked = true;
        DeleteBtn.Visibility = Visibility.Collapsed;
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
