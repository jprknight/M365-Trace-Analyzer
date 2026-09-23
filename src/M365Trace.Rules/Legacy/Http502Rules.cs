namespace M365Trace.Rules.Legacy;

public sealed class Http502Rules : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public Http502Rules(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.HTTP.502";

    public AnalysisPhase Phase => AnalysisPhase.ResponseCode;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.StatusCode == 502;

    public void Evaluate(AnalysisContext context)
    {
        var session = context.Session;
        var host = session.GetRequestHeader("Host") ?? session.Url.Authority;
        string classificationName;
        string stringPrefix;
        string ruleId;
        string evidence;
        string? responseServer = null;
        string? authentication = null;

        if (string.Equals(
                host,
                "sqm.telemetry.microsoft.com:443",
                StringComparison.OrdinalIgnoreCase)
            && session.ResponseContains("target machine actively refused it"))
        {
            classificationName =
                "HTTP_502_Bad_Gateway_Telemetry_False_Positive";
            stringPrefix = classificationName;
            ruleId = "M365.HTTP.502.TelemetryFalsePositive";
            evidence =
                "Telemetry traffic to sqm.telemetry.microsoft.com:443 was refused.";
        }
        else if (session.ResponseContains("DNS Lookup for ")
                 && session.ResponseContains(".onmicrosoft.com")
                 && session.ResponseContains(" failed."))
        {
            classificationName =
                "HTTP_502_Bad_Gateway_EXO_DNS_Lookup_False_Positive";
            stringPrefix = classificationName;
            ruleId = "M365.HTTP.502.ExoDnsFalsePositive";
            evidence =
                "The response described a failed DNS lookup for an onmicrosoft.com host.";
            responseServer = _data.GetString("False Positive");
            authentication = _data.GetString("False Positive");
        }
        else if (session.ResponseContains(".onmicrosoft.com")
                 && session.ResponseContains("autodiscover")
                 && session.ResponseContains(
                     "target machine actively refused it"))
        {
            classificationName =
                "HTTP_502_Bad_Gateway_EXO_AutoDiscover_False_Positive";
            stringPrefix = classificationName;
            ruleId = "M365.HTTP.502.ExoAutodiscoverFalsePositive";
            evidence =
                "An onmicrosoft.com Autodiscover endpoint refused the port 443 connection.";
            responseServer = _data.GetString("False Positive");
            authentication = _data.GetString("False Positive");
        }
        else if (session.ResponseContains(
                     "target machine actively refused it")
                 && session.ResponseContains("autodiscover"))
        {
            classificationName =
                "HTTP_502_Bad_Gateway_Anything_Else_AutoDiscover";
            stringPrefix = classificationName;
            ruleId = "M365.HTTP.502.AutodiscoverRefused";
            evidence =
                "An Autodiscover connection was actively refused.";
        }
        else
        {
            classificationName =
                "HTTP_502_Bad_Gateway_Anything_Else";
            stringPrefix = classificationName;
            ruleId = "M365.HTTP.502.BadGateway";
            evidence = "The HTTP response status was 502.";
        }

        var description = classificationName ==
            "HTTP_502_Bad_Gateway_EXO_AutoDiscover_False_Positive"
            ? _data.GetPlainText(
                "HTTP_502_Bad_Gateway_EXO_AutoDiscover_False_Positive_ResponseCommentsStart")
              + " "
              + _data.GetPlainText(
                  "HTTP_502_Bad_Gateway_EXO_AutoDiscover_False_Positive_ResponseCommentsEnd")
            : _data.GetPlainText($"{stringPrefix}_ResponseComments");

        LegacyRuleApplication.Apply(
            context,
            _data.GetClassification("HTTP_502s", classificationName),
            ruleId,
            _data.GetString($"{stringPrefix}_SessionType"),
            _data.GetPlainText($"{stringPrefix}_ResponseAlert"),
            description,
            evidence,
            responseServer: responseServer,
            authentication: authentication);
    }
}
