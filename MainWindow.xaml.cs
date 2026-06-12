using System.IO;
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
            // Show changelog if this is the first launch after an update
            await ShowChangelogIfUpdatedAsync();
            // Fire update check after UI is up - runs in background, won't block startup
            await UpdateChecker.CheckAsync(this);
        };
    }

    // Shows the changelog popup once per version - fires on the first launch after each update
    private async Task ShowChangelogIfUpdatedAsync()
    {
        var settings = AppState.Settings;
        var current  = UpdateChecker.CurrentVersion;
        if (settings.LastShownChangelogVersion == current) return;

        var markdown = await UpdateChecker.FetchChangelogAsync();
        if (string.IsNullOrWhiteSpace(markdown)) return;

        new ChangelogDialog(current, markdown) { Owner = this }.ShowDialog();

        settings.LastShownChangelogVersion = current;
        _db.SaveSettings(settings);
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

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HomeschoolPlanner");
        Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start("explorer.exe", folder);
    }

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
            SplitViewBtn.Content          = "Single View";
            SingleStudentPanel.Visibility = Visibility.Collapsed;
            SplitViewPanel.Visibility     = Visibility.Visible;
            SplitStudentLabel.Text        = string.Join(", ", _splitStudents.Select(s => s.Name));

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
                var weekStart = GetWeekStart(_currentDate);
                var weekEnd   = weekStart.AddDays(6);
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

    private void TodayBtn_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = DateTime.Today;
        UpdatePeriodLabel();
        RefreshCurrentView();
    }

    private void SwitchView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string view)
            ShowView(view);
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = DateTime.Today;
        UpdatePeriodLabel();
        RefreshCurrentView();
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var setting = AppState.Settings?.WeekStartDay ?? "Monday";
        if (setting == "CurrentDay") return date.Date;
        var anchor = setting switch
        {
            "Sunday"   => DayOfWeek.Sunday,
            "Saturday" => DayOfWeek.Saturday,
            _          => DayOfWeek.Monday
        };
        int diff = ((int)date.DayOfWeek - (int)anchor + 7) % 7;
        return date.AddDays(-diff).Date;
    }

    // -------------------------------------------------------------------------
    // Menu handlers
    // -------------------------------------------------------------------------

    private void Preferences_Click(object sender, RoutedEventArgs e)
    {
        new PreferencesDialog(_db) { Owner = this }.ShowDialog();
        // Re-apply theme/settings changes
        InvalidateViews();
        RefreshCurrentView();
    }

    private void ManageStudents_Click(object sender, RoutedEventArgs e)
    {
        new ManageStudentsDialog(_db) { Owner = this }.ShowDialog();
        ReloadStudents();
    }

    private void ManageSubjects_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStudent == null) return;
        // Reload student from DB in case grade changed
        var fresh = _db.GetStudents().FirstOrDefault(s => s.Id == _selectedStudent.Id) ?? _selectedStudent;
        new ManageSubjectsDialog(_db, fresh) { Owner = this }.ShowDialog();
        InvalidateViews();
        RefreshCurrentView();
    }

    private void SchoolSettings_Click(object sender, RoutedEventArgs e)
    {
        new SchoolSettingsDialog(_db) { Owner = this }.ShowDialog();
        InvalidateViews();
        RefreshCurrentView();
    }

    private void Resources_Click(object sender, RoutedEventArgs e)
    {
        new ResourcesDialog(_db) { Owner = this }.ShowDialog();
    }

    private void Reports_Click(object sender, RoutedEventArgs e)
    {
        new ReportsDialog(_db) { Owner = this }.ShowDialog();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutDialog { Owner = this }.ShowDialog();
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {

    }
}
