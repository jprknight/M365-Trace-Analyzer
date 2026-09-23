using M365Trace.Core;

namespace M365Trace.Rules.Legacy;

internal static class LegacyRuleApplication
{
    public static void Apply(
        AnalysisContext context,
        LegacyClassificationDefinition definition,
        string ruleId,
        string sessionType,
        string title,
        string description,
        string evidence,
        string? recommendation = null,
        string? responseServer = null,
        string? authentication = null)
    {
        context.ApplyClassification(definition.Classification, sessionType);

        if (responseServer is not null)
        {
            context.SetResponseServer(
                responseServer,
                definition.ResponseServerConfidence);
        }

        if (authentication is not null)
        {
            context.SetAuthentication(
                authentication,
                definition.AuthenticationConfidence);
        }

        context.AddFinding(new TraceFinding
        {
            RuleId = ruleId,
            Title = title,
            Severity = definition.Severity,
            Description = description,
            Recommendation = recommendation,
            Evidence = evidence
        });
    }
}
