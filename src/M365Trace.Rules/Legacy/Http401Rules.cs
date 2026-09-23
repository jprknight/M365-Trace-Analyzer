namespace M365Trace.Rules.Legacy;

public sealed class Http401Rules : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public Http401Rules(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.HTTP.401";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 401;

    public void Evaluate(AnalysisContext context)
    {
        var session = context.Session;
        string classificationName;
        string stringPrefix;
        string ruleId;

        if ((string.Equals(
                 session.Url.Host,
                 "autodiscover-s.outlook.com",
                 StringComparison.OrdinalIgnoreCase)
             || string.Equals(
                 session.Url.Host,
                 "outlook.office365.com",
                 StringComparison.OrdinalIgnoreCase))
            && session.UrlContains("autodiscover.xml"))
        {
            classificationName = "HTTP_401_Exchange_Online_AutoDiscover";
            stringPrefix = classificationName;
            ruleId = "M365.HTTP.401.ExchangeOnlineAutodiscover";
        }
        else if (session.UrlContains("/Autodiscover/Autodiscover.xml"))
        {
            classificationName = "HTTP_401_Exchange_OnPremise_AutoDiscover";
            stringPrefix = "HTTP_401_Exchange_Server_AutoDiscover";
            ruleId = "M365.HTTP.401.ExchangeServerAutodiscover";
        }
        else if (session.UrlContains("/EWS/Exchange.asmx"))
        {
            classificationName = "HTTP_401_EWS";
            stringPrefix = classificationName;
            ruleId = "M365.HTTP.401.Ews";
        }
        else
        {
            classificationName = "HTTP_401_Everything_Else";
            stringPrefix = classificationName;
            ruleId = "M365.HTTP.401.AuthenticationChallenge";
        }

        LegacyRuleApplication.Apply(
            context,
            _data.GetClassification("HTTP_401s", classificationName),
            ruleId,
            _data.GetString($"{stringPrefix}_SessionType"),
            _data.GetPlainText($"{stringPrefix}_ResponseAlert"),
            _data.GetPlainText($"{stringPrefix}_ResponseComments"),
            "The HTTP response status was 401.");
    }
}
