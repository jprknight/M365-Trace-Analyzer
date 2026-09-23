namespace M365Trace.Rules.Legacy;

public sealed class Http504Rules : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public Http504Rules(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.HTTP.504";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 504;

    public void Evaluate(AnalysisContext context)
    {
        var isInternetBlocked =
            context.Session.ResponseContains("internet")
            && context.Session.ResponseContains("access")
            && context.Session.ResponseContains("blocked");
        var classificationName = isInternetBlocked
            ? "HTTP_504_Gateway_Timeout_Internet_Access_Blocked"
            : "HTTP_504_Gateway_Timeout_Anything_Else";

        LegacyRuleApplication.Apply(
            context,
            _data.GetClassification("HTTP_504s", classificationName),
            isInternetBlocked
                ? "M365.HTTP.504.InternetAccessBlocked"
                : "M365.HTTP.504.GatewayTimeout",
            _data.GetString($"{classificationName}_SessionType"),
            _data.GetPlainText($"{classificationName}_ResponseAlert"),
            _data.GetPlainText($"{classificationName}_ResponseComments"),
            isInternetBlocked
                ? "The response contained the words internet, access, and blocked."
                : "The HTTP response status was 504.");
    }
}
