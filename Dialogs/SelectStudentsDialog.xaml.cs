using System.Windows;
using System.Windows.Controls;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Dialogs;

public partial class SelectStudentsDialog : Window
{
    private readonly List<Student> _allStudents;
    private readonly List<CheckBox> _checkboxes = new();

    public List<Student> SelectedStudents { get; private set; } = new();

    public SelectStudentsDialog(List<Student> students)
    {
        InitializeComponent();
        _allStudents = students;

        foreach (var s in students)
        {
            var cb = new CheckBox
            {
                Content   = s.Name,
                FontSize  = 14,
                Margin    = new Thickness(0, 0, 0, 8),
                IsChecked = true,
                Tag       = s
            };
            _checkboxes.Add(cb);
            CheckboxPanel.Children.Add(cb);
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        SelectedStudents = _checkboxes
            .Where(cb => cb.IsChecked == true)
            .Select(cb => (Student)cb.Tag)
            .ToList();

        if (SelectedStudents.Count < 2)
        {
            MessageBox.Show("Select at least 2 students for split view.", "Not enough selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
