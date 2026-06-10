using System.IO;
using System.Text;

namespace HomeschoolPlanner.Services;

/// <summary>
/// Writes timestamped entries to a daily rolling log file.
/// Call LogEvent() for app actions and LogDbError() for database failures.
/// </summary>
public static class LogService
{
    private static readonly string LogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "HomeschoolPlanner", "logs");

    private static string LogFilePath =>
        Path.Combine(LogDirectory, $"app-{DateTime.Today:yyyy-MM-dd}.log");

    // -------------------------------------------------------------------------

    public static void LogEvent(string category, string message)
        => Write("EVENT", $"[{category}] {message}");

    public static void LogDbError(string operation, Exception ex)
        => Write("DB_ERROR", $"{operation} - {ex.Message}");

    public static void LogError(string context, Exception ex)
        => Write("ERROR", $"{context} - {ex.GetType().Name}: {ex.Message}");

    // -------------------------------------------------------------------------

    /// <summary>Returns the last <paramref name="lines"/> lines across today and yesterday.</summary>
    public static string GetRecentLog(int lines = 80)
    {
        var files = new[]
        {
            Path.Combine(LogDirectory, $"app-{DateTime.Today.AddDays(-1):yyyy-MM-dd}.log"),
            LogFilePath
        };

        var all = new List<string>();
        foreach (var f in files)
            if (File.Exists(f))
                all.AddRange(File.ReadAllLines(f));

        return all.Count == 0
            ? "(no log entries)"
            : string.Join('\n', all.TakeLast(lines));
    }

    // -------------------------------------------------------------------------

    private static readonly object _lock = new();

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
            lock (_lock)
                File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch { /* never crash the app over a logging failure */ }
    }
}
