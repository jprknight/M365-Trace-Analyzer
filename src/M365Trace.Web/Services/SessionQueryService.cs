using M365Trace.Core;

namespace M365Trace.Web.Services;

public sealed class SessionQueryService
{
    public IReadOnlyList<TraceSession> Apply(
        IEnumerable<TraceSession> sessions,
        SessionQuery query)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(query);

        var filter = query.FreeText.Trim();
        var filteredSessions = filter.Length == 0
            ? sessions
            : sessions.Where(session => MatchesFilter(session, filter));

        Func<TraceSession, object?> selector = query.SortColumn switch
        {
            SessionSortColumn.Id => session => session.Id,
            SessionSortColumn.Analysis => session =>
                (int?)session.Analysis.Severity ?? -1,
            SessionSortColumn.Status => session => session.StatusCode,
            SessionSortColumn.Method => session => session.Method,
            SessionSortColumn.Host => session => session.Url.Host,
            SessionSortColumn.Path => session => session.Url.PathAndQuery,
            SessionSortColumn.Duration => session => session.Duration.Ticks,
            _ => session => session.Id
        };

        return query.SortAscending
            ? filteredSessions
                .OrderBy(selector, SessionSortValueComparer.Instance)
                .ThenBy(session => session.Id)
                .ToArray()
            : filteredSessions
                .OrderByDescending(
                    selector,
                    SessionSortValueComparer.Instance)
                .ThenBy(session => session.Id)
                .ToArray();
    }

    public TraceSession? GetAdjacent(
        IReadOnlyList<TraceSession> visibleSessions,
        TraceSession? selectedSession,
        int offset)
    {
        ArgumentNullException.ThrowIfNull(visibleSessions);

        if (visibleSessions.Count == 0 || selectedSession is null || offset == 0)
        {
            return null;
        }

        var selectedIndex = -1;
        for (var index = 0; index < visibleSessions.Count; index++)
        {
            if (visibleSessions[index].Id == selectedSession.Id)
            {
                selectedIndex = index;
                break;
            }
        }

        var adjacentIndex = selectedIndex + offset;
        return selectedIndex >= 0
            && adjacentIndex >= 0
            && adjacentIndex < visibleSessions.Count
                ? visibleSessions[adjacentIndex]
                : null;
    }

    private static bool MatchesFilter(TraceSession session, string filter)
    {
        var analysis = session.Analysis;

        return ContainsFilter(session.Url.AbsoluteUri, filter)
            || ContainsFilter(session.Url.Host, filter)
            || ContainsFilter(session.Url.PathAndQuery, filter)
            || ContainsFilter(session.Method, filter)
            || ContainsFilter(session.StatusCode.ToString(), filter)
            || ContainsFilter(session.StatusText, filter)
            || ContainsFilter(GetSeverityLabel(analysis.Severity), filter)
            || ContainsFilter(analysis.SessionType, filter)
            || ContainsFilter(analysis.Authentication, filter)
            || ContainsFilter(analysis.ResponseServer, filter)
            || analysis.Findings.Any(finding =>
                ContainsFilter(finding.RuleId, filter)
                || ContainsFilter(finding.Title, filter)
                || ContainsFilter(finding.Description, filter)
                || ContainsFilter(finding.Recommendation, filter)
                || ContainsFilter(finding.Evidence, filter));
    }

    private static bool ContainsFilter(string? value, string filter) =>
        value?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true;

    private static string GetSeverityLabel(TraceSeverity? severity) =>
        severity switch
        {
            TraceSeverity.InternalError => "Internal error",
            TraceSeverity.Uninteresting => "Uninteresting",
            TraceSeverity.FalsePositive => "False positive",
            TraceSeverity.Normal => "Normal",
            TraceSeverity.Warning => "Warning",
            TraceSeverity.Concerning => "Concerning",
            TraceSeverity.Severe => "Severe",
            _ => "Not analyzed"
        };

    private sealed class SessionSortValueComparer : IComparer<object?>
    {
        public static SessionSortValueComparer Instance { get; } = new();

        public int Compare(object? left, object? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            if (left is string leftText && right is string rightText)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(
                    leftText,
                    rightText);
            }

            return Comparer<object>.Default.Compare(left, right);
        }
    }
}
