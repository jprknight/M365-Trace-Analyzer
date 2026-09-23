using M365Trace.Core;

namespace M365Trace.Rules.Legacy;

public sealed class Http0Rules : ITraceRule
{
    private static readonly LegacyClassificationDefinition NotificationClassification = new(
        "HTTP_0s",
        null,
        null,
        new RuleClassification(5, 10, 5, TraceSeverity.Severe));

    private readonly LegacyRulesetData _data;

    public Http0Rules(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.HTTP.0";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 0;

    public void Evaluate(AnalysisContext context)
    {
        if (context.Session.UrlContains("/owa/notificationchannel/"))
        {
            LegacyRuleApplication.Apply(
                context,
                NotificationClassification,
                "M365.HTTP.0.OwaNotificationChannel",
                _data.GetString("HTTP_0_Outlook_Web_App_Notification_Channel_SessionType"),
                _data.GetPlainText("HTTP_0_Outlook_Web_App_Notification_Channel_ResponseAlert"),
                _data.GetPlainText("HTTP_0_Outlook_Web_App_Notification_Channel_ResponseComments"),
                "The request URL contains /owa/notificationchannel/.");
            return;
        }

        var definition = _data.GetClassification("HTTP0s");
        LegacyRuleApplication.Apply(
            context,
            definition,
            "M365.HTTP.0.NoResponse",
            _data.GetString("HTTP_0 SessionType"),
            _data.GetPlainText("HTTP_0 Response Alert"),
            _data.GetPlainText("HTTP_0 ResponseComments"),
            "No HTTP response status was recorded.",
            responseServer: _data.GetString("HTTP_0 ResonseServer"),
            authentication: _data.GetString("HTTP_0 Authentication"));
    }
}
