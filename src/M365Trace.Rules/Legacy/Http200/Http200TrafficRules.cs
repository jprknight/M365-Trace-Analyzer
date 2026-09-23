namespace M365Trace.Rules.Legacy.Http200;

public sealed class Http200MapiRule : Http200RuleBase
{
    public Http200MapiRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.Mapi";

    public override int Order => 900;

    protected override bool Matches(AnalysisContext context) =>
        context.Facts.UrlContains("/mapi/emsmdb/?MailboxId=");

    public override void Evaluate(AnalysisContext context)
    {
        var exchangeOnline = IsExchangeOnlineMapi(context.Facts);
        ApplyByPrefix(
            context,
            exchangeOnline
                ? "HTTP_200_Outlook_Exchange_Online_Microsoft_365_Mapi"
                : "HTTP_200_Outlook_Exchange_OnPremise_Mapi",
            exchangeOnline
                ? "HTTP_200_Outlook_Exchange_Online_Microsoft_365_Mapi"
                : "HTTP_200_Outlook_Exchange_OnPremise_Mapi",
            exchangeOnline
                ? "M365.HTTP.200.ExchangeOnlineMapi"
                : "M365.HTTP.200.ExchangeServerMapi",
            exchangeOnline
                ? "The request was successful Exchange Online MAPI over HTTP traffic."
                : "The request was successful Exchange Server MAPI over HTTP traffic.");
    }
}

public sealed class Http200OwaNotificationRule : Http200RuleBase
{
    public Http200OwaNotificationRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.OwaNotificationChannel";

    public override int Order => 1000;

    protected override bool Matches(AnalysisContext context) =>
        context.Facts.UrlContains("/owa/notificationchannel/");

    public override void Evaluate(AnalysisContext context) =>
        ApplyByPrefix(
            context,
            "HTTP_200_Outlook_Web_App_Notification_Channel",
            "HTTP_200_Outlook_Web_App_Notification_Channel",
            Id,
            "The request was an OWA notification channel call.");
}

public sealed class Http200OwaRule : Http200RuleBase
{
    public Http200OwaRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.Owa";

    public override int Order => 1100;

    protected override bool Matches(AnalysisContext context) =>
        context.Facts.UrlContains("/owa/");

    public override void Evaluate(AnalysisContext context) =>
        ApplyByPrefix(
            context,
            "HTTP_200_Outlook_Web_App",
            "HTTP_200_Outlook_Web_App",
            Id,
            "The request was successful Outlook on the web traffic.");
}

public sealed class Http200RpcRule : Http200RuleBase
{
    public Http200RpcRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.OutlookRpc";

    public override int Order => 1200;

    protected override bool Matches(AnalysisContext context) =>
        context.Facts.UrlContains("/rpc/emsmdb/");

    public override void Evaluate(AnalysisContext context) =>
        ApplyByPrefix(
            context,
            "HTTP_200_Outlook_RPC",
            "HTTP_200_Outlook_RPC",
            Id,
            "The request was successful Outlook RPC over HTTP traffic.");
}

public sealed class Http200NspiRule : Http200RuleBase
{
    public Http200NspiRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.OutlookNspi";

    public override int Order => 1300;

    protected override bool Matches(AnalysisContext context) =>
        context.Facts.UrlContains("/mapi/nspi/");

    public override void Evaluate(AnalysisContext context) =>
        ApplyByPrefix(
            context,
            "HTTP_200_Outlook_NSPI",
            "HTTP_200_Outlook_NSPI",
            Id,
            "The request was successful Outlook NSPI traffic.");
}

public sealed class Http200SuggestionsRule : Http200RuleBase
{
    public Http200SuggestionsRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.Suggestions";

    public override int Order => 1400;

    protected override bool Matches(AnalysisContext context) =>
        context.Facts.UrlContains("search/api/v1/suggestions");

    public override void Evaluate(AnalysisContext context) =>
        Apply(
            context,
            "HTTP_200_3S_Suggestions",
            Id,
            Data.GetString("HTTP_200_3S_Suggestions"),
            Data.GetString("HTTP_200_3S_Suggestions"),
            "A successful Microsoft Search suggestions response was detected.",
            $"The URL contained search/api/v1/suggestions. {context.Session.Url.Query}");
}

public sealed class Http200RestPeopleRule : Http200RuleBase
{
    public Http200RestPeopleRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.RestPeople";

    public override int Order => 1500;

    protected override bool Matches(AnalysisContext context) =>
        context.Facts.UrlContains("people");

    public override void Evaluate(AnalysisContext context)
    {
        var requestKind = context.Facts.UrlContains("/me/people")
            ? "Private"
            : context.Facts.UrlContains("/users(")
              && context.Facts.UrlContains("/people")
                ? "Public"
                : string.Empty;
        var sessionType =
            Data.GetString("HTTP_200_REST_People_Request_SessionType");
        var alert =
            Data.GetString("HTTP_200_REST_People_Request_ResponseAlert");

        Apply(
            context,
            "HTTP_200_REST_People_Request",
            Id,
            $"{sessionType} {requestKind}".Trim(),
            $"{alert} {requestKind}".Trim(),
            "A successful REST People request was detected.",
            $"The URL contained a People endpoint. {context.Session.Url.Query}");
    }
}

public sealed class Http200EwsRule : Http200RuleBase
{
    public Http200EwsRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.Ews";

    public override int Order => 1600;

    protected override bool Matches(AnalysisContext context) =>
        context.Facts.UrlContains("ews/exchange.asmx");

    public override void Evaluate(AnalysisContext context)
    {
        var exchangeOnline = string.Equals(
            context.Facts.Host,
            "outlook.office365.com",
            StringComparison.OrdinalIgnoreCase);

        ApplyByPrefix(
            context,
            exchangeOnline
                ? "HTTP_200_Exchange_Online_Any_Other_EWS"
                : "HTTP_200_OnPremise_Any_Other_EWS",
            exchangeOnline
                ? "HTTP_200s_Microsoft365_Any_Other_EWS"
                : "HTTP_200s_OnPremise_Exchange_EWS",
            exchangeOnline
                ? "M365.HTTP.200.ExchangeOnlineEws"
                : "M365.HTTP.200.ExchangeServerEws",
            exchangeOnline
                ? "The request was a successful Exchange Online EWS call."
                : "The request was a successful Exchange Server EWS call.");
    }
}
