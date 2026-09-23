namespace M365Trace.Rules.Legacy;

public sealed class Http500Rules : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public Http500Rules(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.HTTP.500";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 500;

    public void Evaluate(AnalysisContext context)
    {
        var session = context.Session;
        string classificationName;
        string ruleId;
        string evidence;

        if (string.Equals(
                session.Url.Host,
                "outlook.office365.com",
                StringComparison.OrdinalIgnoreCase)
            && session.ResponseContains("Repeating redirects detected"))
        {
            classificationName =
                "HTTP_500_Internal_Server_Error_Repeating_Redirects";
            ruleId = "M365.HTTP.500.RepeatingRedirects";
            evidence =
                "The Exchange Online response contained 'Repeating redirects detected'.";
        }
        else if (string.Equals(
                     session.Url.Host,
                     "outlook.office365.com",
                     StringComparison.OrdinalIgnoreCase)
                 && session.UrlContains("/EWS/Exchange.asmx")
                 && session.ResponseContains("ErrorImpersonateUserDenied"))
        {
            classificationName =
                "HTTP_500_Internal_Server_Error_Impersonate_User_Denied";
            ruleId = "M365.HTTP.500.EwsImpersonationDenied";
            evidence =
                "The EWS response contained ErrorImpersonateUserDenied.";
        }
        else if (string.Equals(
                     session.Url.Host,
                     "outlook.office365.com",
                     StringComparison.OrdinalIgnoreCase)
                 && session.ResponseContains("Something went wrong"))
        {
            classificationName =
                "HTTP_500_Internal_Server_Error_OWA_Something_Went_Wrong";
            ruleId = "M365.HTTP.500.OwaSomethingWentWrong";
            evidence =
                "The Exchange Online response contained 'Something went wrong'.";
        }
        else
        {
            classificationName =
                "HTTP_500_Internal_Server_Error_All_Others";
            ruleId = "M365.HTTP.500.InternalServerError";
            evidence = "The HTTP response status was 500.";
        }

        LegacyRuleApplication.Apply(
            context,
            _data.GetClassification("HTTP_500s", classificationName),
            ruleId,
            _data.GetString($"{classificationName}_SessionType"),
            _data.GetPlainText($"{classificationName}_ResponseAlert"),
            _data.GetPlainText($"{classificationName}_ResponseComments"),
            evidence);
    }
}
