namespace M365Trace.Rules;

public sealed record RulesetManifest
{
    public required string RulesetVersion { get; init; }

    public required int SchemaVersion { get; init; }

    public required string MinimumApplicationVersion { get; init; }

    public required int SessionClassificationCount { get; init; }

    public required DateOnly Released { get; init; }
}
