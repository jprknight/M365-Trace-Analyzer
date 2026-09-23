namespace M365Trace.Rules.Legacy;

public sealed class ApacheAutodiscoverRule : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public ApacheAutodiscoverRule(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.Broad.ApacheAutodiscover";

    public AnalysisPhase Phase => AnalysisPhase.BroadChecks;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.UrlContains("autodiscover")
        && context.Session.GetResponseHeader("Server")?.Contains(
            "Apache",
            StringComparison.OrdinalIgnoreCase) == true;

    public void Evaluate(AnalysisContext context) =>
        LegacyRuleApplication.Apply(
            context,
            _data.GetClassification(
                "BroadLogicChecks",
                "ApacheAutodiscover"),
            Id,
            _data.GetString("BroadLogicChecks_APACHE AUTODISCOVER"),
            _data.GetString(
                "BroadLogicChecks_Apache is answering Autodiscover requests!"),
            _data.GetPlainText(
                "BroadLogicChecks_Apache AutoDiscover Response Comments"),
            "The Autodiscover response Server header identified Apache.",
            responseServer: _data.GetString("BroadLogicChecks_APACHE"));
}
