using M365Trace.Core;

namespace M365Trace.Rules.Http503;

public sealed class FederatedStsUnavailableRule : ITraceRule
{
    public string Id => "M365.HTTP.503.FederatedStsUnavailable";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 503
        && context.Session.ResponseContainsWord("FederatedSTSUnreachable");

    public void Evaluate(AnalysisContext context)
    {
        var links = CreateLinks(context.Session);

        Http503RuleDefaults.Apply(
            context,
            "!FederatedSTSUnreachable!",
            new TraceFinding
            {
                RuleId = Id,
                Title = "HTTP 503: Federated STS unreachable",
                Severity = TraceSeverity.Severe,
                Description =
                    "The response indicates that the federated identity service is unreachable or unavailable.",
                Recommendation =
                    "Investigate federation service availability first. Validate DNS, proxy connectivity, load balancers, and the federation endpoints returned by the user realm service.",
                Evidence = "FederatedSTSUnreachable was found as a complete word in the response body.",
                Links = links
            });
    }

    private static IReadOnlyList<FindingLink> CreateLinks(TraceSession session)
    {
        var identity = session.GetRequestHeader("X-User-Identity");
        if (string.IsNullOrWhiteSpace(identity))
        {
            return [];
        }

        var realmUrl = new Uri(
            $"https://login.microsoftonline.com/GetUserRealm.srf?Login={Uri.EscapeDataString(identity)}&xml=1");

        return [new FindingLink("Open the Microsoft 365 user realm response", realmUrl)];
    }
}
