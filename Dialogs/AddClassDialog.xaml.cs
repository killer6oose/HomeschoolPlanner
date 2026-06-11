using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Dialogs;

public partial class AddClassDialog : Window
{
    private readonly DatabaseService _db;
    private readonly Student _student;
    private readonly List<Subject> _existingSubjects;
    private readonly DateTime _selectedDate;
    private readonly List<string> _specificDates = new();
    private static readonly Subject NewSubjectSentinel = new() { Id = -1, Name = "[ New subject... ]" };

    public AddClassDialog(DatabaseService db, Student student, List<Subject> existingSubjects, DateTime selectedDate)
    {
        InitializeComponent();
        _db               = db;
        _student          = student;
        _existingSubjects = existingSubjects;
        _selectedDate     = selectedDate;

        var comboItems = new List<Subject> { NewSubjectSentinel };
        comboItems.AddRange(existingSubjects);
        SubjectCombo.ItemsSource       = comboItems;
        SubjectCombo.DisplayMemberPath = "Name";
        SubjectCombo.SelectedIndex     = 0;

        _specificDates.Add(selectedDate.ToString("yyyy-MM-dd"));
        SelectedDatesList.ItemsSource = _specificDates;
        DatePickerInput.SelectedDate  = selectedDate;

        Loaded += (_, _) =>
        {
            ColorSwatches.Children.Add(ColorPickerHelper.BuildSwatchPanel(NewSubjectColor, ColorPreview));
            ColorPickerHelper.AttachColorWheelPicker(NewSubjectColor, ColorPreview);
        };
    }

    // -------------------------------------------------------------------------
    // Visibility toggles
    // -------------------------------------------------------------------------

    private void SubjectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NewSubjectPanel == null) return;
        NewSubjectPanel.Visibility = (SubjectCombo.SelectedItem as Subject)?.Id == -1
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ScheduleType_Changed(object sender, RoutedEventArgs e)
    {
        if (DaysOfWeekPanel == null) return;
        DaysOfWeekPanel.Visibility    = RadioDaysOfWeek.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        MonthlyPanel.Visibility       = RadioMonthly.IsChecked    == true ? Visibility.Visible : Visibility.Collapsed;
        SpecificDatesPanel.Visibility = RadioSpecific.IsChecked    == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RepeatEnd_Changed(object sender, RoutedEventArgs e)
    {
        if (RepeatEndDatePicker == null) return;
        RepeatEndDatePicker.Visibility = RepeatByDate.IsChecked  == true ? Visibility.Visible : Visibility.Collapsed;
        RepeatCountPanel.Visibility    = RepeatByCount.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Monthly_Changed(object sender, RoutedEventArgs e)
    {
        if (MonthlyDayNumPanel == null) return;
        bool isDayNum = MonthlyDayNum.IsChecked == true;
        MonthlyDayNumPanel.Visibility   = isDayNum ? Visibility.Visible : Visibility.Collapsed;
        MonthlyWeekdayPanel.Visibility  = isDayNum ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ColorPreview == null) return;
        try
        {
            var hex = NewSubjectColor.Text.Trim().TrimStart('#');
            if (hex.Length == 6)
                ColorPreview.Background = new SolidColorBrush(Color.FromRgb(
                    Convert.ToByte(hex[..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..], 16)));
        }
        catch { }
    }

    // -------------------------------------------------------------------------
    // Specific date picker
    // -------------------------------------------------------------------------

    private void AddDate_Click(object sender, RoutedEventArgs e)
    {
        if (!DatePickerInput.SelectedDate.HasValue) return;
        var ds = DatePickerInput.SelectedDate.Value.ToString("yyyy-MM-dd");
        if (!_specificDates.Contains(ds)) { _specificDates.Add(ds); _specificDates.Sort(); RefreshDateList(); }
    }

    private void RemoveDate_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDatesList.SelectedItem is string ds) { _specificDates.Remove(ds); RefreshDateList(); }
    }

    private void RefreshDateList()
    {
        SelectedDatesList.ItemsSource = null;
        SelectedDatesList.ItemsSource = _specificDates;
    }

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var selected = SubjectCombo.SelectedItem as Subject;
        Subject subject;

        if (selected?.Id == -1)
        {
            var name = NewSubjectName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Enter a name for the new subject.", "Name required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var color = NewSubjectColor.Text.Trim();
            if (!color.StartsWith("#")) color = "#" + color;
            subject = _db.AddSubject(new Subject { StudentId = _student.Id, Name = name, Color = color });
        }
        else if (selected != null) subject = selected;
        else
        {
            MessageBox.Show("Select a subject.", "No subject", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (RadioEveryDay.IsChecked == true)
        {
            subject.ScheduleType  = "EveryDay";
            subject.ScheduleDays  = "";
            subject.ScheduleDates = "";
            subject.ScheduleMonthly = "";
        }
        else if (RadioDaysOfWeek.IsChecked == true)
        {
            var days = new List<string>();
            if (ChkMon.IsChecked == true) days.Add("1");
            if (ChkTue.IsChecked == true) days.Add("2");
            if (ChkWed.IsChecked == true) days.Add("3");
            if (ChkThu.IsChecked == true) days.Add("4");
            if (ChkFri.IsChecked == true) days.Add("5");
            if (ChkSat.IsChecked == true) days.Add("6");
            if (ChkSun.IsChecked == true) days.Add("7");

            if (days.Count == 0)
            {
                MessageBox.Show("Select at least one day.", "No days", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            subject.ScheduleType    = "DaysOfWeek";
            subject.ScheduleDays    = string.Join(",", days);
            subject.ScheduleDates   = "";
            subject.ScheduleMonthly = "";

            // Repeat-until
            if (RepeatByDate.IsChecked == true && RepeatEndDatePicker.SelectedDate.HasValue)
            {
                subject.ScheduleEndType = "ByDate";
                subject.ScheduleEndDate = RepeatEndDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
                subject.ScheduleEndCount = 0;
            }
            else if (RepeatByCount.IsChecked == true)
            {
                subject.ScheduleEndType  = "ByCount";
                subject.ScheduleEndDate  = "";
                if (int.TryParse(RepeatCountBox.Text, out var n) && n > 0)
                    subject.ScheduleEndCount = n;
                else
                    subject.ScheduleEndCount = 7;
            }
            else
            {
                subject.ScheduleEndType  = "Forever";
                subject.ScheduleEndDate  = "";
                subject.ScheduleEndCount = 0;
            }
        }
        else if (RadioMonthly.IsChecked == true)
        {
            string monthlyPattern;
            if (MonthlyFirstDay.IsChecked == true)
            {
                var wd = (MonthlyWeekdayCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "Mon";
                monthlyPattern = $"First:{wd}";
            }
            else if (MonthlyLastDay.IsChecked == true)
            {
                var wd = (MonthlyWeekdayCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "Mon";
                monthlyPattern = $"Last:{wd}";
            }
            else
            {
                monthlyPattern = $"Day:{MonthlyDayNumBox.Text.Trim()}";
            }
            subject.ScheduleType     = "Monthly";
            subject.ScheduleDays     = "";
            subject.ScheduleDates    = "";
            subject.ScheduleMonthly  = monthlyPattern;
            subject.ScheduleEndType  = "Forever";
            subject.ScheduleEndDate  = "";
            subject.ScheduleEndCount = 0;
        }
        else // RadioSpecific
        {
            if (_specificDates.Count == 0)
            {
                MessageBox.Show("Add at least one date.", "No dates", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            subject.ScheduleType     = "Specific";
            subject.ScheduleDays     = "";
            subject.ScheduleDates    = string.Join(",", _specificDates);
            subject.ScheduleMonthly  = "";
            subject.ScheduleEndType  = "";
            subject.ScheduleEndDate  = "";
            subject.ScheduleEndCount = 0;
        }

        _db.UpdateSubject(subject);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
