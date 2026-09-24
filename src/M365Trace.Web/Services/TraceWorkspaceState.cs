using M365Trace.Core;

namespace M365Trace.Web.Services;

public sealed class TraceWorkspaceState(SessionQueryService queryService)
{
    private readonly List<TraceSession> _sessions = [];

    public IReadOnlyList<TraceSession> Sessions => _sessions;

    public IReadOnlyList<TraceSession> VisibleSessions =>
        queryService.Apply(_sessions, Query);

    public TraceSession? SelectedSession { get; private set; }

    public string? FileName { get; private set; }

    public SessionQuery Query { get; private set; } = new();

    public void ClearSessions()
    {
        _sessions.Clear();
        SelectedSession = null;
        FileName = null;
    }

    public void ReplaceSessions(
        string fileName,
        IEnumerable<TraceSession> sessions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(sessions);

        _sessions.Clear();
        _sessions.AddRange(sessions);
        SelectedSession = _sessions.FirstOrDefault();
        FileName = fileName;
    }

    public void SetFilter(string? freeText)
    {
        SetQuery(Query with { FreeText = freeText ?? string.Empty });
    }

    public void ClearFilter()
    {
        SetQuery(Query with { FreeText = string.Empty });
    }

    public void SetQuery(SessionQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        Query = query;
        ReconcileSelection();
    }

    public void ClearAllFilters()
    {
        SetQuery(new SessionQuery
        {
            SortColumn = Query.SortColumn,
            SortAscending = Query.SortAscending
        });
    }

    public void SetSort(SessionSortColumn column)
    {
        Query = Query.SortColumn == column
            ? Query with { SortAscending = !Query.SortAscending }
            : Query with
            {
                SortColumn = column,
                SortAscending = true
            };
    }

    public void SelectSession(TraceSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        SelectedSession = _sessions.FirstOrDefault(candidate =>
            candidate.Id == session.Id);
    }

    private void ReconcileSelection()
    {
        var visibleSessions = VisibleSessions;
        if (visibleSessions.Count == 0)
        {
            SelectedSession = null;
            return;
        }

        if (SelectedSession is null)
        {
            SelectedSession = GetFirstVisibleInTraceOrder(visibleSessions);
            return;
        }

        if (visibleSessions.Any(session =>
            session.Id == SelectedSession.Id))
        {
            return;
        }

        var selectedIndex = _sessions.FindIndex(session =>
            session.Id == SelectedSession.Id);
        var visibleIds = visibleSessions
            .Select(session => session.Id)
            .ToHashSet();

        for (var index = selectedIndex + 1; index < _sessions.Count; index++)
        {
            if (visibleIds.Contains(_sessions[index].Id))
            {
                SelectedSession = _sessions[index];
                return;
            }
        }

        for (var index = selectedIndex - 1; index >= 0; index--)
        {
            if (visibleIds.Contains(_sessions[index].Id))
            {
                SelectedSession = _sessions[index];
                return;
            }
        }

        SelectedSession = GetFirstVisibleInTraceOrder(visibleSessions);
    }

    private TraceSession? GetFirstVisibleInTraceOrder(
        IReadOnlyList<TraceSession> visibleSessions)
    {
        var visibleIds = visibleSessions
            .Select(session => session.Id)
            .ToHashSet();
        return _sessions.FirstOrDefault(session =>
            visibleIds.Contains(session.Id));
    }
}
