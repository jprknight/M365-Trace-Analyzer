using M365Trace.Core;
using M365Trace.Rules.Legacy;

namespace M365Trace.Rules.Tests;

public sealed class LegacyRulesetDataTests
{
    [Fact]
    public void Constructor_LoadsLegacyStatusAndStringAssets()
    {
        var data = new LegacyRulesetData();

        Assert.True(data.SimpleStatuses.Count > 50);
        Assert.Equal("201 Created", data.SimpleStatuses[201].Definition.SessionType);
        Assert.Contains("No known issue", data.GetPlainText("Response Comments No Known Issue"));
    }

    [Fact]
    public void SimpleStatusRule_AppliesLegacyClassification()
    {
        var data = new LegacyRulesetData();
        var engine = new TraceAnalysisEngine(
        [
            new SimpleHttpStatusRule(data),
            new UnknownHttpStatusRule(data)
        ]);

        var result = engine.Analyze(CreateSession(201, "Created"));

        var finding = Assert.Single(result.Findings);
        Assert.Equal("M365.HTTP.201.SimpleStatus", finding.RuleId);
        Assert.Equal("201 Created", result.SessionType);
        Assert.Equal(TraceSeverity.Uninteresting, result.Severity);
        Assert.Equal(10, result.SessionTypeConfidence);
    }

    [Fact]
    public void UnknownStatusRule_OnlyAppliesToUncataloguedStatus()
    {
        var data = new LegacyRulesetData();
        var engine = new TraceAnalysisEngine(
        [
            new SimpleHttpStatusRule(data),
            new UnknownHttpStatusRule(data)
        ]);

        var result = engine.Analyze(CreateSession(599, "Custom Status"));

        Assert.Equal(
            "M365.HTTP.UnknownStatus",
            Assert.Single(result.Findings).RuleId);
        Assert.Null(result.SessionType);
    }

    [Fact]
    public void SpecializedStatus_IsNotConsumedBySimpleOrUnknownRules()
    {
        var data = new LegacyRulesetData();
        var engine = new TraceAnalysisEngine(
        [
            new SimpleHttpStatusRule(data),
            new UnknownHttpStatusRule(data)
        ]);

        var result = engine.Analyze(CreateSession(503, "Service Unavailable"));

        Assert.Empty(result.Findings);
    }

    private static TraceSession CreateSession(int statusCode, string statusText) => new()
    {
        Id = 1,
        StartedAt = DateTimeOffset.Parse("2026-09-23T10:00:00-04:00"),
        Method = "GET",
        Url = new Uri("https://example.test/"),
        StatusCode = statusCode,
        StatusText = statusText,
        Duration = TimeSpan.FromMilliseconds(10)
    };
}
