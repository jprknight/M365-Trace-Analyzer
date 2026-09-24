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
                .Add(parameter => parameter.VisibleSessionCount, 9));

        Assert.False(component.Find("details").HasAttribute("open"));
        Assert.Contains("9 visible of 12", component.Markup);
        Assert.Contains("2.0 min", component.Markup);
        Assert.Contains("api.example.test", component.Markup);
        Assert.Contains("M365.Test.Rule", component.Markup);
        Assert.Contains("#8", component.Markup);
        Assert.Contains("OAuth", component.Markup);
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

    public void Dispose() => _context.Dispose();
}
