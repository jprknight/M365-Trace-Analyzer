using M365Trace.Core;

namespace M365Trace.Web.Services;

public sealed record TraceSummary
{
    public required int TotalSessions { get; init; }

    public DateTimeOffset? TraceStart { get; init; }

    public DateTimeOffset? TraceEnd { get; init; }

    public TimeSpan TraceWindow { get; init; }

    public required int SessionsWithFindings { get; init; }

    public required int SlowSessions { get; init; }

    public required TraceSeveritySummary Severities { get; init; }

    public required TraceStatusSummary Statuses { get; init; }

    public IReadOnlyList<TraceNamedCount> FailingHosts { get; init; } = [];

    public IReadOnlyList<TraceFindingCount> HighImpactFindings { get; init; } = [];

    public IReadOnlyList<TraceSessionSummary> SlowestSessions { get; init; } = [];

    public IReadOnlyList<TraceNamedCount> AuthenticationTypes { get; init; } = [];
}

public sealed record TraceSeveritySummary(
    int Severe,
    int Concerning,
    int Warning,
    int Normal,
    int Other);

public sealed record TraceStatusSummary(
    int NoResponse,
    int Success,
    int Redirect,
    int ClientError,
    int ServerError,
    int Other);

public sealed record TraceNamedCount(string Name, int Count);

public sealed record TraceFindingCount(string RuleId, int Count);

public sealed record TraceSessionSummary(
    int SessionId,
    string Method,
    string Host,
    string Path,
    TimeSpan Duration);
