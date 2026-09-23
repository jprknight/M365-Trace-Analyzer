namespace M365Trace.Rules.Legacy;

public sealed class Http400Rules : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public Http400Rules(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.HTTP.400";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 400;

    public void Evaluate(AnalysisContext context)
    {
        var isCloudAuthentication = string.Equals(
            context.Session.Url.Host,
            "login.microsoftonline.com",
            StringComparison.OrdinalIgnoreCase);
        var classificationName = isCloudAuthentication
            ? "HTTP_400_Cloud_Authentication"
            : "HTTP_400_Everything_Else";
        var stringPrefix = isCloudAuthentication
            ? "HTTP_400_Cloud_Authentication"
            : "HTTP_400s";

        LegacyRuleApplication.Apply(
            context,
            _data.GetClassification("HTTP_400s", classificationName),
            isCloudAuthentication
                ? "M365.HTTP.400.CloudAuthentication"
                : "M365.HTTP.400.BadRequest",
            _data.GetString($"{stringPrefix}_SessionType"),
            _data.GetPlainText($"{stringPrefix}_ResponseAlert"),
            _data.GetPlainText($"{stringPrefix}_ResponseComments"),
            isCloudAuthentication
                ? "The HTTP status was 400 and the host was login.microsoftonline.com."
                : "The HTTP response status was 400.");
    }
}
