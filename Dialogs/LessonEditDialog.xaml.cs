using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    // Working copy of lesson items - edited in-dialog, saved on Save_Click
    private readonly List<LessonItem> _items = new();

    // -1 means "add mode"; >= 0 means the index being edited
    private int _editingItemIdx = -1;

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
            NotesBox.Text           = _entry.Notes;
            CompleteCheck.IsChecked = _entry.IsComplete;
            DeleteBtn.Visibility    = Visibility.Visible;
            _items.AddRange(_entry.Items.Select(i => new LessonItem
            {
                Id            = i.Id,
                LessonEntryId = i.LessonEntryId,
                Title         = i.Title,
                SubTitle      = i.SubTitle,
                SortOrder     = i.SortOrder,
                IsComplete    = i.IsComplete
            }));
        }

        Loaded += (_, _) =>
        {
            RefreshItemsList();
            LoadResources();
            // Show "Add to all students" only when more than one student exists
            if (_db.GetStudents().Count > 1)
                AddToAllCheck.Visibility = Visibility.Visible;
            NewLessonTitle.Focus();
        };
    }

    // -------------------------------------------------------------------------
    // Lesson items
    // -------------------------------------------------------------------------

    private void RefreshItemsList()
    {
        LessonItemsPanel.Children.Clear();
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var idx  = i; // capture for lambda

            var row = new Border
            {
                BorderBrush     = new SolidColorBrush(Color.FromRgb(0xD8, 0xDC, 0xE6)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(0, 4, 0, 4),
                Margin          = new Thickness(0, 0, 0, 2),
                // Highlight the row currently being edited
                Background      = (idx == _editingItemIdx)
                    ? new SolidColorBrush(Color.FromArgb(0x18, 0x4A, 0x7C, 0xB5))
                    : null
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textCol = new StackPanel();
            var titleTb = new TextBlock
            {
                FontWeight      = FontWeights.SemiBold,
                TextWrapping    = TextWrapping.Wrap,
                TextDecorations = item.IsComplete ? TextDecorations.Strikethrough : null
            };
            titleTb.Inlines.Add($"• {item.Title}");
            textCol.Children.Add(titleTb);
            if (!string.IsNullOrWhiteSpace(item.SubTitle))
                textCol.Children.Add(new TextBlock
                {
                    Text            = $"   {item.SubTitle}",
                    FontSize        = 11,
                    Foreground      = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
                    TextWrapping    = TextWrapping.Wrap,
                    TextDecorations = item.IsComplete ? TextDecorations.Strikethrough : null
                });
            Grid.SetColumn(textCol, 0);
            grid.Children.Add(textCol);

            // Buttons: check, edit, move up, move down, remove
            var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

            var chkBtn = new Button
            {
                Content = item.IsComplete ? "✓" : "○",
                Style   = (Style)FindResource("NavButtonStyle"),
                Padding = new Thickness(4, 2, 4, 2),
                ToolTip = item.IsComplete ? "Mark incomplete" : "Mark complete"
            };
            chkBtn.Click += (_, _) => { _items[idx].IsComplete = !_items[idx].IsComplete; RefreshItemsList(); };
            btns.Children.Add(chkBtn);

            var editBtn = new Button
            {
                Content = "✎",
                Style   = (Style)FindResource("NavButtonStyle"),
                Padding = new Thickness(4, 2, 4, 2),
                ToolTip = "Edit this lesson"
            };
            editBtn.Click += (_, _) => BeginEditItem(idx);
            btns.Children.Add(editBtn);

            if (i > 0)
            {
                var upBtn = new Button { Content = "↑", Style = (Style)FindResource("NavButtonStyle"), Padding = new Thickness(4, 2, 4, 2) };
                upBtn.Click += (_, _) => { (_items[idx - 1], _items[idx]) = (_items[idx], _items[idx - 1]); RefreshItemsList(); };
                btns.Children.Add(upBtn);
            }
            if (i < _items.Count - 1)
            {
                var downBtn = new Button { Content = "↓", Style = (Style)FindResource("NavButtonStyle"), Padding = new Thickness(4, 2, 4, 2) };
                downBtn.Click += (_, _) => { (_items[idx + 1], _items[idx]) = (_items[idx], _items[idx + 1]); RefreshItemsList(); };
                btns.Children.Add(downBtn);
            }

            var removeBtn = new Button
            {
                Content    = "✕",
                Style      = (Style)FindResource("NavButtonStyle"),
                Padding    = new Thickness(4, 2, 4, 2),
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0x22, 0x22)),
                ToolTip    = "Remove this lesson"
            };
            removeBtn.Click += (_, _) =>
            {
                // If we were editing this item, cancel the edit first
                if (_editingItemIdx == idx) ResetEditForm();
                _items.RemoveAt(idx);
                RefreshItemsList();
            };
            btns.Children.Add(removeBtn);

            Grid.SetColumn(btns, 1);
            grid.Children.Add(btns);
            row.Child = grid;
            LessonItemsPanel.Children.Add(row);
        }
    }

    // Populate the form fields for editing an existing item
    private void BeginEditItem(int idx)
    {
        _editingItemIdx             = idx;
        NewLessonTitle.Text         = _items[idx].Title;
        NewLessonSubTitle.Text      = _items[idx].SubTitle;
        AddLessonFormTitle.Text     = "Edit lesson";
        AddLessonBtn.Content        = "Update Lesson";
        CancelEditBtn.Visibility    = Visibility.Visible;
        NewLessonTitle.Focus();
        RefreshItemsList(); // re-render to highlight the row
    }

    // Reset the form back to "add" mode
    private void ResetEditForm()
    {
        _editingItemIdx             = -1;
        NewLessonTitle.Clear();
        NewLessonSubTitle.Clear();
        AddLessonFormTitle.Text     = "Add a lesson";
        AddLessonBtn.Content        = "+ Add Lesson";
        CancelEditBtn.Visibility    = Visibility.Collapsed;
    }

    private void AddLesson_Click(object sender, RoutedEventArgs e)
    {
        var title = NewLessonTitle.Text.Trim();
        if (string.IsNullOrEmpty(title)) return;

        if (_editingItemIdx >= 0)
        {
            // Update existing item in place
            _items[_editingItemIdx].Title    = title;
            _items[_editingItemIdx].SubTitle = NewLessonSubTitle.Text.Trim();
        }
        else
        {
            // Add new item
            _items.Add(new LessonItem
            {
                Title     = title,
                SubTitle  = NewLessonSubTitle.Text.Trim(),
                SortOrder = _items.Count
            });
        }

        ResetEditForm();
        RefreshItemsList();
        NewLessonTitle.Focus();
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        ResetEditForm();
        RefreshItemsList();
    }

    // -------------------------------------------------------------------------
    // Resources
    // -------------------------------------------------------------------------

    private void LoadResources()
    {
        var resources = _db.GetResources(_subject.Id);
        ResourceListBox.ItemsSource = resources;
        NoResourcesLabel.Visibility = resources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResourceListBox.Visibility  = resources.Count > 0  ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenResource_Click(object sender, RoutedEventArgs e)
    {
        if (ResourceListBox.SelectedItem is not Resource r) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(r.Path) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ManageResources_Click(object sender, RoutedEventArgs e)
    {
        new ResourcesDialog(_db) { Owner = this }.ShowDialog();
        LoadResources();
    }

    // -------------------------------------------------------------------------
    // Save / Delete
    // -------------------------------------------------------------------------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // If the user typed a title but did not click "+ Add Lesson", add it automatically
        var pendingTitle = NewLessonTitle.Text.Trim();
        if (!string.IsNullOrEmpty(pendingTitle))
        {
            if (_editingItemIdx >= 0)
            {
                _items[_editingItemIdx].Title    = pendingTitle;
                _items[_editingItemIdx].SubTitle = NewLessonSubTitle.Text.Trim();
            }
            else
            {
                _items.Add(new LessonItem
                {
                    Title     = pendingTitle,
                    SubTitle  = NewLessonSubTitle.Text.Trim(),
                    SortOrder = _items.Count
                });
            }
            ResetEditForm();
        }

        var notes = NotesBox.Text.Trim();
        var done  = CompleteCheck.IsChecked == true;

        // If nothing to save, close without writing
        if (_entry == null && _items.Count == 0 && string.IsNullOrEmpty(notes) && !done)
        {
            DialogResult = true;
            return;
        }

        if (_entry == null)
        {
            _entry = new LessonEntry
            {
                SubjectId  = _subject.Id,
                StudentId  = _student.Id,
                LessonDate = _date.ToString("yyyy-MM-dd")
            };
        }

        _entry.Notes      = notes;
        _entry.IsComplete = done;
        _entry.Items      = _items;

        _db.SaveEntry(_entry);

        // Optionally copy this lesson to all other students with a matching subject name
        if (AddToAllCheck.IsChecked == true)
        {
            var dateStr = _date.ToString("yyyy-MM-dd");
            foreach (var student in _db.GetStudents())
            {
                if (student.Id == _student.Id) continue;

                // Find a subject with the same name on the other student
                var match = _db.GetSubjects(student.Id, activeOnly: false)
                    .FirstOrDefault(s => string.Equals(s.Name, _subject.Name, StringComparison.OrdinalIgnoreCase));
                if (match == null) continue;

                // Load or create the entry for that student/subject/date
                var otherEntry = _db.GetEntry(match.Id, student.Id, dateStr) ?? new LessonEntry
                {
                    SubjectId  = match.Id,
                    StudentId  = student.Id,
                    LessonDate = dateStr
                };

                otherEntry.Notes      = notes;
                otherEntry.IsComplete = done;
                // Copy lesson items (without IDs so they get inserted fresh)
                otherEntry.Items = _items.Select(i => new LessonItem
                {
                    Title     = i.Title,
                    SubTitle  = i.SubTitle,
                    SortOrder = i.SortOrder,
                    IsComplete = i.IsComplete
                }).ToList();

                _db.SaveEntry(otherEntry);
            }
        }

        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_entry == null) return;
        var res = MessageBox.Show(
            $"Delete all lesson data for {_subject.Name} on {_date:MMM d}?",
            "Delete Entry", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            _db.DeleteEntry(_entry.Id);
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
