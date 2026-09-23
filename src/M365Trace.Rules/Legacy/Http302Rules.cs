namespace M365Trace.Rules.Legacy;

public sealed class Http302Rules : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public Http302Rules(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.HTTP.302";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 302;

    public void Evaluate(AnalysisContext context)
    {
        var isAutodiscover = context.Session.UrlContains("autodiscover");
        var classificationName = isAutodiscover
            ? "HTTP_302_Redirect_AutoDiscover"
            : "HTTP_302_Redirect_AllOthers";
        var stringPrefix = classificationName;

        LegacyRuleApplication.Apply(
            context,
            _data.GetClassification("HTTP302s", classificationName),
            isAutodiscover
                ? "M365.HTTP.302.AutodiscoverRedirect"
                : "M365.HTTP.302.Redirect",
            _data.GetString($"{stringPrefix}_SessionType"),
            _data.GetPlainText($"{stringPrefix}_ResponseAlert"),
            _data.GetPlainText($"{stringPrefix}_ResponseComments"),
            isAutodiscover
                ? "The HTTP status was 302 and the request URL contains Autodiscover."
                : "The HTTP response status was 302.");
    }
}
