using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace M365Trace.Web.Services;

public sealed class VersionUpdateService
{
    private const string GitHubClientName = "GitHubReleases";
    private const string LatestReleasePath =
        "repos/jprknight/M365-HAR-Viewer/releases/latest";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly object _sync = new();
    private Task<VersionCheckResult>? _cachedCheck;

    public VersionUpdateService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string CurrentVersion { get; } = GetCurrentVersion();

    public Task<VersionCheckResult> CheckForUpdatesAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            return _cachedCheck ??= CheckCoreAsync(cancellationToken);
        }
    }

    private async Task<VersionCheckResult> CheckCoreAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(GitHubClientName);
            using var response = await client.GetAsync(
                LatestReleasePath,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new VersionCheckResult
                {
                    State = VersionCheckState.NoPublishedRelease,
                    CurrentVersion = CurrentVersion,
                    Detail = "No published GitHub release is available yet."
                };
            }

            response.EnsureSuccessStatusCode();
            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(
                cancellationToken);

            if (release is null
                || !TryParseVersion(release.TagName, out var latestVersion))
            {
                return Unavailable(
                    "The latest GitHub release did not contain a valid semantic version tag.");
            }

            if (!TryParseVersion(CurrentVersion, out var currentVersion))
            {
                return Unavailable(
                    "The running application version could not be compared.");
            }

            var releaseUri = Uri.TryCreate(
                release.HtmlUrl,
                UriKind.Absolute,
                out var parsedReleaseUri)
                && parsedReleaseUri.Scheme == Uri.UriSchemeHttps
                ? parsedReleaseUri
                : null;
            var updateAvailable = latestVersion > currentVersion;

            return new VersionCheckResult
            {
                State = updateAvailable
                    ? VersionCheckState.UpdateAvailable
                    : VersionCheckState.Latest,
                CurrentVersion = CurrentVersion,
                LatestVersion = NormalizeVersion(release.TagName),
                ReleaseUri = releaseUri,
                Detail = updateAvailable
                    ? "A newer published release is available."
                    : "This is the latest published release."
            };
        }
        catch (Exception exception) when (
            exception is HttpRequestException
            or TaskCanceledException
            or JsonException)
        {
            return Unavailable(
                "The GitHub release check could not be completed. Trace analysis remains available offline.");
        }
    }

    private VersionCheckResult Unavailable(string detail) => new()
    {
        State = VersionCheckState.Unavailable,
        CurrentVersion = CurrentVersion,
        Detail = detail
    };

    private static string GetCurrentVersion()
    {
        var assembly = typeof(VersionUpdateService).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var version = informationalVersion
            ?? assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";

        return NormalizeVersion(version);
    }

    private static bool TryParseVersion(
        string? value,
        out Version version) =>
        Version.TryParse(NormalizeVersion(value), out version!);

    private static string NormalizeVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "0.0.0";
        }

        var normalized = value.Trim().TrimStart('v', 'V');
        var metadataIndex = normalized.IndexOfAny(['-', '+']);
        return metadataIndex >= 0
            ? normalized[..metadataIndex]
            : normalized;
    }

    private sealed record GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }
    }
}
