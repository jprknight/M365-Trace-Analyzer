namespace M365Trace.Rules.Legacy;

public sealed class ResponseServerRule : ITraceRule
{
    private static readonly string[] HeaderPriority =
    [
        "Server",
        "Host",
        "X-Powered-By",
        "X-Served-By",
        "X-Server-Name",
        "X-CDN-Provider"
    ];

    private readonly LegacyRulesetData _data;

    public ResponseServerRule(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.ResponseServer";

    public AnalysisPhase Phase => AnalysisPhase.ResponseServer;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.ResponseServerConfidence < 10;

    public void Evaluate(AnalysisContext context)
    {
        foreach (var headerName in HeaderPriority)
        {
            var value = context.Session.GetResponseHeader(headerName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                context.SetResponseServer(value, 10);
                return;
            }
        }

        context.SetResponseServer(
            _data.GetString("ResponseServer_Unknown"),
            10);
    }
}
