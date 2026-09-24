using M365Trace.Core;
using M365Trace.Web.Services;

namespace M365Trace.Web.Tests;

public sealed class TraceSummaryServiceTests
{
    private readonly TraceSummaryService _service = new();

    [Fact]
    public void Create_EmptyTraceReturnsZeroSummary()
    {
        var summary = _service.Create([]);

        Assert.Equal(0, summary.TotalSessions);
        Assert.Null(summary.TraceStart);
        Assert.Null(summary.TraceEnd);
        Assert.Equal(TimeSpan.Zero, summary.TraceWindow);
        Assert.Empty(summary.FailingHosts);
        Assert.Empty(summary.HighImpactFindings);
        Assert.Empty(summary.SlowestSessions);
        Assert.Empty(summary.AuthenticationTypes);
    }

    [Fact]
    public void Create_CalculatesTraceWindowAndDistributions()
    {
        var start = DateTimeOffset.Parse("2026-09-24T10:00:00-04:00");
        var sessions = new[]
        {
            CreateSession(
                1,
                start,
                200,
                100,
                TraceSeverity.Normal),
            CreateSession(
                2,
                start.AddSeconds(2),
                302,
                200,
                TraceSeverity.Warning),
            CreateSession(
                3,
                start.AddSeconds(4),
                401,
                300,
                TraceSeverity.Concerning),
            CreateSession(
                4,
                start.AddSeconds(6),
                503,
                400,
                TraceSeverity.Severe),
            CreateSession(
                5,
                start.AddSeconds(8),
                0,
                500,
                TraceSeverity.InternalError),
            CreateSession(
                6,
                start.AddSeconds(10),
                101,
                600,
                null)
        };

        var summary = _service.Create(sessions);

        Assert.Equal(6, summary.TotalSessions);
        Assert.Equal(start, summary.TraceStart);
        Assert.Equal(
            start + TimeSpan.FromMilliseconds(10600),
            summary.TraceEnd);
        Assert.Equal(TimeSpan.FromMilliseconds(10600), summary.TraceWindow);
        Assert.Equal(
            new TraceSeveritySummary(1, 1, 1, 1, 2),
            summary.Severities);
        Assert.Equal(
            new TraceStatusSummary(1, 1, 1, 1, 1, 1),
            summary.Statuses);
    }

    [Fact]
    public void Create_RanksFindingsFailuresSlowSessionsAndAuthentication()
    {
        var start = DateTimeOffset.Parse("2026-09-24T10:00:00-04:00");
        var sessions = new[]
        {
            CreateSession(
                1,
                start,
                503,
                6000,
                TraceSeverity.Severe,
                host: "api.example.test",
                authentication: "OAuth",
                findings:
                [
                    CreateFinding(
                        "M365.Performance.Duration",
                        TraceSeverity.Severe),
                    CreateFinding("M365.Test.Repeated", TraceSeverity.Severe)
                ]),
            CreateSession(
                2,
                start,
                401,
                4000,
                TraceSeverity.Concerning,
                host: "api.example.test",
                authentication: "oauth",
                findings:
                [
                    CreateFinding(
                        "M365.Performance.Duration",
                        TraceSeverity.Warning),
                    CreateFinding(
                        "M365.Test.Repeated",
                        TraceSeverity.Concerning)
                ]),
            CreateSession(
                3,
                start,
                200,
                2000,
                TraceSeverity.Warning,
                host: "mail.example.test"),
            CreateSession(
                4,
                start,
                0,
                1000,
                TraceSeverity.Warning,
                host: "offline.example.test",
                authentication: "Bearer")
        };

        var summary = _service.Create(sessions);

        Assert.Equal(2, summary.SessionsWithFindings);
        Assert.Equal(2, summary.SlowSessions);
        Assert.Equal(
            [
                new TraceNamedCount("api.example.test", 2),
                new TraceNamedCount("offline.example.test", 1)
            ],
            summary.FailingHosts);
        Assert.Equal(
            [
                new TraceFindingCount("M365.Test.Repeated", 2),
                new TraceFindingCount("M365.Performance.Duration", 1)
            ],
            summary.HighImpactFindings);
        Assert.Equal(
            [1, 2, 3, 4],
            summary.SlowestSessions.Select(session => session.SessionId));
        Assert.Equal(
            [
                new TraceNamedCount("OAuth", 2),
                new TraceNamedCount("Bearer", 1),
                new TraceNamedCount("Not classified", 1)
            ],
            summary.AuthenticationTypes);
    }

    [Fact]
    public void Create_LimitsRankedListsToFiveDeterministically()
    {
        var start = DateTimeOffset.Parse("2026-09-24T10:00:00-04:00");
        var sessions = Enumerable.Range(1, 7)
            .Select(id => CreateSession(
                id,
                start,
                500,
                id * 100,
                TraceSeverity.Severe,
                host: $"host-{id}.example.test",
                findings:
                [
                    CreateFinding(
                        $"M365.Test.Rule{id}",
                        TraceSeverity.Severe)
                ]))
            .ToArray();

        var summary = _service.Create(sessions);

        Assert.Equal(5, summary.FailingHosts.Count);
        Assert.Equal(
            [
                "host-1.example.test",
                "host-2.example.test",
                "host-3.example.test",
                "host-4.example.test",
                "host-5.example.test"
            ],
            summary.FailingHosts.Select(item => item.Name));
        Assert.Equal(5, summary.HighImpactFindings.Count);
        Assert.Equal(
            [7, 6, 5, 4, 3],
            summary.SlowestSessions.Select(session => session.SessionId));
    }

    private static TraceSession CreateSession(
        int id,
        DateTimeOffset startedAt,
        int statusCode,
        double durationMilliseconds,
        TraceSeverity? severity,
        string host = "example.test",
        string? authentication = null,
        IReadOnlyList<TraceFinding>? findings = null) =>
        new()
        {
            Id = id,
            StartedAt = startedAt,
            Method = "GET",
            Url = new Uri($"https://{host}/session/{id}"),
            StatusCode = statusCode,
            StatusText = "Status",
            Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
            Analysis = new TraceAnalysisResult
            {
                Severity = severity,
                Authentication = authentication,
                Findings = findings ?? []
            }
        };

    private static TraceFinding CreateFinding(
        string ruleId,
        TraceSeverity severity) =>
        new()
        {
            RuleId = ruleId,
            Title = ruleId,
            Severity = severity
        };
}
