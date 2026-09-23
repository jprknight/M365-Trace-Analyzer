namespace M365Trace.Core;

public sealed record TraceFinding
{
    public required string RuleId { get; init; }

    public required string Title { get; init; }

    public required TraceSeverity Severity { get; init; }

    public string? Description { get; init; }

    public string? Recommendation { get; init; }

    public string? Evidence { get; init; }

    public IReadOnlyList<FindingLink> Links { get; init; } = [];
}
