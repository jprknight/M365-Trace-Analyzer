using M365Trace.Core;
using M365Trace.Rules.Legacy;

namespace M365Trace.Rules.Tests;

public sealed class LegacyResponseRuleTests
{
    private static readonly LegacyRulesetData Data = new();

    [Fact]
    public void Http302_Autodiscover_UsesSpecificClassification()
    {
        var result = Analyze(
            new Http302Rules(Data),
            CreateSession(302, "https://autodiscover.contoso.com/autodiscover/autodiscover.xml"));

        Assert.Equal("Autodiscover Redirect", result.SessionType);
        Assert.Equal(
            "M365.HTTP.302.AutodiscoverRedirect",
            Assert.Single(result.Findings).RuleId);
    }

    [Fact]
    public void Http307_UnexpectedAutodiscoverLocation_IsSevere()
    {
        var session = CreateSession(
            307,
            "https://autodiscover.contoso.mail.onmicrosoft.com/autodiscover/autodiscover.xml")
            with
        {
            ResponseHeaders =
                [
                    new TraceHeader("Location", "https://mail.contoso.com/autodiscover/autodiscover.xml")
                ]
        };

        var result = Analyze(new Http307Rules(Data), session);

        Assert.Equal(TraceSeverity.Severe, result.Severity);
        Assert.Equal("!UNEXPECTED LOCATION!", result.ResponseServer);
    }

    [Fact]
    public void Http400_LoginHost_UsesCloudAuthenticationClassification()
    {
        var result = Analyze(
            new Http400Rules(Data),
            CreateSession(400, "https://login.microsoftonline.com/common/oauth2/authorize"));

        Assert.Equal("!CLOUD AUTHENTICATION! ", result.SessionType);
        Assert.Equal(TraceSeverity.Severe, result.Severity);
    }

    [Fact]
    public void Http404_AppliesLegacyWarningClassification()
    {
        var result = Analyze(
            new Http404Rule(Data),
            CreateSession(404, "https://example.test/missing"));

        Assert.Equal("HTTP 404 Not Found", result.SessionType);
        Assert.Equal(TraceSeverity.Warning, result.Severity);
    }

    [Fact]
    public void Http504_InternetBlocked_UsesSpecificClassification()
    {
        var session = CreateSession(504, "https://example.test/") with
        {
            ResponseContent = new TraceContent(
                "Internet access has been blocked.",
                "text/plain",
                33,
                false,
                false)
        };

        var result = Analyze(new Http504Rules(Data), session);

        Assert.Equal("!INTERNET BLOCKED!", result.SessionType);
        Assert.Equal(
            "M365.HTTP.504.InternetAccessBlocked",
            Assert.Single(result.Findings).RuleId);
    }

    private static TraceAnalysisResult Analyze(
        ITraceRule rule,
        TraceSession session) =>
        new TraceAnalysisEngine([rule]).Analyze(session);

    private static TraceSession CreateSession(int statusCode, string url) => new()
    {
        Id = 1,
        StartedAt = DateTimeOffset.Parse("2026-09-23T10:00:00-04:00"),
        Method = "GET",
        Url = new Uri(url),
        StatusCode = statusCode,
        StatusText = "Status",
        Duration = TimeSpan.FromMilliseconds(10)
    };
}
