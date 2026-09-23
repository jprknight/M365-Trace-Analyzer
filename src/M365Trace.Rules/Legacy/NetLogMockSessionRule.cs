namespace M365Trace.Rules.Legacy;

public sealed class NetLogMockSessionRule : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public NetLogMockSessionRule(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.Broad.NetLogMock";

    public AnalysisPhase Phase => AnalysisPhase.BroadChecks;

    public int Order => 300;

    public bool AppliesTo(AnalysisContext context) =>
        string.Equals(
            context.Session.Url.Host,
            "NETLOG",
            StringComparison.OrdinalIgnoreCase);

    public void Evaluate(AnalysisContext context) =>
        LegacyRuleApplication.Apply(
            context,
            _data.GetClassification(
                "BroadLogicChecks",
                "NetLogCaptureMockSession"),
            Id,
            _data.GetString(
                "BroadLogicChecks_NetLogCaptureMockSession_SessionType"),
            _data.GetString(
                "BroadLogicChecks_NetLogCaptureMockSession_ResponseAlert"),
            _data.GetString(
                "BroadLogicChecks_NetLogCaptureMockSession_ResponseComments"),
            "The trace entry host was NETLOG.",
            responseServer: _data.GetString(
                "BroadLogicChecks_NetLogCaptureMockSession_ResponseServer"));
}
