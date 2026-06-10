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
                Margin          = new Thickness(0, 0, 0, 2)
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
                    Text         = $"   {item.SubTitle}",
                    FontSize     = 11,
                    Foreground   = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
                    TextWrapping = TextWrapping.Wrap,
                    TextDecorations = item.IsComplete ? TextDecorations.Strikethrough : null
                });
            Grid.SetColumn(textCol, 0);
            grid.Children.Add(textCol);

            // Buttons: check, move up, move down, remove
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
                Content   = "✕",
                Style     = (Style)FindResource("NavButtonStyle"),
                Padding   = new Thickness(4, 2, 4, 2),
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0x22, 0x22)),
                ToolTip   = "Remove this lesson"
            };
            removeBtn.Click += (_, _) => { _items.RemoveAt(idx); RefreshItemsList(); };
            btns.Children.Add(removeBtn);

            Grid.SetColumn(btns, 1);
            grid.Children.Add(btns);
            row.Child = grid;
            LessonItemsPanel.Children.Add(row);
        }
    }

    private void AddLesson_Click(object sender, RoutedEventArgs e)
    {
        var title = NewLessonTitle.Text.Trim();
        if (string.IsNullOrEmpty(title)) return;

        _items.Add(new LessonItem
        {
            Title    = title,
            SubTitle = NewLessonSubTitle.Text.Trim(),
            SortOrder = _items.Count
        });

        NewLessonTitle.Clear();
        NewLessonSubTitle.Clear();
        NewLessonTitle.Focus();
        RefreshItemsList();
    }

    // -------------------------------------------------------------------------
    // Resources
    // -------------------------------------------------------------------------

    private void LoadResources()
    {
        var resources = _db.GetResources(_subject.Id);
        ResourceListBox.ItemsSource    = resources;
        NoResourcesLabel.Visibility    = resources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResourceListBox.Visibility     = resources.Count > 0  ? Visibility.Visible : Visibility.Collapsed;
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
