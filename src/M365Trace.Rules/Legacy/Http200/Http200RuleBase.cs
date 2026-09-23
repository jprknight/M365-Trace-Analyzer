using M365Trace.Core;

namespace M365Trace.Rules.Legacy.Http200;

public abstract class Http200RuleBase : ITraceRule
{
    protected Http200RuleBase(LegacyRulesetData data)
    {
        Data = data;
    }

    protected LegacyRulesetData Data { get; }

    public abstract string Id { get; }

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public abstract int Order { get; }

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 200
        && !context.HasFindingWithRuleIdPrefix("M365.HTTP.200.")
        && Matches(context);

    public abstract void Evaluate(AnalysisContext context);

    protected abstract bool Matches(AnalysisContext context);

    protected void ApplyByPrefix(
        AnalysisContext context,
        string classificationName,
        string stringPrefix,
        string ruleId,
        string evidence,
        string? sessionTypeKey = null)
    {
        Apply(
            context,
            classificationName,
            ruleId,
            Data.GetString(sessionTypeKey ?? $"{stringPrefix}_SessionType"),
            Data.GetPlainText($"{stringPrefix}_ResponseAlert"),
            Data.GetPlainText($"{stringPrefix}_ResponseComments"),
            evidence);
    }

    protected void Apply(
        AnalysisContext context,
        string classificationName,
        string ruleId,
        string sessionType,
        string title,
        string description,
        string evidence) =>
        LegacyRuleApplication.Apply(
            context,
            Data.GetClassification("HTTP_200s", classificationName),
            ruleId,
            sessionType,
            title,
            description,
            evidence);

    protected static bool IsExchangeOnlineMapi(SessionFacts facts) =>
        string.Equals(
            facts.Host,
            "outlook.office365.com",
            StringComparison.OrdinalIgnoreCase)
        && facts.UrlContains("/mapi/emsmdb/?MailboxId=");
}
