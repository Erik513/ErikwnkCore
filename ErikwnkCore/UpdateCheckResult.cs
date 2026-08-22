namespace ErikwnkCore.Updater;

public sealed record UpdateCheckResult(
    Version LatestVersion,
    string ReleaseUrl,
    string? DownloadUrl);
