using M365Trace.Core;
using M365Trace.Rules.Legacy;

namespace M365Trace.Rules.Tests;

public sealed class LegacyErrorRuleTests
{
    private static readonly LegacyRulesetData Data = new();

    [Fact]
    public void Http401_Ews_IsClassifiedAsExpectedChallenge()
    {
        var result = Analyze(
            new Http401Rules(Data),
            CreateSession(401, "https://outlook.office365.com/EWS/Exchange.asmx"));

        Assert.Equal("Exchange Web Services", result.SessionType);
        Assert.Equal(TraceSeverity.Warning, result.Severity);
    }

    [Fact]
    public void Http403_AccessDenied_IsClassifiedAsProxyBlock()
    {
        var result = Analyze(
            new Http403Rules(Data),
            CreateSession(403, "https://outlook.office.com/") with
            {
                ResponseContent = Text("Access Denied by corporate proxy")
            });

        Assert.Equal("!WEB PROXY BLOCK!", result.SessionType);
        Assert.Equal(
            "M365.HTTP.403.ProxyBlock",
            Assert.Single(result.Findings).RuleId);
    }

    [Fact]
    public void Http456_OAuthUnavailable_UsesSpecificRule()
    {
        var result = Analyze(
            new Http456Rules(Data),
            CreateSession(456, "https://outlook.office365.com/") with
            {
                ResponseContent = Text("oauth_not_available")
            });

        Assert.Equal(
            "M365.HTTP.456.OAuthNotAvailable",
            Assert.Single(result.Findings).RuleId);
    }

    [Fact]
    public void Http500_EwsImpersonationDenied_UsesSpecificRule()
    {
        var result = Analyze(
            new Http500Rules(Data),
            CreateSession(
                500,
                "https://outlook.office365.com/EWS/Exchange.asmx") with
            {
                ResponseContent = Text("ErrorImpersonateUserDenied")
            });

        Assert.Equal("!EWS Impersonate User Denied!", result.SessionType);
    }

    [Fact]
    public void Http502_OnMicrosoftAutodiscover_IsFalsePositive()
    {
        var result = Analyze(
            new Http502Rules(Data),
            CreateSession(502, "https://proxy.test/") with
            {
                ResponseContent = Text(
                    "The connection to 'autodiscover.contoso.mail.onmicrosoft.com' failed because the target machine actively refused it.")
            });

        Assert.Equal(TraceSeverity.FalsePositive, result.Severity);
        Assert.Equal("False Positive", result.SessionType);
    }

    private static TraceAnalysisResult Analyze(
        ITraceRule rule,
        TraceSession session) =>
        new TraceAnalysisEngine([rule]).Analyze(session);

    private static TraceContent Text(string value) =>
        new(value, "text/plain", value.Length, false, false);

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
