using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Dialogs;

public partial class ResourcesDialog : Window
{
    private readonly DatabaseService _db;
    private Resource? _editingResource;

    // Wrapper for the subject combo: shows "ClassName - Grade"
    private record SubjectItem(int Id, string Display);

    private List<SubjectItem> _subjectItems = new();

    // Sentinel for "no class"
    private static readonly SubjectItem NoClassItem = new(0, "(No class)");

    public ResourcesDialog(DatabaseService db)
    {
        InitializeComponent();
        _db = db;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Build subject list from all students with grade labels
        var students = _db.GetStudents();

        _subjectItems = students
            .SelectMany(student =>
            {
                var gradeDisplay = GradeHelper.KeyToDisplay(student.Grade);
                return _db.GetSubjects(student.Id, activeOnly: false)
                          .Select(sub => new SubjectItem(sub.Id, $"{sub.Name} - {gradeDisplay}"));
            })
            .OrderBy(x => x.Display)
            .ToList();

        var comboList = new List<SubjectItem> { NoClassItem };
        comboList.AddRange(_subjectItems);
        SubjectCombo.ItemsSource       = comboList;
        SubjectCombo.DisplayMemberPath = "Display";
        SubjectCombo.SelectedIndex     = 0;

        RefreshList();
    }

    private void RefreshList()
    {
        ResourceListBox.ItemsSource = _db.GetResources();
    }

    // -------------------------------------------------------------------------
    // Type toggle
    // -------------------------------------------------------------------------

    private void ResType_Changed(object sender, RoutedEventArgs e)
    {
        if (PathLabel == null) return;
        var isFile = RadioFile.IsChecked == true;
        PathLabel.Text      = isFile ? "File Path" : "URL";
        BrowseBtn.Visibility = isFile ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "Select a file" };
        if (dlg.ShowDialog() == true)
        {
            PathBox.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
                NameBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
        }
    }

    // -------------------------------------------------------------------------
    // List selection - load for edit
    // -------------------------------------------------------------------------

    private void ResourceListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResourceListBox.SelectedItem is not Resource r) return;
        LoadForEdit(r);
    }

    private void LoadForEdit(Resource r)
    {
        _editingResource  = r;
        FormTitle.Text    = "Edit Resource";
        NameBox.Text      = r.Name;
        PathBox.Text      = r.Path;
        DescBox.Text      = r.Description;

        RadioUrl.IsChecked  = r.Type == "URL";
        RadioFile.IsChecked = r.Type == "File";

        // Select linked subject
        var match = SubjectCombo.Items.Cast<SubjectItem>().FirstOrDefault(s => s.Id == (r.SubjectId ?? 0));
        SubjectCombo.SelectedItem = match ?? NoClassItem;
    }

    // -------------------------------------------------------------------------
    // CRUD
    // -------------------------------------------------------------------------

    private void SaveResource_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        var path = PathBox.Text.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
        {
            MessageBox.Show("Name and URL/path are required.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var type      = RadioFile.IsChecked == true ? "File" : "URL";
        var subjItem  = SubjectCombo.SelectedItem as SubjectItem;
        var subjectId = (subjItem == null || subjItem.Id == 0) ? (int?)null : subjItem.Id;

        if (_editingResource != null)
        {
            _editingResource.Name        = name;
            _editingResource.Type        = type;
            _editingResource.Path        = path;
            _editingResource.SubjectId   = subjectId;
            _editingResource.Description = DescBox.Text.Trim();
            _db.UpdateResource(_editingResource);
        }
        else
        {
            _db.AddResource(new Resource
            {
                Name        = name,
                Type        = type,
                Path        = path,
                SubjectId   = subjectId,
                Description = DescBox.Text.Trim()
            });
        }

        ClearForm();
        RefreshList();
    }

    private void DeleteResource_Click(object sender, RoutedEventArgs e)
    {
        if (ResourceListBox.SelectedItem is not Resource r) return;
        var confirm = MessageBox.Show($"Delete '{r.Name}'?", "Delete Resource",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm == MessageBoxResult.Yes)
        {
            _db.DeleteResource(r.Id);
            ClearForm();
            RefreshList();
        }
    }

    private void OpenResource_Click(object sender, RoutedEventArgs e)
    {
        if (ResourceListBox.SelectedItem is not Resource r) return;
        OpenPath(r.Path);
    }

    private static void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open:\n{path}\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearForm()
    {
        _editingResource              = null;
        FormTitle.Text                = "Add Resource";
        NameBox.Text                  = "";
        PathBox.Text                  = "";
        DescBox.Text                  = "";
        RadioUrl.IsChecked            = true;
        SubjectCombo.SelectedItem     = NoClassItem;
        ResourceListBox.SelectedIndex = -1;
    }

    private void ClearForm_Click(object sender, RoutedEventArgs e) => ClearForm();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
