using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HomeschoolPlanner.Services;
using Microsoft.Win32;

namespace HomeschoolPlanner.Dialogs;

public partial class ReportProblemDialog : Window
{
    private string? _screenshotPath;

    public ReportProblemDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshLogPreview();
    }

    // -------------------------------------------------------------------------
    // Log preview
    // -------------------------------------------------------------------------

    private void LogCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (LogPreviewBorder == null) return;
        LogPreviewBorder.Visibility = IncludeLogCheck.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshLogPreview()
        => LogPreviewText.Text = LogService.GetRecentLog(60);

    // -------------------------------------------------------------------------
    // Screenshot
    // -------------------------------------------------------------------------

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select a screenshot",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            SetScreenshot(dlg.FileName);
    }

    private void ScreenshotDrop_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void ScreenshotDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            SetScreenshot(files[0]);
    }

    private void SetScreenshot(string path)
    {
        _screenshotPath = path;
        ScreenshotPlaceholder.Visibility = Visibility.Collapsed;
        ScreenshotPreview.Visibility     = Visibility.Visible;
        ScreenshotName.Visibility        = Visibility.Visible;
        ClearScreenshotBtn.Visibility    = Visibility.Visible;

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource       = new Uri(path);
            bmp.CacheOption     = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 240;
            bmp.EndInit();
            ScreenshotPreview.Source = bmp;
        }
        catch
        {
            ScreenshotPreview.Source = null;
        }

        ScreenshotName.Text = Path.GetFileName(path);
    }

    private void ClearScreenshot_Click(object sender, RoutedEventArgs e)
    {
        _screenshotPath                  = null;
        ScreenshotPreview.Source         = null;
        ScreenshotPlaceholder.Visibility = Visibility.Visible;
        ScreenshotPreview.Visibility     = Visibility.Collapsed;
        ScreenshotName.Visibility        = Visibility.Collapsed;
        ClearScreenshotBtn.Visibility    = Visibility.Collapsed;
    }

    // -------------------------------------------------------------------------
    // Submit
    // -------------------------------------------------------------------------

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            ShowStatus("Please enter a short description before submitting.", isError: true);
            TitleBox.Focus();
            return;
        }

        var categoryItem = CategoryCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
        var category     = categoryItem?.Tag?.ToString() ?? "Other";
        var details      = DetailsBox.Text.Trim();
        var log          = IncludeLogCheck.IsChecked == true ? LogService.GetRecentLog(80) : "(log not included)";
        var contactName  = ContactNameBox.Text.Trim();
        var contactEmail = ContactEmailBox.Text.Trim();

        byte[]? screenshotBytes = null;
        string? screenshotFilename = null;
        if (_screenshotPath != null && File.Exists(_screenshotPath))
        {
            try
            {
                screenshotBytes   = await File.ReadAllBytesAsync(_screenshotPath);
                screenshotFilename = Path.GetFileName(_screenshotPath);
            }
            catch { /* skip screenshot on read failure */ }
        }

        SetSubmitting(true);

        try
        {
            var issueNum = await GitHubIssueService.CreateIssueAsync(
                title, category, details, log, contactName, contactEmail,
                screenshotBytes, screenshotFilename);

            ShowStatus($"Report submitted - thank you! (Issue #{issueNum})", isError: false);
            SubmitBtn.Visibility = Visibility.Collapsed;
            CancelBtn.Content    = "Close";
            CancelBtn.IsEnabled  = true;
            LogService.LogEvent("Help", $"Problem report submitted as issue #{issueNum}");
        }
        catch (Exception ex)
        {
            LogService.LogError("ReportProblemDialog.Submit", ex);
            ShowStatus(
                "Something went wrong sending the report. Please try again, or visit github.com/killer6oose/HomeschoolPlanner/issues to report manually.",
                isError: true);
            SetSubmitting(false);
        }
    }

    private void SetSubmitting(bool active)
    {
        SubmitBtn.IsEnabled = !active;
        CancelBtn.IsEnabled = !active;
        SubmitBtn.Content   = active ? "Submitting..." : "Submit Report";
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text       = message;
        StatusText.Foreground = isError
            ? new SolidColorBrush(Color.FromRgb(220, 80, 80))
            : new SolidColorBrush(Color.FromRgb(80, 180, 100));
        StatusText.Visibility = Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
