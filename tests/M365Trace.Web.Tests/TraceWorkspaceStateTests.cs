using M365Trace.Core;
using M365Trace.Web.Services;

namespace M365Trace.Web.Tests;

public sealed class TraceWorkspaceStateTests
{
    private readonly TraceWorkspaceState _state = new(new SessionQueryService());
    private readonly IReadOnlyList<TraceSession> _sessions =
    [
        CreateSession(1, "GET", "https://example.test/one"),
        CreateSession(2, "POST", "https://example.test/two"),
        CreateSession(3, "DELETE", "https://example.test/three")
    ];

    [Fact]
    public void ReplaceSessions_SetsFileAndInitialSelection()
    {
        _state.ReplaceSessions("sample.har", _sessions);

        Assert.Equal("sample.har", _state.FileName);
        Assert.Equal(_sessions, _state.Sessions);
        Assert.Same(_sessions[0], _state.SelectedSession);
    }

    [Fact]
    public void ClearSessions_PreservesQueryAndClearsTraceState()
    {
        _state.SetFilter("example");
        _state.SetSort(SessionSortColumn.Status);
        _state.ReplaceSessions("sample.har", _sessions);

        _state.ClearSessions();

        Assert.Empty(_state.Sessions);
        Assert.Null(_state.SelectedSession);
        Assert.Null(_state.FileName);
        Assert.Equal("example", _state.Query.FreeText);
        Assert.Equal(SessionSortColumn.Status, _state.Query.SortColumn);
    }

    [Fact]
    public void SetSort_TogglesCurrentColumnAndResetsNewColumnAscending()
    {
        _state.SetSort(SessionSortColumn.Id);

        Assert.False(_state.Query.SortAscending);

        _state.SetSort(SessionSortColumn.Method);

        Assert.Equal(SessionSortColumn.Method, _state.Query.SortColumn);
        Assert.True(_state.Query.SortAscending);
    }

    [Fact]
    public void SelectSession_UsesLoadedSessionIdentity()
    {
        _state.ReplaceSessions("sample.har", _sessions);

        _state.SelectSession(_sessions[2] with { Method = "PATCH" });

        Assert.Same(_sessions[2], _state.SelectedSession);
    }

    [Fact]
    public void VisibleNavigation_UsesCurrentFilterAndSort()
    {
        _state.ReplaceSessions("sample.har", _sessions);
        _state.SetFilter("example.test");
        _state.SetSort(SessionSortColumn.Method);
        _state.SelectSession(_sessions[0]);

        Assert.Same(_sessions[2], _state.GetPreviousVisibleSession());
        Assert.Same(_sessions[1], _state.GetNextVisibleSession());
    }

    [Fact]
    public void ClearFilter_RestoresAllSessions()
    {
        _state.ReplaceSessions("sample.har", _sessions);
        _state.SetFilter("/two");

        Assert.Equal([2], _state.VisibleSessions.Select(session => session.Id));

        _state.ClearFilter();

        Assert.Equal(
            [1, 2, 3],
            _state.VisibleSessions.Select(session => session.Id));
    }

    private static TraceSession CreateSession(
        int id,
        string method,
        string url) =>
        new()
        {
            Id = id,
            StartedAt = DateTimeOffset.Parse("2026-09-23T10:00:00-04:00"),
            Method = method,
            Url = new Uri(url),
            StatusCode = 200 + id,
            StatusText = "Status",
            Duration = TimeSpan.FromMilliseconds(id * 100)
        };
}
