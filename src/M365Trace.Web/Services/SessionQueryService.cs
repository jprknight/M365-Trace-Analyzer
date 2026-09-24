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
        var filteredSessions = sessions.Where(session =>
            MatchesStructuredFilters(session, query)
            && (filter.Length == 0 || MatchesFreeText(session, filter)));

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

    private static bool MatchesStructuredFilters(
        TraceSession session,
        SessionQuery query) =>
        MatchesSeverity(session, query.Severities)
        && MatchesStatus(
            session,
            query.StatusFamilies,
            query.StatusCodes)
        && MatchesExactValue(session.Method, query.Methods)
        && MatchesExactValue(session.Url.Host, query.Hosts)
        && MatchesDuration(session.Duration, query.Duration)
        && MatchesExactValue(
            session.Analysis.SessionType,
            query.SessionTypes)
        && MatchesExactValue(
            NormalizeAuthentication(session.Analysis.Authentication),
            query.Authentications)
        && MatchesFindings(
            session,
            query.FindingRuleIds,
            query.Findings);

    private static bool MatchesSeverity(
        TraceSession session,
        IReadOnlyList<TraceSeverityFilter> filters) =>
        filters.Count == 0
        || filters.Any(filter => filter switch
        {
            TraceSeverityFilter.Severe =>
                session.Analysis.Severity == TraceSeverity.Severe,
            TraceSeverityFilter.Concerning =>
                session.Analysis.Severity == TraceSeverity.Concerning,
            TraceSeverityFilter.Warning =>
                session.Analysis.Severity == TraceSeverity.Warning,
            TraceSeverityFilter.Normal =>
                session.Analysis.Severity == TraceSeverity.Normal,
            TraceSeverityFilter.Other =>
                session.Analysis.Severity is not (
                    TraceSeverity.Severe
                    or TraceSeverity.Concerning
                    or TraceSeverity.Warning
                    or TraceSeverity.Normal),
            _ => false
        });

    private static bool MatchesStatus(
        TraceSession session,
        IReadOnlyList<TraceStatusFamily> families,
        IReadOnlyList<int> exactCodes) =>
        families.Count == 0 && exactCodes.Count == 0
        || exactCodes.Contains(session.StatusCode)
        || families.Any(family =>
            GetStatusFamily(session.StatusCode) == family);

    private static bool MatchesExactValue(
        string? value,
        IReadOnlyList<string> filters) =>
        filters.Count == 0
        || filters.Any(filter =>
            string.Equals(
                value,
                filter,
                StringComparison.OrdinalIgnoreCase));

    private static bool MatchesDuration(
        TimeSpan duration,
        SessionDurationFilter? filter) =>
        filter switch
        {
            null => true,
            SessionDurationFilter.UnderOneSecond =>
                duration < TimeSpan.FromSeconds(1),
            SessionDurationFilter.OneToFiveSeconds =>
                duration >= TimeSpan.FromSeconds(1)
                && duration < TimeSpan.FromSeconds(5),
            SessionDurationFilter.FiveToThirtySeconds =>
                duration >= TimeSpan.FromSeconds(5)
                && duration < TimeSpan.FromSeconds(30),
            SessionDurationFilter.ThirtySecondsOrMore =>
                duration >= TimeSpan.FromSeconds(30),
            _ => false
        };

    private static bool MatchesFindings(
        TraceSession session,
        IReadOnlyList<string> ruleIds,
        SessionFindingFilter? findingFilter)
    {
        if (findingFilter == SessionFindingFilter.HasFindings
            && !session.Analysis.HasFindings)
        {
            return false;
        }

        if (findingFilter == SessionFindingFilter.NoFindings
            && session.Analysis.HasFindings)
        {
            return false;
        }

        return ruleIds.Count == 0
            || session.Analysis.Findings.Any(finding =>
                ruleIds.Any(ruleId =>
                    string.Equals(
                        finding.RuleId,
                        ruleId,
                        StringComparison.OrdinalIgnoreCase)));
    }

    private static TraceStatusFamily GetStatusFamily(int statusCode) =>
        statusCode switch
        {
            <= 0 => TraceStatusFamily.NoResponse,
            >= 200 and <= 299 => TraceStatusFamily.Success,
            >= 300 and <= 399 => TraceStatusFamily.Redirect,
            >= 400 and <= 499 => TraceStatusFamily.ClientError,
            >= 500 and <= 599 => TraceStatusFamily.ServerError,
            _ => TraceStatusFamily.Other
        };

    public static string NormalizeAuthentication(string? authentication) =>
        string.IsNullOrWhiteSpace(authentication)
            ? "Not classified"
            : authentication.Trim();

    private static bool MatchesFreeText(TraceSession session, string filter)
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
