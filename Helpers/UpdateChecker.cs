using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using HomeschoolPlanner.Dialogs;

namespace HomeschoolPlanner.Helpers;

public static class UpdateChecker
{
    private const string VersionUrl   = "https://hsrc.thehattons.co/releases/version.txt";
    private const string InstallerUrl = "https://hsrc.thehattons.co/releases/HomeschoolPlannerSetup.exe";
    private const string ChangelogUrl  = "https://hsrc.thehattons.co/releases/changelog-latest.txt";

    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly()
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?.Split('+')[0]   // strip git commit hash if present
                .Trim()
        ?? "1.0.0";

    /// <summary>
    /// Fetches the latest changelog markdown from the server. Returns null on network failure.
    /// </summary>
    public static async Task<string?> FetchChangelogAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            return (await http.GetStringAsync(ChangelogUrl)).Trim();
        }
        catch { return null; }
    }

    /// <summary>
    /// Checks for a newer version silently. Prompts the user only if one is found.
    /// Safe to fire-and-forget from the UI thread via async void.
    /// </summary>
    public static async Task CheckAsync(Window owner)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var raw    = (await http.GetStringAsync(VersionUrl)).Trim();
            var latest = raw.TrimStart('v').Trim(); // tolerate "v1.2.3" format

            if (!Version.TryParse(latest, out var latestVer))   return;
            if (!Version.TryParse(CurrentVersion, out var curr)) return;
            if (latestVer <= curr) return;

            var answer = MessageBox.Show(
                $"Version {latest} is available (you have {CurrentVersion}).\n\nDownload and install now?",
                "Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (answer == MessageBoxResult.Yes)
                await DownloadAndInstallAsync(owner, latest);
        }
        catch
        {
            // Non-critical - silently swallow network/parse errors
        }
    }

    private static async Task DownloadAndInstallAsync(Window owner, string version)
    {
        var tempFile = Path.Combine(
            Path.GetTempPath(),
            $"HomeschoolPlannerSetup_{version}.exe");

        var progress = new UpdateProgressDialog(version);
        progress.Owner = owner;
        progress.Show();

        try
        {
            using (var http = new HttpClient())
            using (var response = await http.GetAsync(InstallerUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1L;

                using (var src  = await response.Content.ReadAsStreamAsync())
                using (var dest = File.Create(tempFile))
                {
                    var  buf      = new byte[81920];
                    long received = 0;
                    int  read;

                    while ((read = await src.ReadAsync(buf)) > 0)
                    {
                        await dest.WriteAsync(buf.AsMemory(0, read));
                        received += read;
                        if (total > 0)
                            progress.SetProgress((double)received / total * 100);
                    }
                } // dest and src are fully closed/flushed here
            }

            progress.Close();

            // /VERYSILENT  - no installer UI
            // /CLOSEAPPLICATIONS - tells Inno Setup to close any running instance
            // /RESTARTAPPLICATIONS - Inno Setup re-launches the app after install
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = tempFile,
                Arguments       = "/VERYSILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true
            });

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            progress.Close();
            MessageBox.Show(
                $"Update failed:\n\n{ex.Message}",
                "Update Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
