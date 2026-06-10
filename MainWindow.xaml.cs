using System.Windows;
using System.Windows.Controls;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Dialogs;
using HomeschoolPlanner.Helpers;
using HomeschoolPlanner.Models;
using HomeschoolPlanner.Services;
using HomeschoolPlanner.Views;

namespace HomeschoolPlanner;

public partial class MainWindow : Window
{
    private readonly DatabaseService _db = new();
    private string _currentView = "Week";
    private DateTime _currentDate = DateTime.Today;

    // Single-student mode
    private Student? _selectedStudent;

    // Multi-student split-view mode
    private bool _splitViewActive = false;
    private List<Student> _splitStudents = new();

    // View instances kept alive so they don't rebuild from scratch on every switch
    private WeekView?  _weekView;
    private DayView?   _dayView;
    private MonthView? _monthView;

    public MainWindow()
    {
        InitializeComponent();
        // Defer student loading until after the window is shown - dialogs need
        // an already-visible owner window or WPF throws InvalidOperationException
        Loaded += async (_, _) =>
        {
            var ver = typeof(MainWindow).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion ?? "?";
            LogService.LogEvent("App", $"Started v{ver}");
            LoadStudents();
            ShowView("Week");
            // Fire update check after UI is up - runs in background, won't block startup
            await UpdateChecker.CheckAsync(this);
        };
    }

    // -------------------------------------------------------------------------
    // Student loading
    // -------------------------------------------------------------------------

    private void LoadStudents()
    {
        var students = _db.GetStudents();
        StudentCombo.ItemsSource = students;

        if (students.Count > 0)
        {
            StudentCombo.SelectedIndex = 0;
            _selectedStudent = students[0];
        }
        else
        {
            // No students - show the welcome screen instead of auto-opening dialogs
            ShowWelcomeScreen();
        }
    }

    // -------------------------------------------------------------------------
    // Welcome screen (shown on first launch when no students exist)
    // -------------------------------------------------------------------------

    private void ShowWelcomeScreen()
    {
        var welcome = new WelcomeStartupDialog { Owner = this };
        welcome.ShowDialog();

        switch (welcome.ChosenAction)
        {
            case WelcomeAction.AddStudent:
                new ManageStudentsDialog(_db) { Owner = this }.ShowDialog();
                ReloadStudents();
                ShowWelcomeScreen();   // return to welcome after dialog closes
                break;

            case WelcomeAction.AddSubject:
                if (_selectedStudent != null)
                {
                    var subs = _db.GetSubjects(_selectedStudent.Id, activeOnly: false);
                    new AddClassDialog(_db, _selectedStudent, subs, DateTime.Today) { Owner = this }.ShowDialog();
                }
                ShowWelcomeScreen();   // return to welcome after dialog closes
                break;

            case WelcomeAction.TakeTour:
                StartTour(onComplete: ShowWelcomeScreen);
                break;

            // WelcomeAction.None = user closed the dialog - just continue to the planner
        }
    }

    // -------------------------------------------------------------------------
    // Tour - called from welcome screen and File > Take a Tour
    // -------------------------------------------------------------------------

    private void StartTour(Action? onComplete = null)
    {
        var tour = new WalkthroughOverlay(this);
        tour.WalkthroughCompleted += (_, _) =>
        {
            // Mark seen the first time
            var s = _db.GetSettings();
            if (!s.HasSeenWalkthrough)
            {
                s.HasSeenWalkthrough = true;
                _db.SaveSettings(s);
            }
            onComplete?.Invoke();
        };
        tour.Begin();
    }

    private void TakeTour_Click(object sender, RoutedEventArgs e) => StartTour();

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        LogService.LogEvent("Help", "Opened Report a Problem dialog");
        new ReportProblemDialog { Owner = this }.ShowDialog();
    }

    private void ReloadStudents()
    {
        var students = _db.GetStudents();
        StudentCombo.ItemsSource = students;

        if (students.Count > 0)
        {
            var match = students.FirstOrDefault(s => s.Id == _selectedStudent?.Id);
            StudentCombo.SelectedItem = match ?? students[0];
            _selectedStudent = (Student?)StudentCombo.SelectedItem;
        }
        else
        {
            _selectedStudent = null;
        }

        // Validate split-view students still exist
        if (_splitViewActive)
        {
            _splitStudents = _splitStudents
                .Where(ss => students.Any(s => s.Id == ss.Id))
                .ToList();
            if (_splitStudents.Count < 2)
                ExitSplitView();
        }

        InvalidateViews();
        RefreshCurrentView();
    }

    private void StudentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_splitViewActive) return; // Ignore combo changes while in split mode
        _selectedStudent = StudentCombo.SelectedItem as Student;
        if (_selectedStudent != null)
            LogService.LogEvent("Navigate", $"Student selected: {_selectedStudent.Name}");
        InvalidateViews();
        RefreshCurrentView();
    }

    // -------------------------------------------------------------------------
    // Split view
    // -------------------------------------------------------------------------

    private void SplitView_Click(object sender, RoutedEventArgs e)
    {
        if (_splitViewActive)
        {
            ExitSplitView();
            return;
        }

        var students = _db.GetStudents();
        if (students.Count < 2)
        {
            MessageBox.Show("Add at least two students before using split view.", "Split View",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SelectStudentsDialog(students);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true && dlg.SelectedStudents.Count >= 2)
        {
            _splitStudents   = dlg.SelectedStudents;
            _splitViewActive = true;

            // Update toolbar appearance
            SplitViewBtn.Content    = "Single View";
            SingleStudentPanel.Visibility = Visibility.Collapsed;
            SplitViewPanel.Visibility     = Visibility.Visible;
            SplitStudentLabel.Text  = string.Join(", ", _splitStudents.Select(s => s.Name));

            InvalidateViews();
            RefreshCurrentView();
        }
    }

    private void ExitSplitView()
    {
        _splitViewActive = false;
        _splitStudents   = new();
        SplitViewBtn.Content          = "Split View";
        SingleStudentPanel.Visibility = Visibility.Visible;
        SplitViewPanel.Visibility     = Visibility.Collapsed;

        InvalidateViews();
        RefreshCurrentView();
    }

    // Returns the active student list for the current mode
    private List<Student> ActiveStudents()
    {
        if (_splitViewActive && _splitStudents.Count > 0)
            return _splitStudents;
        if (_selectedStudent != null)
            return new List<Student> { _selectedStudent };
        return new List<Student>();
    }

    // -------------------------------------------------------------------------
    // View management
    // -------------------------------------------------------------------------

    private void InvalidateViews()
    {
        _weekView  = null;
        _dayView   = null;
        _monthView = null;
    }

    private void ShowView(string viewName)
    {
        _currentView = viewName;
        UpdateViewToggleStyles();
        UpdatePeriodLabel();
        RefreshCurrentView();
    }

    private void RefreshCurrentView()
    {
        var students = ActiveStudents();
        if (students.Count == 0)
        {
            MainContent.Content = new TextBlock
            {
                Text                = "Add a student to get started.",
                FontSize            = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            return;
        }

        switch (_currentView)
        {
            case "Week":
                if (_weekView == null)
                    _weekView = new WeekView(_db, students);
                else
                    _weekView.SetStudents(students);
                _weekView.LoadWeek(_currentDate);
                MainContent.Content = _weekView;
                break;

            case "Day":
                if (_dayView == null)
                    _dayView = new DayView(_db, students);
                else
                    _dayView.SetStudents(students);
                _dayView.LoadDay(_currentDate);
                MainContent.Content = _dayView;
                break;

            case "Month":
                if (_monthView == null)
                {
                    _monthView = new MonthView(_db, students);
                    // Clicking a day in month view switches to day view for that date
                    _monthView.DayClicked += date =>
                    {
                        _currentDate = date;
                        ShowView("Day");
                    };
                }
                else
                {
                    _monthView.SetStudents(students);
                }
                _monthView.LoadMonth(_currentDate);
                MainContent.Content = _monthView;
                break;
        }
    }

    private void UpdatePeriodLabel()
    {
        switch (_currentView)
        {
            case "Day":
                PeriodLabel.Text = _currentDate.ToString("dddd, MMMM d, yyyy");
                break;

            case "Week":
                var weekStart = GetMonday(_currentDate);
                var weekEnd   = weekStart.AddDays(4);
                if (weekStart.Month == weekEnd.Month)
                    PeriodLabel.Text = $"{weekStart:MMMM d} - {weekEnd:d, yyyy}";
                else if (weekStart.Year == weekEnd.Year)
                    PeriodLabel.Text = $"{weekStart:MMM d} - {weekEnd:MMM d, yyyy}";
                else
                    PeriodLabel.Text = $"{weekStart:MMM d, yyyy} - {weekEnd:MMM d, yyyy}";
                break;

            case "Month":
                PeriodLabel.Text = _currentDate.ToString("MMMM yyyy");
                break;
        }
    }

    private void UpdateViewToggleStyles()
    {
        var active = (Style)FindResource("PrimaryButtonStyle");
        var normal = (Style)FindResource("NavButtonStyle");
        DayViewBtn.Style   = _currentView == "Day"   ? active : normal;
        WeekViewBtn.Style  = _currentView == "Week"  ? active : normal;
        MonthViewBtn.Style = _currentView == "Month" ? active : normal;
    }

    // -------------------------------------------------------------------------
    // Navigation
    // -------------------------------------------------------------------------

    private void PrevPeriod_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = _currentView switch
        {
            "Day"   => _currentDate.AddDays(-1),
            "Week"  => _currentDate.AddDays(-7),
            "Month" => _currentDate.AddMonths(-1),
            _       => _currentDate
        };
        UpdatePeriodLabel();
        RefreshCurrentView();
    }

    private void NextPeriod_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = _currentView switch
        {
            "Day"   => _currentDate.AddDays(1),
            "Week"  => _currentDate.AddDays(7),
            "Month" => _currentDate.AddMonths(1),
            _       => _currentDate
        };
        UpdatePeriodLabel();
        RefreshCurrentView();
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = DateTime.Today;
        UpdatePeriodLabel();
        RefreshCurrentView();
    }

    private void SwitchView_Click(object sender, RoutedEventArgs e)
    {
        var view = (string)((Button)sender).Tag;
        LogService.LogEvent("Navigate", $"Switched to {view} view");
        ShowView(view);
    }

    // -------------------------------------------------------------------------
    // Manage dialogs
    // -------------------------------------------------------------------------

    private void Preferences_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PreferencesDialog(_db) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            // Theme/font already applied by PreferencesDialog.Apply_Click
            // Rebuild views so colors refresh
            InvalidateViews();
            RefreshCurrentView();
        }
    }

    private void SchoolSettings_Click(object sender, RoutedEventArgs e)
    {
        LogService.LogEvent("Dialog", "Opened School Settings");
        var dlg = new SchoolSettingsDialog(_db, 0) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            InvalidateViews();
            RefreshCurrentView();
        }
    }

    private void Resources_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ResourcesDialog(_db) { Owner = this };
        dlg.ShowDialog();
    }

    private void Reports_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ReportsDialog(_db) { Owner = this };
        dlg.ShowDialog();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AboutDialog { Owner = this };
        dlg.ShowDialog();
    }

    private void ManageStudents_Click(object sender, RoutedEventArgs e)
    {
        LogService.LogEvent("Dialog", "Opened Manage Students");
        var dlg = new ManageStudentsDialog(_db);
        dlg.Owner = this;
        dlg.ShowDialog();
        ReloadStudents();
    }

    private void ManageSubjects_Click(object sender, RoutedEventArgs e)
    {
        LogService.LogEvent("Dialog", "Opened Manage Subjects");
        // In split mode, pick which student's subjects to manage
        Student? target = _splitViewActive
            ? PickStudentForSubjectManagement()
            : _selectedStudent;

        if (target == null)
        {
            MessageBox.Show("Select a student first.", "No student",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new ManageSubjectsDialog(_db, target);
        dlg.Owner = this;
        dlg.ShowDialog();
        InvalidateViews();
        RefreshCurrentView();
    }

    // When in split mode, ask which student to manage subjects for
    private Student? PickStudentForSubjectManagement()
    {
        if (_splitStudents.Count == 1) return _splitStudents[0];

        var picker = new Window
        {
            Title                   = "Manage Subjects For",
            Width                   = 280,
            Height                  = 200,
            WindowStartupLocation   = WindowStartupLocation.CenterOwner,
            Owner                   = this,
            Background              = System.Windows.Media.Brushes.White,
            ResizeMode              = ResizeMode.NoResize
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text     = "Which student?",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin   = new Thickness(0, 0, 0, 12)
        });

        Student? result = null;
        foreach (var s in _splitStudents)
        {
            var capturedStudent = s;
            var btn = new Button
            {
                Content = s.Name,
                Style   = (Style)FindResource("NavButtonStyle"),
                Margin  = new Thickness(0, 0, 0, 8)
            };
            btn.Click += (_, _) => { result = capturedStudent; picker.DialogResult = true; };
            panel.Children.Add(btn);
        }

        picker.Content = panel;
        picker.ShowDialog();
        return result;
    }

    private static DateTime GetMonday(DateTime date)
    {
        int diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff).Date;
    }
}
