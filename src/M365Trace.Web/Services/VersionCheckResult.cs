namespace M365Trace.Web.Services;

public enum VersionCheckState
{
    Latest,
    UpdateAvailable,
    NoPublishedRelease,
    Unavailable
}

public sealed record VersionCheckResult
{
    public required VersionCheckState State { get; init; }

    public required string CurrentVersion { get; init; }

    public string? LatestVersion { get; init; }

    public Uri? ReleaseUri { get; init; }

    public string? Detail { get; init; }
}
