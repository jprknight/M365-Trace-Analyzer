using Bunit;
using M365Trace.Web.Components;
using M365Trace.Web.Services;

namespace M365Trace.Web.Tests;

public sealed class TraceSummaryPanelTests : IDisposable
{
    private readonly BunitContext _context = new();

    [Fact]
    public void Panel_RendersOverviewDistributionsAndRankedLists()
    {
        var summary = new TraceSummary
        {
            TotalSessions = 12,
            TraceStart = DateTimeOffset.Parse(
                "2026-09-24T10:00:00-04:00"),
            TraceEnd = DateTimeOffset.Parse(
                "2026-09-24T10:02:00-04:00"),
            TraceWindow = TimeSpan.FromMinutes(2),
            SessionsWithFindings = 4,
            SlowSessions = 2,
            Severities = new TraceSeveritySummary(1, 2, 3, 5, 1),
            Statuses = new TraceStatusSummary(1, 6, 1, 2, 1, 1),
            FailingHosts =
            [
                new TraceNamedCount("api.example.test", 3)
            ],
            HighImpactFindings =
            [
                new TraceFindingCount("M365.Test.Rule", 2)
            ],
            SlowestSessions =
            [
                new TraceSessionSummary(
                    8,
                    "POST",
                    "api.example.test",
                    "/slow",
                    TimeSpan.FromSeconds(8))
            ],
            AuthenticationTypes =
            [
                new TraceNamedCount("OAuth", 7)
            ]
        };

        var component = _context.Render<TraceSummaryPanel>(parameters =>
            parameters
                .Add(parameter => parameter.Summary, summary)
                .Add(
                    parameter => parameter.CurrentTime,
                    DateTimeOffset.Parse("2026-09-25T10:02:00-04:00"))
                .Add(parameter => parameter.VisibleSessionCount, 9));

        Assert.False(component.Find("details").HasAttribute("open"));
        Assert.Contains("9 visible of 12", component.Markup);
        Assert.Contains("2 mins, 0 secs", component.Markup);
        Assert.Contains("1 day", component.Markup);
        Assert.Contains("Since latest session", component.Markup);
        Assert.Contains("api.example.test", component.Markup);
        Assert.Contains("M365.Test.Rule", component.Markup);
        Assert.Contains("#8", component.Markup);
        Assert.Contains("OAuth", component.Markup);
    }

    [Fact]
    public void Panel_CalculatesTraceAgeFromLatestSession()
    {
        var summary = new TraceSummary
        {
            TotalSessions = 2,
            TraceStart = DateTimeOffset.Parse(
                "2010-01-01T00:00:00+00:00"),
            TraceEnd = DateTimeOffset.Parse(
                "2019-03-28T18:30:06+00:00"),
            SessionsWithFindings = 0,
            SlowSessions = 0,
            Severities = new TraceSeveritySummary(0, 0, 0, 2, 0),
            Statuses = new TraceStatusSummary(0, 2, 0, 0, 0, 0)
        };

        var component = _context.Render<TraceSummaryPanel>(parameters =>
            parameters
                .Add(parameter => parameter.Summary, summary)
                .Add(
                    parameter => parameter.CurrentTime,
                    DateTimeOffset.Parse("2026-09-25T13:59:05+00:00")));

        Assert.Contains("7 years, 5 months", component.Markup);
    }

    [Fact]
    public void Panel_ExplainsUnavailableAndFutureTraceAges()
    {
        var summary = new TraceSummary
        {
            TotalSessions = 0,
            SessionsWithFindings = 0,
            SlowSessions = 0,
            Severities = new TraceSeveritySummary(0, 0, 0, 0, 0),
            Statuses = new TraceStatusSummary(0, 0, 0, 0, 0, 0)
        };
        var component = _context.Render<TraceSummaryPanel>(parameters =>
            parameters
                .Add(parameter => parameter.Summary, summary)
                .Add(
                    parameter => parameter.CurrentTime,
                    DateTimeOffset.Parse("2026-09-25T13:59:05+00:00")));

        Assert.Contains("Unknown", component.Markup);

        var futureComponent = _context.Render<TraceSummaryPanel>(parameters =>
            parameters
                .Add(
                    parameter => parameter.Summary,
                    summary with
                    {
                        TraceEnd = DateTimeOffset.Parse(
                            "2026-09-26T13:59:05+00:00")
                    })
                .Add(
                    parameter => parameter.CurrentTime,
                    DateTimeOffset.Parse("2026-09-25T13:59:05+00:00")));

        Assert.Contains("Future-dated", futureComponent.Markup);
    }

    [Theory]
    [InlineData(0, "0 mins, 0 secs")]
    [InlineData(1, "0 mins, 1 sec")]
    [InlineData(60, "1 min, 0 secs")]
    [InlineData(61, "1 min, 1 sec")]
    [InlineData(119, "1 min, 59 secs")]
    public void Panel_FormatsTraceWindowWithWholeMinutesAndSeconds(
        int totalSeconds,
        string expected)
    {
        var summary = new TraceSummary
        {
            TotalSessions = 0,
            TraceWindow = TimeSpan.FromSeconds(totalSeconds),
            SessionsWithFindings = 0,
            SlowSessions = 0,
            Severities = new TraceSeveritySummary(0, 0, 0, 0, 0),
            Statuses = new TraceStatusSummary(0, 0, 0, 0, 0, 0)
        };

        var component = _context.Render<TraceSummaryPanel>(parameters =>
            parameters.Add(parameter => parameter.Summary, summary));

        Assert.Contains(expected, component.Markup);
    }

    [Fact]
    public void Panel_ExplainsEmptyRankedSections()
    {
        var summary = new TraceSummary
        {
            TotalSessions = 0,
            SessionsWithFindings = 0,
            SlowSessions = 0,
            Severities = new TraceSeveritySummary(0, 0, 0, 0, 0),
            Statuses = new TraceStatusSummary(0, 0, 0, 0, 0, 0)
        };

        var component = _context.Render<TraceSummaryPanel>(parameters =>
            parameters.Add(parameter => parameter.Summary, summary));

        Assert.Contains("No timing data", component.Markup);
        Assert.Contains("No failing hosts observed.", component.Markup);
        Assert.Contains(
            "No concerning or severe findings.",
            component.Markup);
        Assert.Contains("No sessions available.", component.Markup);
        Assert.Contains(
            "No authentication classifications.",
            component.Markup);
    }

    [Fact]
    public void Panel_InvokesDrillThroughCallbacks()
    {
        var clearRequested = false;
        TraceSeverityFilter? severity = null;
        TraceStatusFamily? status = null;
        SessionFindingFilter? findings = null;
        string? host = null;
        string? ruleId = null;
        string? authentication = null;
        int? sessionId = null;
        var summary = new TraceSummary
        {
            TotalSessions = 3,
            SessionsWithFindings = 2,
            SlowSessions = 1,
            Severities = new TraceSeveritySummary(1, 0, 0, 2, 0),
            Statuses = new TraceStatusSummary(0, 2, 0, 0, 1, 0),
            FailingHosts =
            [
                new TraceNamedCount("api.example.test", 1)
            ],
            HighImpactFindings =
            [
                new TraceFindingCount("M365.Test.Rule", 1)
            ],
            SlowestSessions =
            [
                new TraceSessionSummary(
                    7,
                    "GET",
                    "api.example.test",
                    "/slow",
                    TimeSpan.FromSeconds(7))
            ],
            AuthenticationTypes =
            [
                new TraceNamedCount("OAuth", 2)
            ]
        };

        var component = _context.Render<TraceSummaryPanel>(parameters =>
            parameters
                .Add(parameter => parameter.Summary, summary)
                .Add(
                    parameter => parameter.ClearAllRequested,
                    () => clearRequested = true)
                .Add(
                    parameter => parameter.SeveritySelected,
                    value => severity = value)
                .Add(
                    parameter => parameter.StatusSelected,
                    value => status = value)
                .Add(
                    parameter => parameter.FindingsSelected,
                    value => findings = value)
                .Add(
                    parameter => parameter.HostSelected,
                    value => host = value)
                .Add(
                    parameter => parameter.FindingSelected,
                    value => ruleId = value)
                .Add(
                    parameter => parameter.AuthenticationSelected,
                    value => authentication = value)
                .Add(
                    parameter => parameter.SessionSelected,
                    value => sessionId = value));

        component
            .Find("button[title='Clear filters and show all sessions']")
            .Click();
        FindButton(component, "Severe").Click();
        FindButton(component, "5xx").Click();
        FindButton(component, "Sessions with findings").Click();
        FindButton(component, "api.example.test").Click();
        FindButton(component, "M365.Test.Rule").Click();
        FindButton(component, "OAuth").Click();
        FindButton(component, "#7").Click();

        Assert.True(clearRequested);
        Assert.Equal(TraceSeverityFilter.Severe, severity);
        Assert.Equal(TraceStatusFamily.ServerError, status);
        Assert.Equal(SessionFindingFilter.HasFindings, findings);
        Assert.Equal("api.example.test", host);
        Assert.Equal("M365.Test.Rule", ruleId);
        Assert.Equal("OAuth", authentication);
        Assert.Equal(7, sessionId);
    }

    private static AngleSharp.Dom.IElement FindButton(
        IRenderedComponent<TraceSummaryPanel> component,
        string text) =>
        component.FindAll("button")
            .Single(button =>
                NormalizeWhitespace(button.TextContent).StartsWith(
                    text,
                    StringComparison.Ordinal));

    private static string NormalizeWhitespace(string value) =>
        string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

    public void Dispose() => _context.Dispose();
}
