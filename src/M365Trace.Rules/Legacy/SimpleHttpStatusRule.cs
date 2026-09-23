using M365Trace.Core;

namespace M365Trace.Rules.Legacy;

public sealed class SimpleHttpStatusRule : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public SimpleHttpStatusRule(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.HTTP.SimpleStatus";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 10;

    public bool AppliesTo(AnalysisContext context) =>
        context.SessionTypeConfidence < 10
        && _data.SimpleStatuses.ContainsKey(context.Session.StatusCode);

    public void Evaluate(AnalysisContext context)
    {
        var definition = _data.SimpleStatuses[context.Session.StatusCode].Definition;
        var sessionType = definition.SessionType
            ?? $"{context.Session.StatusCode} {context.Session.StatusText}".Trim();

        context.ApplyClassification(definition.Classification, sessionType);
        context.AddFinding(new TraceFinding
        {
            RuleId = $"M365.HTTP.{context.Session.StatusCode}.SimpleStatus",
            Title = definition.ResponseAlert ?? sessionType,
            Severity = definition.Severity,
            Description =
                "This response uses the legacy ruleset's standard status-code classification. No more specific condition was required for this status.",
            Evidence = $"The HTTP response status was {context.Session.StatusCode}."
        });
    }
}
