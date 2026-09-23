namespace M365Trace.Rules.Legacy;

public sealed class Http456Rules : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public Http456Rules(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.HTTP.456";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 456;

    public void Evaluate(AnalysisContext context)
    {
        var classificationName =
            context.Session.ResponseContains(
                "you must use multi-factor authentication")
                ? "HTTP_456_Multi_Factor_Required"
                : context.Session.ResponseContains("oauth_not_available")
                    ? "HTTP_456_OAuth_Not_Available"
                    : "HTTP_456_Anything_Else";

        LegacyRuleApplication.Apply(
            context,
            _data.GetClassification("HTTP_456s", classificationName),
            classificationName switch
            {
                "HTTP_456_Multi_Factor_Required" =>
                    "M365.HTTP.456.MultiFactorRequired",
                "HTTP_456_OAuth_Not_Available" =>
                    "M365.HTTP.456.OAuthNotAvailable",
                _ => "M365.HTTP.456.AuthenticationRequired"
            },
            _data.GetString($"{classificationName}_SessionType"),
            _data.GetPlainText($"{classificationName}_ResponseAlert"),
            _data.GetPlainText($"{classificationName}_ResponseComments"),
            classificationName switch
            {
                "HTTP_456_Multi_Factor_Required" =>
                    "The response stated that multi-factor authentication must be used.",
                "HTTP_456_OAuth_Not_Available" =>
                    "The response contained oauth_not_available.",
                _ => "The HTTP response status was 456."
            });
    }
}
