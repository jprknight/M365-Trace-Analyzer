namespace M365Trace.Rules.Legacy;

public sealed class Http404Rule : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public Http404Rule(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.HTTP.404";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 404;

    public void Evaluate(AnalysisContext context)
    {
        LegacyRuleApplication.Apply(
            context,
            _data.GetClassification("HTTP_404s"),
            Id,
            _data.GetString("HTTP_404s_SessionType"),
            _data.GetPlainText("HTTP_404s_ResponseAlerts"),
            _data.GetPlainText("HTTP_404s_ResponseComments"),
            "The HTTP response status was 404.");
    }
}
