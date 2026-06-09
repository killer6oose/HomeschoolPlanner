using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Dialogs;

// Dialog for adding a class (subject) to a student's schedule.
public partial class AddClassDialog : Window
{
    private readonly DatabaseService _db;
    private readonly Student _student;
    private readonly List<Subject> _existingSubjects;
    private readonly DateTime _selectedDate;

    // Backing list for the specific-dates ListBox
    private readonly List<string> _specificDates = new();

    private static readonly Subject NewSubjectSentinel = new() { Id = -1, Name = "[ New subject... ]" };

    public AddClassDialog(DatabaseService db, Student student, List<Subject> existingSubjects, DateTime selectedDate)
    {
        InitializeComponent();
        _db               = db;
        _student          = student;
        _existingSubjects = existingSubjects;
        _selectedDate     = selectedDate;

        // Build subject combo
        var comboItems = new List<Subject>(existingSubjects) { NewSubjectSentinel };
        SubjectCombo.ItemsSource       = comboItems;
        SubjectCombo.DisplayMemberPath = "Name";
        SubjectCombo.SelectedIndex     = 0;

        // Pre-seed the specific dates list with the clicked date
        _specificDates.Add(selectedDate.ToString("yyyy-MM-dd"));
        SelectedDatesList.ItemsSource = _specificDates;

        // Default DatePicker to the clicked date
        DatePickerInput.SelectedDate = selectedDate;

        Loaded += (_, _) =>
        {
            // Wire color swatch panel now that XAML elements exist
            ColorSwatches.Children.Add(ColorPickerHelper.BuildSwatchPanel(NewSubjectColor, ColorPreview));
        };
    }

    private void SubjectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NewSubjectPanel == null) return;
        var selected = SubjectCombo.SelectedItem as Subject;
        NewSubjectPanel.Visibility = selected?.Id == -1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ScheduleType_Changed(object sender, RoutedEventArgs e)
    {
        if (DaysOfWeekPanel == null || SpecificDatesPanel == null) return;
        DaysOfWeekPanel.Visibility    = RadioDaysOfWeek.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SpecificDatesPanel.Visibility = RadioSpecific.IsChecked   == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ColorPreview == null) return;
        try
        {
            var hex = NewSubjectColor.Text.Trim().TrimStart('#');
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

    private void AddDate_Click(object sender, RoutedEventArgs e)
    {
        if (!DatePickerInput.SelectedDate.HasValue) return;
        var dateStr = DatePickerInput.SelectedDate.Value.ToString("yyyy-MM-dd");
        if (!_specificDates.Contains(dateStr))
        {
            _specificDates.Add(dateStr);
            _specificDates.Sort();
            RefreshDateList();
        }
    }

    private void RemoveDate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDatesList.SelectedItem is string dateStr)
        {
            _specificDates.Remove(dateStr);
            RefreshDateList();
        }
    }

    private void RefreshDateList()
    {
        SelectedDatesList.ItemsSource = null;
        SelectedDatesList.ItemsSource = _specificDates;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var selectedSubject = SubjectCombo.SelectedItem as Subject;

        Subject subject;
        if (selectedSubject?.Id == -1)
        {
            var name = NewSubjectName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Enter a name for the new subject.", "Subject name required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                NewSubjectName.Focus();
                return;
            }

            var color = NewSubjectColor.Text.Trim();
            if (!color.StartsWith("#")) color = "#" + color;

            subject = _db.AddSubject(new Subject
            {
                StudentId     = _student.Id,
                Name          = name,
                Color         = color,
                ScheduleType  = "None",
                ScheduleDays  = "",
                ScheduleDates = ""
            });
        }
        else if (selectedSubject != null)
        {
            subject = selectedSubject;
        }
        else
        {
            MessageBox.Show("Select a subject.", "No subject selected",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (RadioEveryDay.IsChecked == true)
        {
            subject.ScheduleType  = "EveryDay";
            subject.ScheduleDays  = "";
            subject.ScheduleDates = "";
        }
        else if (RadioDaysOfWeek.IsChecked == true)
        {
            var dayNumbers = new List<string>();
            if (ChkMon.IsChecked == true) dayNumbers.Add("1");
            if (ChkTue.IsChecked == true) dayNumbers.Add("2");
            if (ChkWed.IsChecked == true) dayNumbers.Add("3");
            if (ChkThu.IsChecked == true) dayNumbers.Add("4");
            if (ChkFri.IsChecked == true) dayNumbers.Add("5");
            if (ChkSat.IsChecked == true) dayNumbers.Add("6");
            if (ChkSun.IsChecked == true) dayNumbers.Add("7");

            if (dayNumbers.Count == 0)
            {
                MessageBox.Show("Select at least one day of the week.", "No days selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            subject.ScheduleType  = "DaysOfWeek";
            subject.ScheduleDays  = string.Join(",", dayNumbers);
            subject.ScheduleDates = "";
        }
        else if (RadioSpecific.IsChecked == true)
        {
            if (_specificDates.Count == 0)
            {
                MessageBox.Show("Add at least one date.", "No dates",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Merge with any existing specific dates
            var merged = _specificDates.ToList();
            if (subject.ScheduleType == "SpecificDates" && !string.IsNullOrEmpty(subject.ScheduleDates))
            {
                merged = subject.ScheduleDates
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Union(merged)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();
            }

            subject.ScheduleType  = "SpecificDates";
            subject.ScheduleDays  = "";
            subject.ScheduleDates = string.Join(",", merged);
        }

        _db.UpdateSubject(subject);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
