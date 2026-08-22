using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ErikwnkCore.Updater;

/// <summary>
/// Checks a GitHub repo's "latest release" endpoint for a newer tagged version.
/// Failures (offline, rate limited, malformed response, no release published yet)
/// are swallowed and reported as "no update found" since this check is meant to be
/// a best-effort convenience, never a hard requirement.
/// </summary>
public sealed class GitHubUpdateChecker
{
    private readonly string releasesApiUrl;
    private readonly string userAgentProductName;
    private readonly HttpClient httpClient;

    public GitHubUpdateChecker(
        string repositoryOwner,
        string repositoryName,
        HttpClient httpClient)
    {
        if (string.IsNullOrWhiteSpace(repositoryOwner))
        {
            throw new ArgumentException(
                "Repository owner is required.", nameof(repositoryOwner));
        }

        if (string.IsNullOrWhiteSpace(repositoryName))
        {
            throw new ArgumentException(
                "Repository name is required.", nameof(repositoryName));
        }

        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        userAgentProductName = repositoryName;
        releasesApiUrl =
            $"https://api.github.com/repos/{repositoryOwner}/{repositoryName}/releases/latest";

        AssemblyResolution.EnsureRegistered();
    }

    public async Task<UpdateCheckResult?> CheckForUpdateAsync(
        Version currentVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, releasesApiUrl);
            request.Headers.UserAgent.Add(
                new ProductInfoHeaderValue(userAgentProductName, currentVersion.ToString()));
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using HttpResponseMessage response = await httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using Stream stream = await response.Content
                .ReadAsStreamAsync()
                .ConfigureAwait(false);
            using JsonDocument json = await JsonDocument
                .ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            JsonElement root = json.RootElement;
            string? tagName = root.TryGetProperty("tag_name", out JsonElement tagElement)
                ? tagElement.GetString()
                : null;
            string? releaseUrl = root.TryGetProperty("html_url", out JsonElement urlElement)
                ? urlElement.GetString()
                : null;
            string? downloadUrl = GetExeAssetDownloadUrl(root);

            return UpdateVersionParser.TryParseNewerRelease(
                tagName,
                releaseUrl,
                downloadUrl,
                currentVersion);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Finds the .exe attached to the release, if any, so it can be downloaded and
    /// swapped in automatically. Returns null (the caller then just links to the
    /// release page) if no exe asset was attached.
    /// </summary>
    private static string? GetExeAssetDownloadUrl(JsonElement releaseRoot)
    {
        if (!releaseRoot.TryGetProperty("assets", out JsonElement assets) ||
            assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string? name = asset.TryGetProperty("name", out JsonElement nameElement)
                ? nameElement.GetString()
                : null;

            if (name is null || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (asset.TryGetProperty("browser_download_url", out JsonElement urlElement))
            {
                return urlElement.GetString();
            }
        }

        return null;
    }
}
