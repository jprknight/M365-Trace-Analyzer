using M365Trace.Core;

namespace M365Trace.Rules.Http503;

public sealed class GenericServiceUnavailableRule : ITraceRule
{
    public string Id => "M365.HTTP.503.ServiceUnavailable";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 1000;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 503
        && context.SessionTypeConfidence < 10;

    public void Evaluate(AnalysisContext context)
    {
        Http503RuleDefaults.Apply(
            context,
            "Service Unavailable",
            new TraceFinding
            {
                RuleId = Id,
                Title = "HTTP 503 Service Unavailable",
                Severity = TraceSeverity.Severe,
                Description =
                    "The server or an intermediary was unable to service this request.",
                Recommendation =
                    "Consider the number and timing of 503 responses in the trace. Review Microsoft 365 service health, retry behavior, proxies, load balancers, and the response body for a more specific cause.",
                Evidence = "The HTTP response status was 503."
            });
    }
}
