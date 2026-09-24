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
        Query = Query with { FreeText = freeText ?? string.Empty };
    }

    public void ClearFilter()
    {
        Query = Query with { FreeText = string.Empty };
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

    public TraceSession? GetPreviousVisibleSession() =>
        queryService.GetAdjacent(VisibleSessions, SelectedSession, -1);

    public TraceSession? GetNextVisibleSession() =>
        queryService.GetAdjacent(VisibleSessions, SelectedSession, 1);
}
