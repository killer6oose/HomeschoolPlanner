using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Dialogs;

public partial class SchoolSettingsDialog : Window
{
    private readonly DatabaseService _db;
    private GradeClass? _editingClass;

    public SchoolSettingsDialog(DatabaseService db, int selectedTab = 0)
    {
        InitializeComponent();
        _db = db;

        Loaded += (s, e) =>
        {
            SchoolTabControl.SelectedIndex = selectedTab;
            OnLoaded(s, e);
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // ---- General tab ----
        // School year start / end
        SchoolYearStartPicker.SelectedDate = DateTime.TryParse(AppState.Settings.SchoolYearStart, out var schoolStart)
            ? schoolStart : DateTime.Today;
        SchoolYearEndPicker.SelectedDate = DateTime.TryParse(AppState.Settings.SchoolYearEnd, out var schoolEnd)
            ? schoolEnd : DateTime.Today.AddYears(1).AddDays(-1);

        // School days checkboxes
        var activeDays = AppState.Settings.SchoolDayNumbers.ToHashSet();
        ChkMon.IsChecked = activeDays.Contains(1);
        ChkTue.IsChecked = activeDays.Contains(2);
        ChkWed.IsChecked = activeDays.Contains(3);
        ChkThu.IsChecked = activeDays.Contains(4);
        ChkFri.IsChecked = activeDays.Contains(5);
        ChkSat.IsChecked = activeDays.Contains(6);
        ChkSun.IsChecked = activeDays.Contains(7);

        // ---- Class Library tab ----
        // Grade selector
        GradeCombo.ItemsSource  = GradeHelper.Grades.Select(g => g.Display).ToArray();
        GradeCombo.SelectedIndex = 0;

        // Wire color swatches
        ClassColorSwatches.Children.Add(ColorPickerHelper.BuildSwatchPanel(ClassColorBox, ClassColorPreview));
        ColorPickerHelper.AttachColorWheelPicker(ClassColorBox, ClassColorPreview);
    }

    // -------------------------------------------------------------------------
    // Class Library tab
    // -------------------------------------------------------------------------

    private string SelectedGradeKey()
    {
        if (GradeCombo.SelectedItem is not string display) return "K";
        return GradeHelper.DisplayToKey(display);
    }

    private void GradeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshClassList();
        ClearEdit();
    }

    private void RefreshClassList()
    {
        var key     = SelectedGradeKey();
        var classes = _db.GetGradeClasses(key);
        ClassListBox.ItemsSource = classes;
        ClassListBox.DisplayMemberPath = "Name";
    }

    private void ClassListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClassListBox.SelectedItem is not GradeClass gc) return;
        LoadForEdit(gc);
    }

    private void LoadForEdit(GradeClass gc)
    {
        _editingClass          = gc;
        EditPanelTitle.Text    = "Edit Subject";
        ClassNameBox.Text      = gc.Name;
        ClassColorBox.Text     = gc.Color;
        DeleteClassBtn.Visibility = Visibility.Visible;

        RadioEveryDay.IsChecked   = gc.ScheduleType != "DaysOfWeek";
        RadioDaysOfWeek.IsChecked = gc.ScheduleType == "DaysOfWeek";

        if (gc.ScheduleType == "DaysOfWeek")
        {
            var days = gc.ScheduleDays
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToHashSet();
            TplMon.IsChecked = days.Contains(1);
            TplTue.IsChecked = days.Contains(2);
            TplWed.IsChecked = days.Contains(3);
            TplThu.IsChecked = days.Contains(4);
            TplFri.IsChecked = days.Contains(5);
            TplSat.IsChecked = days.Contains(6);
            TplSun.IsChecked = days.Contains(7);
        }
    }

    private void ClearEdit()
    {
        _editingClass             = null;
        EditPanelTitle.Text       = "New Subject";
        ClassNameBox.Text         = "";
        ClassColorBox.Text        = "#4A7CB5";
        DeleteClassBtn.Visibility = Visibility.Collapsed;
        RadioEveryDay.IsChecked   = true;
        TplMon.IsChecked = TplTue.IsChecked = TplWed.IsChecked =
        TplThu.IsChecked = TplFri.IsChecked = TplSat.IsChecked = TplSun.IsChecked = false;
        ClassListBox.SelectedIndex = -1;
    }

    private void ScheduleType_Changed(object sender, RoutedEventArgs e)
    {
        if (DayCheckPanel == null) return;
        DayCheckPanel.Visibility = RadioDaysOfWeek.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ClassColor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ClassColorPreview == null) return;
        try
        {
            var hex = ClassColorBox.Text.Trim().TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                ClassColorPreview.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
            }
        }
        catch { }
    }

    private void SaveClass_Click(object sender, RoutedEventArgs e)
    {
        var name = ClassNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Enter a subject name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var schedType = RadioDaysOfWeek.IsChecked == true ? "DaysOfWeek" : "EveryDay";
        var schedDays = "";
        if (schedType == "DaysOfWeek")
        {
            var days = new List<int>();
            if (TplMon.IsChecked == true) days.Add(1);
            if (TplTue.IsChecked == true) days.Add(2);
            if (TplWed.IsChecked == true) days.Add(3);
            if (TplThu.IsChecked == true) days.Add(4);
            if (TplFri.IsChecked == true) days.Add(5);
            if (TplSat.IsChecked == true) days.Add(6);
            if (TplSun.IsChecked == true) days.Add(7);
            schedDays = string.Join(",", days);
        }

        var color = NormalizeHex(ClassColorBox.Text);

        if (_editingClass != null)
        {
            _editingClass.Name         = name;
            _editingClass.Color        = color;
            _editingClass.ScheduleType = schedType;
            _editingClass.ScheduleDays = schedDays;
            _db.UpdateGradeClass(_editingClass);
        }
        else
        {
            var gc = new GradeClass
            {
                GradeKey     = SelectedGradeKey(),
                Name         = name,
                Color        = color,
                ScheduleType = schedType,
                ScheduleDays = schedDays
            };
            _db.AddGradeClass(gc);
        }

        RefreshClassList();
        ClearEdit();
    }

    private void DeleteClass_Click(object sender, RoutedEventArgs e)
    {
        if (_editingClass == null) return;
        var confirm = MessageBox.Show(
            $"Delete '{_editingClass.Name}'?",
            "Delete Subject",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            _db.DeleteGradeClass(_editingClass.Id);
            RefreshClassList();
            ClearEdit();
        }
    }

    private void ClearEdit_Click(object sender, RoutedEventArgs e) => ClearEdit();

    // -------------------------------------------------------------------------
    // Save / Cancel
    // -------------------------------------------------------------------------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // General tab - school year start / end
        AppState.Settings.SchoolYearStart = SchoolYearStartPicker.SelectedDate.HasValue
            ? SchoolYearStartPicker.SelectedDate.Value.ToString("yyyy-MM-dd")
            : DateTime.Today.ToString("yyyy-MM-dd");
        AppState.Settings.SchoolYearEnd = SchoolYearEndPicker.SelectedDate.HasValue
            ? SchoolYearEndPicker.SelectedDate.Value.ToString("yyyy-MM-dd")
            : DateTime.Today.AddYears(1).AddDays(-1).ToString("yyyy-MM-dd");

        // General tab - school days
        var selectedDays = new List<int>();
        if (ChkMon.IsChecked == true) selectedDays.Add(1);
        if (ChkTue.IsChecked == true) selectedDays.Add(2);
        if (ChkWed.IsChecked == true) selectedDays.Add(3);
        if (ChkThu.IsChecked == true) selectedDays.Add(4);
        if (ChkFri.IsChecked == true) selectedDays.Add(5);
        if (ChkSat.IsChecked == true) selectedDays.Add(6);
        if (ChkSun.IsChecked == true) selectedDays.Add(7);

        if (selectedDays.Count == 0)
        {
            MessageBox.Show("Select at least one school day.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AppState.Settings.SchoolDays = string.Join(",", selectedDays);
        _db.SaveSettings(AppState.Settings);

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static string NormalizeHex(string hex)
    {
        hex = hex.Trim();
        return hex.StartsWith("#") ? hex : "#" + hex;
    }
}
