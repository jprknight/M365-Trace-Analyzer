namespace M365Trace.Rules.Legacy.Http200;

public sealed class Http200ConnectTunnelRule : Http200RuleBase
{
    public Http200ConnectTunnelRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.ConnectTunnel";

    public override int Order => 100;

    protected override bool Matches(AnalysisContext context) =>
        string.Equals(
            context.Session.Method,
            "CONNECT",
            StringComparison.OrdinalIgnoreCase);

    public override void Evaluate(AnalysisContext context) =>
        Apply(
            context,
            "HTTP_200_ConnectTunnelSessions",
            Id,
            Data.GetString("HTTP_200_ConnectTunnel"),
            Data.GetString("HTTP_200_ConnectTunnel"),
            Data.GetPlainText("HTTP_200_ConnectTunnel_RepsonseComments"),
            "The successful response established an encrypted CONNECT tunnel.");
}

public sealed class Http200ClientAccessRule : Http200RuleBase
{
    public Http200ClientAccessRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.ClientAccessRule";

    public override int Order => 200;

    protected override bool Matches(AnalysisContext context) =>
        context.Facts.UrlContains("outlook.office365.com/mapi")
        && context.Facts.RequestOrResponseContains(
            "Connection blocked by Client Access Rules");

    public override void Evaluate(AnalysisContext context) =>
        Apply(
            context,
            "HTTP_200_ClientAccessRule",
            Id,
            Data.GetString("HTTP_200_Client Access Rule"),
            Data.GetPlainText("HTTP_200_Client Access Rule ResponseAlert"),
            Data.GetPlainText("HTTP_200_Client Access Rule ResponseComments"),
            "The MAPI response stated that Client Access Rules blocked the connection.");
}

public sealed class Http200CultureNotFoundRule : Http200RuleBase
{
    public Http200CultureNotFoundRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.CultureNotFound";

    public override int Order => 300;

    protected override bool Matches(AnalysisContext context) =>
        IsExchangeOnlineMapi(context.Facts)
        && context.Facts.RequestOrResponseContains("Culture is not supported");

    public override void Evaluate(AnalysisContext context) =>
        ApplyByPrefix(
            context,
            "HTTP_200_CultureNotFound",
            "HTTP_200_CultureNotFound",
            Id,
            "The Exchange Online MAPI response reported an unsupported mailbox culture.",
            sessionTypeKey: "HTTP_200_CultureNotFound_Session_Type");
}

public sealed class Http200MapiProtocolDisabledRule : Http200RuleBase
{
    public Http200MapiProtocolDisabledRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.MapiProtocolDisabled";

    public override int Order => 400;

    protected override bool Matches(AnalysisContext context) =>
        IsExchangeOnlineMapi(context.Facts)
        && context.Facts.ResponseContains("ProtocolDisabled");

    public override void Evaluate(AnalysisContext context) =>
        ApplyByPrefix(
            context,
            "HTTP_200_Outlook_Mapi_Microsoft365_Protocol_Disabled",
            "HTTP_200_Outlook_Mapi_Microsoft365_Protocol_Disabled",
            Id,
            "The Exchange Online MAPI response contained ProtocolDisabled.");
}
