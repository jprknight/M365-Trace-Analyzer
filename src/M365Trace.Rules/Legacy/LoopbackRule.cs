namespace M365Trace.Rules.Legacy;

public sealed class LoopbackRule : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public LoopbackRule(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.Broad.Loopback";

    public AnalysisPhase Phase => AnalysisPhase.BroadChecks;

    public int Order => 200;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.Url.IsLoopback;

    public void Evaluate(AnalysisContext context)
    {
        var label = _data.GetString("BroadLogicChecks_LoopbackTunnel");
        LegacyRuleApplication.Apply(
            context,
            _data.GetClassification("BroadLogicChecks", "LoopBackTunnel"),
            Id,
            label,
            label,
            _data.GetPlainText(
                "BroadLogicChecks_Loopback Tunnel Response Comments"),
            "The destination URL resolved to a loopback address.",
            responseServer: label,
            authentication: label);
    }
}
