namespace M365Trace.Rules.Legacy;

public sealed class Http307Rules : ITraceRule
{
    private const string ExpectedAutodiscoverLocation =
        "https://autodiscover-s.outlook.com/autodiscover/autodiscover.xml";

    private readonly LegacyRulesetData _data;

    public Http307Rules(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.HTTP.307";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 307;

    public void Evaluate(AnalysisContext context)
    {
        var session = context.Session;
        var isAutodiscover = session.Url.Host.Contains(
            "autodiscover",
            StringComparison.OrdinalIgnoreCase)
            || session.UrlContains("autodiscover");
        var location = session.GetResponseHeader("Location");

        if (isAutodiscover
            && session.Url.Host.Contains(
                "mail.onmicrosoft.com",
                StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                location,
                ExpectedAutodiscoverLocation,
                StringComparison.OrdinalIgnoreCase))
        {
            const string prefix =
                "HTTP_307_AutoDiscover_Temporary_Redirect";
            LegacyRuleApplication.Apply(
                context,
                _data.GetClassification(
                    "HTTP_307s",
                    "HTTP_307_AutoDiscover_Temporary_Redirect"),
                "M365.HTTP.307.UnexpectedAutodiscoverLocation",
                _data.GetString($"{prefix}_SessionType"),
                _data.GetPlainText($"{prefix}_ResponseAlert"),
                _data.GetPlainText($"{prefix}_ResponseComments"),
                $"The Location header was '{location ?? "(missing)"}'.",
                responseServer: _data.GetString($"{prefix}_ResponseServer"));
            return;
        }

        var classificationName = isAutodiscover
            ? "HTTP_307_Other_AutoDiscover_Redirects"
            : "HTTP_307_All_Other_Redirects";

        LegacyRuleApplication.Apply(
            context,
            _data.GetClassification("HTTP_307s", classificationName),
            isAutodiscover
                ? "M365.HTTP.307.AutodiscoverRedirect"
                : "M365.HTTP.307.TemporaryRedirect",
            _data.GetString($"{classificationName}_SessionType"),
            _data.GetPlainText($"{classificationName}_ResponseAlert"),
            _data.GetPlainText($"{classificationName}_ResponseComments"),
            isAutodiscover
                ? "The HTTP status was 307 and the request URL contains Autodiscover."
                : "The HTTP response status was 307.");
    }
}
