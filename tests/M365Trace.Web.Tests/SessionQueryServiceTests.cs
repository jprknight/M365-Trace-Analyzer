using M365Trace.Core;
using M365Trace.Web.Services;

namespace M365Trace.Web.Tests;

public sealed class SessionQueryServiceTests
{
    private readonly SessionQueryService _service = new();
    private readonly IReadOnlyList<TraceSession> _sessions =
    [
        CreateSession(
            1,
            "GET",
            "https://outlook.office.com/zeta",
            200,
            "OK",
            300,
            TraceSeverity.Normal,
            "Exchange Online",
            "None",
            "FrontEnd-01"),
        CreateSession(
            2,
            "POST",
            "https://login.microsoftonline.com/alpha",
            503,
            "Service Unavailable",
            100,
            TraceSeverity.Severe,
            "Authentication",
            "OAuth",
            "FrontEnd-02",
            new TraceFinding
            {
                RuleId = "M365.Test.Failure",
                Title = "Token request failed",
                Severity = TraceSeverity.Severe,
                Description = "Expired token caused the endpoint error.",
                Evidence = "Diagnostic status header was 503.",
                Recommendation = "Retry sign in after validating identity."
            }),
        CreateSession(
            3,
            "DELETE",
            "https://graph.microsoft.com/beta",
            401,
            "Unauthorized",
            200,
            TraceSeverity.Warning,
            "Microsoft Graph",
            "Bearer",
            "Graph-01")
    ];

    [Theory]
    [InlineData("LOGIN.MICROSOFTONLINE.COM")]
    [InlineData("/alpha")]
    [InlineData("POST")]
    [InlineData("503")]
    [InlineData("Service Unavailable")]
    [InlineData("Severe")]
    [InlineData("Authentication")]
    [InlineData("OAuth")]
    [InlineData("FrontEnd-02")]
    [InlineData("M365.Test.Failure")]
    [InlineData("Token request failed")]
    [InlineData("Expired token")]
    [InlineData("Retry sign in")]
    [InlineData("Diagnostic status header")]
    public void Apply_FreeTextMatchesEverySupportedField(string filter)
    {
        var result = _service.Apply(
            _sessions,
            new SessionQuery { FreeText = $"  {filter}  " });

        Assert.Equal([2], result.Select(session => session.Id));
    }

    [Theory]
    [InlineData(SessionSortColumn.Id, new[] { 1, 2, 3 }, new[] { 3, 2, 1 })]
    [InlineData(SessionSortColumn.Analysis, new[] { 1, 3, 2 }, new[] { 2, 3, 1 })]
    [InlineData(SessionSortColumn.Status, new[] { 1, 3, 2 }, new[] { 2, 3, 1 })]
    [InlineData(SessionSortColumn.Method, new[] { 3, 1, 2 }, new[] { 2, 1, 3 })]
    [InlineData(SessionSortColumn.Host, new[] { 3, 2, 1 }, new[] { 1, 2, 3 })]
    [InlineData(SessionSortColumn.Path, new[] { 2, 3, 1 }, new[] { 1, 3, 2 })]
    [InlineData(SessionSortColumn.Duration, new[] { 2, 3, 1 }, new[] { 1, 3, 2 })]
    public void Apply_SortsEveryColumnInBothDirections(
        SessionSortColumn column,
        int[] ascending,
        int[] descending)
    {
        Assert.Equal(
            ascending,
            GetIds(new SessionQuery
            {
                SortColumn = column,
                SortAscending = true
            }));
        Assert.Equal(
            descending,
            GetIds(new SessionQuery
            {
                SortColumn = column,
                SortAscending = false
            }));
    }

    [Fact]
    public void Apply_UsesSessionIdAsStableTieBreaker()
    {
        var sessions = new[]
        {
            _sessions[2] with { Id = 30, Method = "GET" },
            _sessions[0] with { Id = 10, Method = "GET" },
            _sessions[1] with { Id = 20, Method = "GET" }
        };

        var result = _service.Apply(
            sessions,
            new SessionQuery
            {
                SortColumn = SessionSortColumn.Method,
                SortAscending = false
            });

        Assert.Equal([10, 20, 30], result.Select(session => session.Id));
    }

    [Fact]
    public void Apply_StructuredCategoriesCombineWithAnd()
    {
        var result = GetIds(new SessionQuery
        {
            Severities = [TraceSeverityFilter.Severe],
            StatusFamilies = [TraceStatusFamily.ServerError],
            Methods = ["post"],
            Hosts = ["LOGIN.MICROSOFTONLINE.COM"],
            Duration = SessionDurationFilter.UnderOneSecond,
            SessionTypes = ["authentication"],
            Authentications = ["oauth"],
            FindingRuleIds = ["m365.test.failure"],
            Findings = SessionFindingFilter.HasFindings
        });

        Assert.Equal([2], result);
    }

    [Fact]
    public void Apply_ValuesWithinCategoryCombineWithOr()
    {
        var result = GetIds(new SessionQuery
        {
            Severities =
            [
                TraceSeverityFilter.Severe,
                TraceSeverityFilter.Warning
            ],
            StatusFamilies = [TraceStatusFamily.ClientError],
            StatusCodes = [503]
        });

        Assert.Equal([2, 3], result);
    }

    [Fact]
    public void Apply_FindingPresenceSupportsBothStates()
    {
        Assert.Equal(
            [2],
            GetIds(new SessionQuery
            {
                Findings = SessionFindingFilter.HasFindings
            }));
        Assert.Equal(
            [1, 3],
            GetIds(new SessionQuery
            {
                Findings = SessionFindingFilter.NoFindings
            }));
    }

    [Theory]
    [InlineData(SessionDurationFilter.UnderOneSecond, new[] { 1 })]
    [InlineData(SessionDurationFilter.OneToFiveSeconds, new[] { 2 })]
    [InlineData(SessionDurationFilter.FiveToThirtySeconds, new[] { 3 })]
    [InlineData(SessionDurationFilter.ThirtySecondsOrMore, new[] { 4 })]
    public void Apply_DurationPresetsUseStableBoundaries(
        SessionDurationFilter filter,
        int[] expectedIds)
    {
        var sessions = new[]
        {
            _sessions[0] with
            {
                Id = 1,
                Duration = TimeSpan.FromMilliseconds(999)
            },
            _sessions[0] with
            {
                Id = 2,
                Duration = TimeSpan.FromSeconds(1)
            },
            _sessions[0] with
            {
                Id = 3,
                Duration = TimeSpan.FromSeconds(5)
            },
            _sessions[0] with
            {
                Id = 4,
                Duration = TimeSpan.FromSeconds(30)
            }
        };

        var result = _service.Apply(
            sessions,
            new SessionQuery { Duration = filter });

        Assert.Equal(expectedIds, result.Select(session => session.Id));
    }

    [Fact]
    public void Apply_OtherSeverityIncludesUnclassifiedStates()
    {
        var sessions = new[]
        {
            _sessions[0] with
            {
                Id = 10,
                Analysis = TraceAnalysisResult.Empty
            },
            _sessions[0] with
            {
                Id = 20,
                Analysis = new TraceAnalysisResult
                {
                    Severity = TraceSeverity.InternalError
                }
            },
            _sessions[0]
        };

        var result = _service.Apply(
            sessions,
            new SessionQuery
            {
                Severities = [TraceSeverityFilter.Other]
            });

        Assert.Equal([10, 20], result.Select(session => session.Id));
    }

    [Fact]
    public void GetAdjacent_StaysWithinVisibleSessions()
    {
        Assert.Null(_service.GetAdjacent(_sessions, _sessions[0], -1));
        Assert.Same(
            _sessions[1],
            _service.GetAdjacent(_sessions, _sessions[0], 1));
        Assert.Same(
            _sessions[1],
            _service.GetAdjacent(_sessions, _sessions[2], -1));
        Assert.Null(_service.GetAdjacent(_sessions, _sessions[2], 1));
    }

    [Fact]
    public void GetAdjacent_ReturnsNullWhenSelectionIsNotVisible()
    {
        Assert.Null(_service.GetAdjacent(
            [_sessions[0], _sessions[2]],
            _sessions[1],
            1));
    }

    private int[] GetIds(SessionQuery query) =>
        _service.Apply(_sessions, query)
            .Select(session => session.Id)
            .ToArray();

    private static TraceSession CreateSession(
        int id,
        string method,
        string url,
        int statusCode,
        string statusText,
        double durationMilliseconds,
        TraceSeverity severity,
        string sessionType,
        string authentication,
        string responseServer,
        params TraceFinding[] findings) =>
        new()
        {
            Id = id,
            StartedAt = DateTimeOffset.Parse("2026-09-23T10:00:00-04:00"),
            Method = method,
            Url = new Uri(url),
            StatusCode = statusCode,
            StatusText = statusText,
            Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
            Analysis = new TraceAnalysisResult
            {
                Severity = severity,
                SessionType = sessionType,
                Authentication = authentication,
                ResponseServer = responseServer,
                Findings = findings
            }
        };
}
