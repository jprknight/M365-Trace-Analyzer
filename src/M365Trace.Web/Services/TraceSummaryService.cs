using M365Trace.Core;

namespace M365Trace.Web.Services;

public sealed class TraceSummaryService
{
    public const string PerformanceFindingRuleId =
        "M365.Performance.Duration";

    private const int MaximumRankedItems = 5;
    private const string UnclassifiedAuthentication = "Not classified";

    public TraceSummary Create(IReadOnlyCollection<TraceSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        var traceStart = sessions.Count == 0
            ? (DateTimeOffset?)null
            : sessions.Min(session => session.StartedAt);
        var traceEnd = sessions.Count == 0
            ? (DateTimeOffset?)null
            : sessions.Max(session => session.StartedAt + session.Duration);

        return new TraceSummary
        {
            TotalSessions = sessions.Count,
            TraceStart = traceStart,
            TraceEnd = traceEnd,
            TraceWindow = traceStart is null || traceEnd is null
                ? TimeSpan.Zero
                : traceEnd.Value - traceStart.Value,
            SessionsWithFindings = sessions.Count(session =>
                session.Analysis.HasFindings),
            SlowSessions = sessions.Count(session =>
                session.Analysis.Findings.Any(finding =>
                    string.Equals(
                        finding.RuleId,
                        PerformanceFindingRuleId,
                        StringComparison.Ordinal))),
            Severities = CreateSeveritySummary(sessions),
            Statuses = CreateStatusSummary(sessions),
            FailingHosts = CreateFailingHosts(sessions),
            HighImpactFindings = CreateHighImpactFindings(sessions),
            SlowestSessions = CreateSlowestSessions(sessions),
            AuthenticationTypes = CreateAuthenticationTypes(sessions)
        };
    }

    private static TraceSeveritySummary CreateSeveritySummary(
        IEnumerable<TraceSession> sessions)
    {
        var analyzedSessions = sessions.ToArray();
        var severe = analyzedSessions.Count(session =>
            session.Analysis.Severity == TraceSeverity.Severe);
        var concerning = analyzedSessions.Count(session =>
            session.Analysis.Severity == TraceSeverity.Concerning);
        var warning = analyzedSessions.Count(session =>
            session.Analysis.Severity == TraceSeverity.Warning);
        var normal = analyzedSessions.Count(session =>
            session.Analysis.Severity == TraceSeverity.Normal);
        var other = analyzedSessions.Length
            - severe
            - concerning
            - warning
            - normal;

        return new TraceSeveritySummary(
            severe,
            concerning,
            warning,
            normal,
            other);
    }

    private static TraceStatusSummary CreateStatusSummary(
        IEnumerable<TraceSession> sessions)
    {
        var noResponse = 0;
        var success = 0;
        var redirect = 0;
        var clientError = 0;
        var serverError = 0;
        var other = 0;

        foreach (var session in sessions)
        {
            switch (session.StatusCode)
            {
                case <= 0:
                    noResponse++;
                    break;
                case >= 200 and <= 299:
                    success++;
                    break;
                case >= 300 and <= 399:
                    redirect++;
                    break;
                case >= 400 and <= 499:
                    clientError++;
                    break;
                case >= 500 and <= 599:
                    serverError++;
                    break;
                default:
                    other++;
                    break;
            }
        }

        return new TraceStatusSummary(
            noResponse,
            success,
            redirect,
            clientError,
            serverError,
            other);
    }

    private static IReadOnlyList<TraceNamedCount> CreateFailingHosts(
        IEnumerable<TraceSession> sessions) =>
        sessions
            .Where(session =>
                session.StatusCode <= 0 || session.StatusCode >= 400)
            .GroupBy(
                session => session.Url.Host,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new TraceNamedCount(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumRankedItems)
            .ToArray();

    private static IReadOnlyList<TraceFindingCount> CreateHighImpactFindings(
        IEnumerable<TraceSession> sessions) =>
        sessions
            .SelectMany(session => session.Analysis.Findings)
            .Where(finding => finding.Severity >= TraceSeverity.Concerning)
            .GroupBy(
                finding => finding.RuleId,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new TraceFindingCount(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.RuleId, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumRankedItems)
            .ToArray();

    private static IReadOnlyList<TraceSessionSummary> CreateSlowestSessions(
        IEnumerable<TraceSession> sessions) =>
        sessions
            .OrderByDescending(session => session.Duration)
            .ThenBy(session => session.Id)
            .Take(MaximumRankedItems)
            .Select(session => new TraceSessionSummary(
                session.Id,
                session.Method,
                session.Url.Host,
                session.Url.PathAndQuery,
                session.Duration))
            .ToArray();

    private static IReadOnlyList<TraceNamedCount> CreateAuthenticationTypes(
        IEnumerable<TraceSession> sessions) =>
        sessions
            .GroupBy(
                session =>
                    string.IsNullOrWhiteSpace(session.Analysis.Authentication)
                        ? UnclassifiedAuthentication
                        : session.Analysis.Authentication.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new TraceNamedCount(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
