using M365Trace.Core;

namespace M365Trace.Rules.Http503;

internal static class Http503RuleDefaults
{
    public static RuleClassification Classification { get; } = new(
        AuthenticationConfidence: 5,
        SessionTypeConfidence: 10,
        ResponseServerConfidence: 5,
        Severity: TraceSeverity.Severe);

    public static void Apply(
        AnalysisContext context,
        string sessionType,
        TraceFinding finding)
    {
        context.SetSessionType(sessionType, Classification.SessionTypeConfidence);
        context.SetAuthentication(null, Classification.AuthenticationConfidence);
        context.SetResponseServer(null, Classification.ResponseServerConfidence);
        context.RaiseSeverity(Classification.Severity);
        context.AddFinding(finding);
    }
}
