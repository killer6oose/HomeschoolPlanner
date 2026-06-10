using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HomeschoolPlanner.Infrastructure;

namespace HomeschoolPlanner.Services;

public static class GitHubIssueService
{
    private static readonly HttpClient _http = new();

    // Maps the ComboBox Tag values to GitHub label names.
    // GitHub will create these labels automatically on first use if they don't exist.
    private static readonly Dictionary<string, string[]> CategoryLabels = new()
    {
        ["Bug"]        = ["user-report", "bug"],
        ["Suggestion"] = ["user-report", "enhancement"],
        ["Question"]   = ["user-report", "question"],
        ["Other"]      = ["user-report"],
    };

    static GitHubIssueService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("HomeschoolPlanner");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AppSecrets.GitHubPat);
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    /// <summary>
    /// Creates a GitHub issue and returns the issue number on success.
    /// If <paramref name="screenshotBytes"/> is provided, attempts to upload it
    /// to .github/issue-screenshots/ in the repo (requires contents:write on the PAT).
    /// Throws <see cref="HttpRequestException"/> on network or API failure.
    /// </summary>
    public static async Task<int> CreateIssueAsync(
        string title,
        string category,
        string description,
        string recentLog,
        string? contactName      = null,
        string? contactEmail     = null,
        byte[]? screenshotBytes  = null,
        string? screenshotFilename = null)
    {
        var rawVersion = typeof(GitHubIssueService).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "unknown";
        var appVersion = rawVersion.Contains('+') ? rawVersion[..rawVersion.IndexOf('+')] : rawVersion;

        // Try to upload the screenshot before building the body so we can embed its URL
        string? screenshotUrl = null;
        if (screenshotBytes != null && screenshotFilename != null)
            screenshotUrl = await TryUploadScreenshotAsync(screenshotBytes, screenshotFilename);

        var body   = BuildBody(category, description, recentLog, appVersion,
                               contactName, contactEmail, screenshotUrl, screenshotFilename);
        var labels = CategoryLabels.TryGetValue(category, out var l) ? l : ["user-report"];

        var payload = new
        {
            title  = $"[{category}] {title}",
            body,
            labels
        };

        var json     = JsonSerializer.Serialize(payload);
        var content  = new StringContent(json, Encoding.UTF8, "application/json");
        var url      = $"https://api.github.com/repos/{AppSecrets.GitHubOwner}/{AppSecrets.GitHubRepo}/issues";
        var response = await _http.PostAsync(url, content);

        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("number").GetInt32();
    }

    // -------------------------------------------------------------------------
    // Screenshot upload
    // Uploads to .github/issue-screenshots/{timestamp}-{filename} via Contents API.
    // Requires the PAT to have "contents: write" scope.
    // Returns the raw GitHub URL on success, null on any failure.
    // -------------------------------------------------------------------------

    private static async Task<string?> TryUploadScreenshotAsync(byte[] imageBytes, string filename)
    {
        try
        {
            var safeName  = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{filename}";
            var repoPath  = $".github/issue-screenshots/{safeName}";
            var uploadUrl = $"https://api.github.com/repos/{AppSecrets.GitHubOwner}/{AppSecrets.GitHubRepo}/contents/{repoPath}";

            var payload = new
            {
                message = $"Upload issue screenshot: {safeName}",
                content = Convert.ToBase64String(imageBytes)
            };

            var json     = JsonSerializer.Serialize(payload);
            var response = await _http.PutAsync(uploadUrl,
                               new StringContent(json, Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode) return null;

            // Parse the download_url from the response rather than constructing it,
            // so the branch name is always correct regardless of repo defaults.
            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc    = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("content", out var contentEl) &&
                contentEl.TryGetProperty("download_url", out var urlEl))
            {
                return urlEl.GetString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    // -------------------------------------------------------------------------

    private static string BuildBody(
        string category,
        string notes,
        string log,
        string version,
        string? contactName,
        string? contactEmail,
        string? screenshotUrl,
        string? screenshotFilename)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Notes");
        sb.AppendLine(string.IsNullOrWhiteSpace(notes) ? "(none provided)" : notes);
        sb.AppendLine();

        if (screenshotUrl != null)
        {
            sb.AppendLine("## Screenshot");
            sb.AppendLine($"![Screenshot]({screenshotUrl})");
            sb.AppendLine();
        }
        else if (screenshotFilename != null)
        {
            sb.AppendLine("## Screenshot");
            sb.AppendLine($"*User attached a screenshot ({screenshotFilename}) but it could not be uploaded automatically. Follow up with the reporter to obtain it.*");
            sb.AppendLine();
        }

        var hasName  = !string.IsNullOrWhiteSpace(contactName);
        var hasEmail = !string.IsNullOrWhiteSpace(contactEmail);
        if (hasName || hasEmail)
        {
            sb.AppendLine("## Contact");
            if (hasName)  sb.AppendLine($"- **Name:** {contactName}");
            if (hasEmail) sb.AppendLine($"- **Email:** {contactEmail}");
            sb.AppendLine();
        }

        sb.AppendLine("## System Info");
        sb.AppendLine($"- **App version:** {version}");
        sb.AppendLine($"- **OS:** {Environment.OSVersion}");
        sb.AppendLine($"- **Date (local):** {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"- **.NET:** {Environment.Version}");
        sb.AppendLine();

        sb.AppendLine("## Recent Activity Log");
        sb.AppendLine("<details><summary>Click to expand</summary>");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine(log);
        sb.AppendLine("```");
        sb.AppendLine("</details>");

        return sb.ToString();
    }
}
