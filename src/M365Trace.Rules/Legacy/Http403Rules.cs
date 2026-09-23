namespace M365Trace.Rules.Legacy;

public sealed class Http403Rules : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public Http403Rules(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.HTTP.403";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 403;

    public void Evaluate(AnalysisContext context)
    {
        var session = context.Session;
        string classificationName;
        string stringPrefix;
        string ruleId;
        string evidence;

        if (session.ResponseContains("Access Denied")
            || session.ResponseContains("Access Blocked"))
        {
            classificationName = "HTTP_403_Forbidden_Proxy_Block";
            stringPrefix = classificationName;
            ruleId = "M365.HTTP.403.ProxyBlock";
            evidence = "The response contained Access Denied or Access Blocked.";
        }
        else if (session.UrlContains("outlook.office365.com/EWS"))
        {
            classificationName =
                "HTTP_403_Forbidden_EWS_Mailbox_Language_Not_Set";
            stringPrefix = classificationName;
            ruleId = "M365.HTTP.403.EwsMailboxLanguage";
            evidence = "The request was an Exchange Online EWS call.";
        }
        else if (session.UrlContains("outlook.office365.com")
                 && (session.UrlContains("CalendarService")
                     || session.UrlContains("outlookgatewayb2")
                     || session.UrlContains("SchedulingB2"))
                 && session.ResponseContains(
                     "Request failed with http code Forbidden"))
        {
            classificationName =
                "HTTP_403_FreeBusy_Request_failed_with_http_code_Forbidden";
            stringPrefix =
                "HTTP_403s_FreeBusy_Request_failed_with_http_code_Forbidden";
            ruleId = "M365.HTTP.403.FreeBusyForbidden";
            evidence =
                "The Microsoft 365 scheduling response contained 'Request failed with http code Forbidden'.";
        }
        else
        {
            classificationName = "HTTP_403_Forbidden_Everything_Else";
            stringPrefix = classificationName;
            ruleId = "M365.HTTP.403.Forbidden";
            evidence = "The HTTP response status was 403.";
        }

        LegacyRuleApplication.Apply(
            context,
            _data.GetClassification("HTTP_403s", classificationName),
            ruleId,
            _data.GetString($"{stringPrefix}_SessionType"),
            _data.GetPlainText($"{stringPrefix}_ResponseAlert"),
            _data.GetPlainText($"{stringPrefix}_ResponseComments"),
            evidence);
    }
}
