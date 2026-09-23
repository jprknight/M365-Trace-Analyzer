using M365Trace.Core;
using M365Trace.Rules.Legacy;
using M365Trace.Rules.Legacy.Http200;

namespace M365Trace.Rules.Tests;

public sealed class Http200RuleTests
{
    private static readonly LegacyRulesetData Data = new();

    [Fact]
    public void ClientAccessRule_TakesPrecedenceOverMapiSuccess()
    {
        var result = Analyze(
            CreateSession(
                "https://outlook.office365.com/mapi/emsmdb/?MailboxId=1") with
            {
                ResponseContent = Text(
                    "Connection blocked by Client Access Rules")
            });

        Assert.Equal("!CLIENT ACCESS RULE!", result.SessionType);
        Assert.Equal(TraceSeverity.Severe, result.Severity);
    }

    [Fact]
    public void Attachment_IsClassifiedBeforeGenericOwa()
    {
        var result = Analyze(
            CreateSession(
                "https://attachments.office.net/owa/user/GetAttachmentThumbnail"));

        Assert.Equal(
            "M365.HTTP.200.OwaAttachmentThumbnail",
            Assert.Single(result.Findings).RuleId);
    }

    [Fact]
    public void ClickToRunAutodiscover_MissingXml_IsSevere()
    {
        var result = Analyze(
            CreateSession(
                "https://outlook.office365.com/Autodiscover/AutoDiscover.xml") with
            {
                ResponseContent = Text("<html>unexpected</html>")
            });

        Assert.Equal(TraceSeverity.Severe, result.Severity);
        Assert.Equal(
            "M365.HTTP.200.AutodiscoverClickToRunUnexpected",
            Assert.Single(result.Findings).RuleId);
    }

    [Fact]
    public void InvalidJson_IsConcerning()
    {
        var result = Analyze(
            CreateSession("https://example.test/api") with
            {
                ResponseHeaders =
                [
                    new TraceHeader("Content-Type", "application/json")
                ],
                ResponseContent = Text("{not-json}")
            });

        Assert.Equal(TraceSeverity.Concerning, result.Severity);
        Assert.Equal("!200 OK with INVALID JSON!", result.SessionType);
    }

    [Fact]
    public void UnrecognizedSuccess_UsesActuallyOkFallback()
    {
        var result = Analyze(
            CreateSession("https://example.test/") with
            {
                ResponseContent = Text("Everything completed successfully.")
            });

        Assert.Equal("200 Actually OK", result.SessionType);
        Assert.Equal(TraceSeverity.Normal, result.Severity);
    }

    private static TraceAnalysisResult Analyze(TraceSession session) =>
        new TraceAnalysisEngine(CreateHttp200Rules()).Analyze(session);

    private static IReadOnlyList<ITraceRule> CreateHttp200Rules() =>
        typeof(Http200RuleBase).Assembly
            .GetTypes()
            .Where(type =>
                !type.IsAbstract
                && typeof(Http200RuleBase).IsAssignableFrom(type))
            .Select(type =>
                (ITraceRule)(Activator.CreateInstance(type, Data)
                    ?? throw new InvalidOperationException(
                        $"Could not create HTTP 200 rule '{type.FullName}'.")))
            .ToArray();

    private static TraceContent Text(string value) =>
        new(value, "text/plain", value.Length, false, false);

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
