namespace M365Trace.Rules.Legacy;

public sealed class SessionTypeRule : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public SessionTypeRule(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.SessionType";

    public AnalysisPhase Phase => AnalysisPhase.SessionType;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.SessionTypeConfidence < 10;

    public void Evaluate(AnalysisContext context)
    {
        var session = context.Session;
        string value;

        if (session.UrlContains("/EWS"))
        {
            value = _data.GetString("Exchange Web Services");
        }
        else if (string.Equals(
                     session.Url.Host,
                     "login.microsoftonline.com",
                     StringComparison.OrdinalIgnoreCase))
        {
            value = _data.GetString("Microsoft365 Authentication");
        }
        else if (session.UrlContains("adfs/services/trust/mex"))
        {
            value = _data.GetString("ADFS Authentication");
        }
        else if (session.UrlContains("outlook.office365.com")
                 || session.UrlContains("outlook.office.com"))
        {
            value = _data.GetString("General Microsoft365");
        }
        else
        {
            value = _data.GetString("Unclassified");
        }

        context.SetSessionType(value, 10);
    }
}
