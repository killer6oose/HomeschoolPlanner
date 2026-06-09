using System.Windows;
using HomeschoolPlanner.Data;
using HomeschoolPlanner.Helpers;

namespace HomeschoolPlanner;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load saved settings and apply theme/font before any window opens
        var db = new DatabaseService();
        AppState.Settings = db.GetSettings();
        ThemeManager.Apply(AppState.Settings);
    }
}
