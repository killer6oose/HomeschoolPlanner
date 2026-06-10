using System.IO;
using System.Windows;
using System.Windows.Threading;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Helpers;

namespace HomeschoolPlanner;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HomeschoolPlanner", "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Wire up global crash handlers before anything else runs
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            WriteCrashLog("UnhandledException", ex.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, ex) =>
        {
            WriteCrashLog("DispatcherUnhandledException", ex.Exception);
            ex.Handled = true; // keep the process alive so the log can be written
            MessageBox.Show(
                $"The application encountered an error and needs to close.\n\nDetails saved to:\n{LogPath}",
                "Homeschool Planner - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        };

        try
        {
            base.OnStartup(e);

            // Load saved settings and apply theme/font before any window opens
            var db = new DatabaseService();
            AppState.Settings = db.GetSettings();
            ThemeManager.Apply(AppState.Settings);
        }
        catch (Exception ex)
        {
            WriteCrashLog("OnStartup", ex);
            MessageBox.Show(
                $"Startup failed.\n\nDetails saved to:\n{LogPath}",
                "Homeschool Planner - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void WriteCrashLog(string source, Exception? ex)
    {
        try
        {
            var lines = new[]
            {
                $"=== Crash @ {DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] ===",
                $"OS:      {Environment.OSVersion}",
                $"Runtime: {Environment.Version}",
                $"BaseDir: {AppDomain.CurrentDomain.BaseDirectory}",
                $"Type:    {ex?.GetType().FullName}",
                $"Message: {ex?.Message}",
                "Stack:",
                ex?.StackTrace ?? "(no stack)",
                ex?.InnerException is { } inner
                    ? $"Inner: {inner.GetType().FullName}: {inner.Message}\n{inner.StackTrace}"
                    : "",
                ""
            };
            File.AppendAllLines(LogPath, lines);
        }
        catch
        {
            // If we can't write the log, there's nothing more we can do
        }
    }
}
