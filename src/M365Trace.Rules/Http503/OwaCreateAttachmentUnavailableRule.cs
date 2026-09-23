using M365Trace.Core;

namespace M365Trace.Rules.Http503;

public sealed class OwaCreateAttachmentUnavailableRule : ITraceRule
{
    private const string CreateAttachmentPath =
        "outlook.office.com/owa/service.svc/CreateAttachment";

    public string Id => "M365.HTTP.503.OwaCreateAttachment";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 200;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 503
        && context.SessionTypeConfidence < 10
        && context.Session.UrlContains(CreateAttachmentPath);

    public void Evaluate(AnalysisContext context)
    {
        Http503RuleDefaults.Apply(
            context,
            "503 OWA unable to CreateAttachment!",
            new TraceFinding
            {
                RuleId = Id,
                Title = "HTTP 503 while OWA created an attachment",
                Severity = TraceSeverity.Severe,
                Description =
                    "Outlook on the web was unable to complete the CreateAttachment operation.",
                Recommendation =
                    "Inspect the response body for additional details. Check service health and review whether an internet proxy or security device is blocking or modifying the attachment request.",
                Evidence = "The request URL matched the OWA CreateAttachment service endpoint."
            });
    }
}
