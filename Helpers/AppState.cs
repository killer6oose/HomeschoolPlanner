using HomeschoolPlanner.Models;

namespace HomeschoolPlanner.Helpers;

// Global app state - settings loaded once at startup and updated when the user saves preferences.
public static class AppState
{
    public static AppSettings Settings { get; set; } = new AppSettings();
}
