using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Dialogs;

public partial class LessonEditDialog : Window
{
    private readonly DatabaseService _db;
    private readonly Student _student;
    private readonly Subject _subject;
    private readonly DateTime _date;
    private LessonEntry? _entry;

    public LessonEditDialog(DatabaseService db, Student student, Subject subject, DateTime date, LessonEntry? existingEntry)
    {
        InitializeComponent();
        _db      = db;
        _student = student;
        _subject = subject;
        _date    = date;
        _entry   = existingEntry;

        SubjectLabel.Text = subject.Name;
        DateLabel.Text    = date.ToString("dddd, MMMM d, yyyy");

        if (_entry != null)
        {
            TitleBox.Text           = _entry.Title;
            NotesBox.Text           = _entry.Notes;
            CompleteCheck.IsChecked = _entry.IsComplete;
            DeleteBtn.Visibility    = Visibility.Visible;
        }

        Loaded += (_, _) =>
        {
            LoadResources();
            TitleBox.Focus();
        };
    }

    // -------------------------------------------------------------------------
    // Resources section
    // -------------------------------------------------------------------------

    private void LoadResources()
    {
        var resources = _db.GetResources(_subject.Id);
        ResourceListBox.ItemsSource = resources;
        NoResourcesLabel.Visibility = resources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ResourceListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Deselect immediately - clicking opens the resource
        // (actual open happens via button; this just provides visual feedback)
    }

    private void OpenResource_Click(object sender, RoutedEventArgs e)
    {
        if (ResourceListBox.SelectedItem is not Resource r) return;
        try
        {
            Process.Start(new ProcessStartInfo(r.Path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open:\n{r.Path}\n\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ManageResources_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ResourcesDialog(_db) { Owner = this };
        dlg.ShowDialog();
        LoadResources(); // Refresh after managing
    }

    // -------------------------------------------------------------------------
    // Lesson save / delete
    // -------------------------------------------------------------------------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text.Trim();
        var notes = NotesBox.Text.Trim();
        var done  = CompleteCheck.IsChecked == true;

        if (_entry == null)
        {
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(notes))
            {
                DialogResult = true;
                return;
            }
            _entry = new LessonEntry
            {
                SubjectId  = _subject.Id,
                StudentId  = _student.Id,
                LessonDate = _date.ToString("yyyy-MM-dd")
            };
        }

        _entry.Title      = title;
        _entry.Notes      = notes;
        _entry.IsComplete = done;

        _db.SaveEntry(_entry);
        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_entry == null) return;
        var result = MessageBox.Show(
            $"Delete the lesson for {_subject.Name} on {_date:MMM d}?",
            "Delete Lesson",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _db.DeleteEntry(_entry.Id);
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
