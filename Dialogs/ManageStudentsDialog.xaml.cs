using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Dialogs;

public partial class ManageStudentsDialog : Window
{
    private readonly DatabaseService _db;
    private Student? _editingStudent;

    public ManageStudentsDialog(DatabaseService db)
    {
        InitializeComponent();
        _db = db;

        // Populate grade dropdown
        GradeCombo.ItemsSource = GradeHelper.Grades.Select(g => g.Display).ToArray();
        GradeCombo.SelectedIndex = 0;

        Loaded += (_, _) =>
        {
            ColorSwatches.Children.Add(ColorPickerHelper.BuildSwatchPanel(ColorBox, ColorPreview));
            ColorPickerHelper.AttachColorWheelPicker(ColorBox, ColorPreview);
        };

        RefreshList();
    }

    private void RefreshList()
    {
        var students = _db.GetStudents();
        StudentList.DisplayMemberPath = "Name";
        StudentList.ItemsSource       = students;
    }

    private void StudentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _editingStudent = StudentList.SelectedItem as Student;
        if (_editingStudent == null) return;

        FormTitle.Text     = "Edit Student";
        NameBox.Text       = _editingStudent.Name;
        ColorBox.Text      = _editingStudent.Color;
        SchoolYearBox.Text = _editingStudent.SchoolYear;
        DeleteBtn.Visibility = Visibility.Visible;

        // Select matching grade
        var display = GradeHelper.KeyToDisplay(_editingStudent.Grade);
        GradeCombo.SelectedItem = display;
    }

    private void GradeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Nothing needed here - just captures selection for Save
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name  = NameBox.Text.Trim();
        var color = ColorBox.Text.Trim();
        if (!color.StartsWith("#")) color = "#" + color;

        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Enter a name.", "Name required", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            return;
        }

        // Grade key from display name
        var gradeDisplay = GradeCombo.SelectedItem as string ?? "";
        var gradeKey     = GradeHelper.DisplayToKey(gradeDisplay);
        var schoolYear   = SchoolYearBox.Text.Trim();

        if (_editingStudent == null)
        {
            // New student - pre-populate school year from app settings if the user left it blank
            if (string.IsNullOrEmpty(schoolYear))
            {
                var s = AppState.Settings;
                if (DateTime.TryParse(s.SchoolYearStart, out var syStart) &&
                    DateTime.TryParse(s.SchoolYearEnd,   out var syEnd))
                {
                    schoolYear = $"{syStart.Year}-{syEnd.Year}";
                }
            }

            var student = _db.AddStudent(name, gradeKey, color, schoolYear);

            // Offer to load grade template if any classes exist for this grade and pref is on
            var templateClasses = _db.GetGradeClasses(gradeKey);
            if (AppState.Settings.ShowGradeTemplatePrompt && templateClasses.Count > 0)
            {
                var result = MessageBox.Show(
                    $"There are {templateClasses.Count} class(es) in the {gradeDisplay} template.\nLoad the default schedule for {name}?",
                    "Load Template?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                    _db.ApplyGradeTemplate(student.Id, gradeKey);
            }
        }
        else
        {
            _editingStudent.Name       = name;
            _editingStudent.Grade      = gradeKey;
            _editingStudent.Color      = color;
            _editingStudent.SchoolYear = schoolYear;
            _db.UpdateStudent(_editingStudent);
        }

        Clear_Click(sender, e);
        RefreshList();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_editingStudent == null) return;

        var result = MessageBox.Show(
            $"Delete {_editingStudent.Name}? This removes all their subjects and lesson history.",
            "Delete Student",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _db.DeleteStudent(_editingStudent.Id);
            Clear_Click(sender, e);
            RefreshList();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _editingStudent    = null;
        FormTitle.Text     = "Add Student";
        NameBox.Text       = "";
        ColorBox.Text      = "#4A7CB5";
        SchoolYearBox.Text = "";
        if (ColorPreview != null)
            ColorPreview.Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x7C, 0xB5));
        DeleteBtn.Visibility     = Visibility.Collapsed;
        StudentList.SelectedItem = null;
        GradeCombo.SelectedIndex = 0;
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
