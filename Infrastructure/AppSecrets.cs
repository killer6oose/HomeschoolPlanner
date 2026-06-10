namespace HomeschoolPlanner.Infrastructure;

/// <summary>
/// Paste your GitHub PAT below. Needs 'issues: write' scope on the HomeschoolPlanner repo.
/// Classic token: GitHub -> Settings -> Developer settings -> Personal access tokens -> Tokens (classic)
/// Fine-grained: GitHub -> Settings -> Developer settings -> Personal access tokens -> Fine-grained tokens
///   -> Repository access: killer6oose/HomeschoolPlanner -> Permissions -> Issues: Read and write
/// </summary>
internal static class AppSecrets
{
    internal const string GitHubPat  = "PLACEHOLDER_PAT";
    internal const string GitHubOwner = "killer6oose";
    internal const string GitHubRepo  = "HomeschoolPlanner";
}
