using M365Trace.Core;
using M365Trace.Web.Services;

namespace M365Trace.Web.Tests;

public sealed class SessionCopyFormatterTests
{
    private readonly SessionCopyFormatter _formatter = new();

    [Fact]
    public void FormatHeaders_PreservesRecordedOrderAndDuplicates()
    {
        var result = _formatter.FormatHeaders(
        [
            new TraceHeader("Set-Cookie", "first=value"),
            new TraceHeader("Set-Cookie", "second=value")
        ]);

        Assert.Equal(
            "Set-Cookie: first=value\nSet-Cookie: second=value",
            result.Text);
        Assert.False(result.IncludesTruncationNotice);
    }

    [Fact]
    public void FormatBody_AppendsExplicitTruncationNotice()
    {
        var result = _formatter.FormatBody(new TraceContent(
            "partial",
            "text/plain",
            100,
            false,
            true));

        Assert.NotNull(result);
        Assert.Equal(
            "partial\n\n[Content truncated during import.]",
            result.Text);
        Assert.True(result.IncludesTruncationNotice);
    }

    [Fact]
    public void FormatFindings_IncludesActionableEvidence()
    {
        var session = new TraceSession
        {
            Id = 1,
            StartedAt = DateTimeOffset.UnixEpoch,
            Method = "POST",
            Url = new Uri("https://example.test/token"),
            StatusCode = 401,
            Duration = TimeSpan.Zero,
            Analysis = new TraceAnalysisResult
            {
                Findings =
                [
                    new TraceFinding
                    {
                        RuleId = "M365.Test.Rule",
                        Title = "Token failure",
                        Severity = TraceSeverity.Severe,
                        Description = "The token was rejected.",
                        Evidence = "Status 401.",
                        Recommendation = "Acquire a new token."
                    }
                ]
            }
        };

        var result = _formatter.FormatFindings(session);

        Assert.NotNull(result);
        Assert.Contains("POST https://example.test/token", result.Text);
        Assert.Contains("[Severe] Token failure", result.Text);
        Assert.Contains("Rule: M365.Test.Rule", result.Text);
        Assert.Contains("Evidence: Status 401.", result.Text);
        Assert.Contains(
            "Recommended investigation: Acquire a new token.",
            result.Text);
    }
}
