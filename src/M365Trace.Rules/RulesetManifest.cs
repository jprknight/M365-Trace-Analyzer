namespace M365Trace.Rules;

public sealed record RulesetManifest
{
    public required int SchemaVersion { get; init; }

    public required int SessionClassificationCount { get; init; }
}
