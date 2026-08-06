namespace GitHubSelfUpdater;

public sealed record UpdateCheckResult(
    Version LatestVersion,
    string ReleaseUrl,
    string? DownloadUrl);
