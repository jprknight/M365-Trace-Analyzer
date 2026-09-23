using M365Trace.Core;
using M365Trace.Rules.Http503;

namespace M365Trace.Rules.Tests;

public sealed class Http503RuleTests
{
    private static readonly TraceAnalysisEngine Engine = new ITraceRule[]
    {
        new GenericServiceUnavailableRule(),
        new OwaCreateAttachmentUnavailableRule(),
        new FederatedStsUnavailableRule()
    }.ToAnalysisEngine();

    [Fact]
    public void Analyze_FederatedStsFailure_UsesSpecializedRuleOnly()
    {
        var session = CreateSession(
            responseText: "Error: FederatedSTSUnreachable",
            requestHeaders: [new TraceHeader("X-User-Identity", "user+test@contoso.com")]);

        var result = Engine.Analyze(session);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("M365.HTTP.503.FederatedStsUnavailable", finding.RuleId);
        Assert.Equal(TraceSeverity.Severe, result.Severity);
        Assert.Equal("!FederatedSTSUnreachable!", result.SessionType);
        Assert.Equal(10, result.SessionTypeConfidence);
        Assert.Equal(5, result.AuthenticationConfidence);
        Assert.Equal(5, result.ResponseServerConfidence);
        Assert.Contains("user%2Btest%40contoso.com", Assert.Single(finding.Links).Url.AbsoluteUri);
    }

    [Fact]
    public void Analyze_OwaCreateAttachment_UsesOwaRuleOnly()
    {
        var session = CreateSession(
            url: "https://outlook.office.com/owa/service.svc/CreateAttachment?id=1");

        var result = Engine.Analyze(session);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("M365.HTTP.503.OwaCreateAttachment", finding.RuleId);
        Assert.Equal("503 OWA unable to CreateAttachment!", result.SessionType);
    }

    [Fact]
    public void Analyze_Other503_UsesGenericFallback()
    {
        var result = Engine.Analyze(CreateSession());

        var finding = Assert.Single(result.Findings);
        Assert.Equal("M365.HTTP.503.ServiceUnavailable", finding.RuleId);
        Assert.Equal("Service Unavailable", result.SessionType);
    }

    [Fact]
    public void Analyze_NearMatchFederatedText_DoesNotTriggerSpecializedRule()
    {
        var session = CreateSession(responseText: "NotFederatedSTSUnreachableSuffix");

        var result = Engine.Analyze(session);

        Assert.Equal(
            "M365.HTTP.503.ServiceUnavailable",
            Assert.Single(result.Findings).RuleId);
    }

    [Fact]
    public void Analyze_Non503_RemainsUnclassified()
    {
        var result = Engine.Analyze(CreateSession(statusCode: 200));

        Assert.Empty(result.Findings);
        Assert.Null(result.Severity);
        Assert.Null(result.SessionType);
    }

    private static TraceSession CreateSession(
        int statusCode = 503,
        string url = "https://outlook.office.com/owa/",
        string? responseText = null,
        IReadOnlyList<TraceHeader>? requestHeaders = null) => new()
    {
        Id = 1,
        StartedAt = DateTimeOffset.Parse("2026-09-23T10:00:00-04:00"),
        Method = "GET",
        Url = new Uri(url),
        StatusCode = statusCode,
        StatusText = statusCode == 503 ? "Service Unavailable" : "OK",
        Duration = TimeSpan.FromMilliseconds(100),
        RequestHeaders = requestHeaders ?? [],
        ResponseContent = responseText is null
            ? null
            : new TraceContent(responseText, "text/plain", responseText.Length, false, false)
    };
}

internal static class RuleTestExtensions
{
    public static TraceAnalysisEngine ToAnalysisEngine(this IEnumerable<ITraceRule> rules) =>
        new(rules);
}
