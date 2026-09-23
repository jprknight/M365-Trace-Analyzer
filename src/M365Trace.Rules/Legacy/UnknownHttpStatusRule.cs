using M365Trace.Core;

namespace M365Trace.Rules.Legacy;

public sealed class UnknownHttpStatusRule : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public UnknownHttpStatusRule(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.HTTP.UnknownStatus";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 20_000;

    public bool AppliesTo(AnalysisContext context) =>
        !_data.IsKnownStatusCode(context.Session.StatusCode);

    public void Evaluate(AnalysisContext context)
    {
        context.RaiseSeverity(TraceSeverity.Uninteresting);
        context.AddFinding(new TraceFinding
        {
            RuleId = Id,
            Title = $"Undefined HTTP status {context.Session.StatusCode}",
            Severity = TraceSeverity.Uninteresting,
            Description =
                "The legacy Office 365 ruleset does not contain a classification for this response code.",
            Evidence = $"The HTTP response status was {context.Session.StatusCode}."
        });
    }
}
