using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace HomeschoolPlanner.Dialogs;

public partial class AboutDialog : Window
{
    private const string RepoUrl = "https://github.com/killer6oose/HomeschoolPlan";

    public AboutDialog()
    {
        InitializeComponent();

        var buildYear = DateTime.Now.Year;
        CopyrightLabel.Text = $"© {buildYear} Andrew Hatton";

        // Use assembly version if set, otherwise show build date
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "1.0.0";
        VersionLabel.Text = version;
    }

    private void RepoLink_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true });
        }
        catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
