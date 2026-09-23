using M365Trace.Core;
using M365Trace.Rules.Legacy;

namespace M365Trace.Rules.Tests;

public sealed class CrossCuttingRuleTests
{
    private static readonly LegacyRulesetData Data = new();

    [Fact]
    public void ApacheAutodiscover_IsSevere()
    {
        var result = Analyze(
            CreateSession("https://example.test/autodiscover/autodiscover.xml") with
            {
                ResponseHeaders = [new TraceHeader("Server", "Apache/2.4")]
            },
            new ApacheAutodiscoverRule(Data));

        Assert.Equal("APACHE AUTODISCOVER", result.SessionType);
        Assert.Equal(TraceSeverity.Severe, result.Severity);
    }

    [Fact]
    public void BearerHeader_IsModernAuthToken()
    {
        var result = Analyze(
            CreateSession("https://graph.microsoft.com/v1.0/me") with
            {
                RequestHeaders =
                [
                    new TraceHeader("Authorization", "Bearer redacted")
                ]
            },
            new AuthenticationRule(Data));

        Assert.Equal("Modern Auth Token", result.Authentication);
    }

    [Fact]
    public void GeneralMicrosoft365_UsesEitherKnownHost()
    {
        var result = Analyze(
            CreateSession("https://outlook.office.com/mail"),
            new SessionTypeRule(Data));

        Assert.Equal("General Microsoft365", result.SessionType);
    }

    [Fact]
    public void ResponseServer_UsesHeaderPriority()
    {
        var result = Analyze(
            CreateSession("https://example.test/") with
            {
                ResponseHeaders =
                [
                    new TraceHeader("X-Powered-By", "ASP.NET"),
                    new TraceHeader("Server", "Microsoft-IIS/10.0")
                ]
            },
            new ResponseServerRule(Data));

        Assert.Equal("Microsoft-IIS/10.0", result.ResponseServer);
    }

    [Theory]
    [InlineData(3000, TraceSeverity.Warning)]
    [InlineData(6000, TraceSeverity.Severe)]
    public void LongRunningSession_UsesCorrectedThresholds(
        int durationMilliseconds,
        TraceSeverity expectedSeverity)
    {
        var result = Analyze(
            CreateSession("https://example.test/") with
            {
                Duration = TimeSpan.FromMilliseconds(durationMilliseconds)
            },
            new LongRunningSessionRule(Data));

        Assert.Equal(expectedSeverity, result.Severity);
    }

    private static TraceAnalysisResult Analyze(
        TraceSession session,
        params ITraceRule[] rules) =>
        new TraceAnalysisEngine(rules).Analyze(session);

    private static TraceSession CreateSession(string url) => new()
    {
        Id = 1,
        StartedAt = DateTimeOffset.Parse("2026-09-23T10:00:00-04:00"),
        Method = "GET",
        Url = new Uri(url),
        StatusCode = 200,
        StatusText = "OK",
        Duration = TimeSpan.FromMilliseconds(10)
    };
}
