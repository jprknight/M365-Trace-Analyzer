namespace M365Trace.Core;

public sealed record TraceAnalysisResult
{
    public static TraceAnalysisResult Empty { get; } = new();

    public TraceSeverity? Severity { get; init; }

    public string? SessionType { get; init; }

    public int SessionTypeConfidence { get; init; }

    public string? Authentication { get; init; }

    public int AuthenticationConfidence { get; init; }

    public string? ResponseServer { get; init; }

    public int ResponseServerConfidence { get; init; }

    public IReadOnlyList<TraceFinding> Findings { get; init; } = [];

    public bool HasFindings => Findings.Count > 0;
}
