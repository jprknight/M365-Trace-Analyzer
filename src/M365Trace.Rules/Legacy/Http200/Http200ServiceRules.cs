using System.Text.RegularExpressions;

namespace M365Trace.Rules.Legacy.Http200;

public sealed class Http200AttachmentRule : Http200RuleBase
{
    public Http200AttachmentRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.OwaAttachment";

    public override int Order => 500;

    protected override bool Matches(AnalysisContext context) =>
        context.Facts.Host.Contains(
            "attachments.office.net",
            StringComparison.OrdinalIgnoreCase)
        && context.Facts.UrlContains("/owa/")
        && (context.Facts.UrlContains("/GetAttachmentThumbnail")
            || context.Facts.UrlContains("/GetFileAttachment"));

    public override void Evaluate(AnalysisContext context)
    {
        var thumbnail = context.Facts.UrlContains("/GetAttachmentThumbnail");
        var prefix = thumbnail
            ? "HTTP_200_OWA_AttachmentThumbnail"
            : "HTTP_200_OWA_Attachment";

        ApplyByPrefix(
            context,
            prefix,
            prefix,
            thumbnail
                ? "M365.HTTP.200.OwaAttachmentThumbnail"
                : "M365.HTTP.200.OwaAttachment",
            thumbnail
                ? "OWA retrieved an attachment thumbnail from the Microsoft cloud attachment service."
                : "OWA retrieved a file from the Microsoft cloud attachment service.");
    }
}

public sealed class Http200AutodiscoverRule : Http200RuleBase
{
    public Http200AutodiscoverRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.Autodiscover";

    public override int Order => 600;

    protected override bool Matches(AnalysisContext context)
    {
        var facts = context.Facts;
        return facts.ResponseContains("<Action>redirectAddr</Action>")
            || (facts.ResponseContains(
                    "<Message>The email address can't be found.</Message>")
                && facts.ResponseContains("<ErrorCode>500</ErrorCode>"))
            || (facts.UrlContains("autodiscover.xml")
                && (IsMicrosoftAutodiscoverHost(facts.Host)
                    || string.Equals(
                        facts.Host,
                        "outlook.office365.com",
                        StringComparison.OrdinalIgnoreCase)));
    }

    public override void Evaluate(AnalysisContext context)
    {
        var facts = context.Facts;

        if (facts.ResponseContains("<Action>redirectAddr</Action>")
            && !IsMicrosoftAutodiscoverHost(facts.Host))
        {
            ApplyRedirect(context);
            return;
        }

        if (!IsMicrosoftAutodiscoverHost(facts.Host)
            && facts.ResponseContains(
                "<Message>The email address can't be found.</Message>")
            && facts.ResponseContains("<ErrorCode>500</ErrorCode>"))
        {
            ApplyByPrefix(
                context,
                "HTTP_200_OnPremise_AutoDiscover_Redirect_AddressNotFound",
                "HTTP_200_OnPremise_AutoDiscover_Redirect_AddressNotFound",
                "M365.HTTP.200.AutodiscoverAddressNotFound",
                "The on-premises Autodiscover response returned error 500 because the email address could not be found.");
            return;
        }

        var hasExpectedXml =
            facts.ResponseContains("<DisplayName>")
            && facts.ResponseContains("<MicrosoftOnline>")
            && facts.ResponseContains("<MailStore>")
            && facts.ResponseContains("<ExternalUrl>");

        if (string.Equals(
                facts.Host,
                "autodiscover-s.outlook.com",
                StringComparison.OrdinalIgnoreCase))
        {
            ApplyByPrefix(
                context,
                hasExpectedXml
                    ? "HTTP_200_Exchange_Online_Microsoft365_AutoDiscover_MSI_Non_ClickToRun"
                    : "HTTP_200_Exchange_Online_Microsoft365_AutoDiscover_MSI_Non_ClickToRun_Unexpected_XML_Response",
                hasExpectedXml
                    ? "HTTP_200s_EXO_MSI_Autodiscover"
                    : "HTTP_200s_MSI_AutoDiscover",
                hasExpectedXml
                    ? "M365.HTTP.200.AutodiscoverMsi"
                    : "M365.HTTP.200.AutodiscoverMsiUnexpected",
                hasExpectedXml
                    ? "Expected Exchange Online Autodiscover XML elements were present."
                    : "Expected Exchange Online Autodiscover XML elements were missing.");
            return;
        }

        ApplyByPrefix(
            context,
            hasExpectedXml
                ? "HTTP_200_Exchange_Online_Microsoft365_AutoDiscover_ClickToRun"
                : "HTTP_200_Exchange_Online_Microsoft365_AutoDiscover_ClickToRun_XML_Response_Not_Found",
            hasExpectedXml
                ? "HTTP_200s_CTR_AutoDiscover"
                : "HTTP_200s_CTR_AutoDiscover_NotFound",
            hasExpectedXml
                ? "M365.HTTP.200.AutodiscoverClickToRun"
                : "M365.HTTP.200.AutodiscoverClickToRunUnexpected",
            hasExpectedXml
                ? "Expected Click-to-Run Autodiscover XML elements were present."
                : "Expected Click-to-Run Autodiscover XML elements were missing.");
    }

    private void ApplyRedirect(AnalysisContext context)
    {
        var redirectAddress = Regex.Match(
                context.Facts.ResponseText,
                "<RedirectAddr>(?<address>.*?)</RedirectAddr>",
                RegexOptions.IgnoreCase
                | RegexOptions.Singleline
                | RegexOptions.CultureInvariant)
            .Groups["address"]
            .Value;
        var correct = redirectAddress.Contains(
            ".onmicrosoft.com",
            StringComparison.OrdinalIgnoreCase);
        var classificationName = correct
            ? "HTTP_200_OnPremise_AutoDiscover_Redirect_Address_Found"
            : "HTTP_200_OnPremise_AutoDiscover_IncorrectRedirect";
        var comments = Data.GetPlainText(
                $"{classificationName}_ResponseCommentsStart")
            + " "
            + (string.IsNullOrWhiteSpace(redirectAddress)
                ? "Redirect address not found"
                : redirectAddress)
            + " "
            + Data.GetPlainText(
                $"{classificationName}_ResponseCommentsEnd");

        Apply(
            context,
            classificationName,
            correct
                ? "M365.HTTP.200.AutodiscoverRedirect"
                : "M365.HTTP.200.AutodiscoverIncorrectRedirect",
            Data.GetString($"{classificationName}_SessionType"),
            Data.GetPlainText($"{classificationName}_ResponseAlert"),
            comments,
            "The response contained an on-premises Autodiscover redirect address.");
    }

    private static bool IsMicrosoftAutodiscoverHost(string host) =>
        string.Equals(
            host,
            "autodiscover-s.outlook.com",
            StringComparison.OrdinalIgnoreCase)
        || string.Equals(
            host,
            "autodiscover.outlook.com",
            StringComparison.OrdinalIgnoreCase);
}

public sealed class Http200UnifiedGroupsRule : Http200RuleBase
{
    public Http200UnifiedGroupsRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.UnifiedGroups";

    public override int Order => 700;

    protected override bool Matches(AnalysisContext context) =>
        string.Equals(
            context.Facts.Host,
            "outlook.office365.com",
            StringComparison.OrdinalIgnoreCase)
        && context.Facts.UrlContains("ews/exchange.asmx")
        && context.Facts.RequestOrResponseContains("GetUnifiedGroupsSettings");

    public override void Evaluate(AnalysisContext context)
    {
        var classificationName =
            context.Facts.ResponseContains(
                "<GroupCreationEnabled>true</GroupCreationEnabled>")
                ? "HTTP_200_Unified_Groups_Settings"
                : context.Facts.ResponseContains(
                    "<GroupCreationEnabled>false</GroupCreationEnabled>")
                    ? "HTTP_200_Unified_Groups_Settings_User_Cannot_Create_Groups"
                    : "HTTP_200_Unified_Groups_Settings_Settings_Not_Found";

        ApplyByPrefix(
            context,
            classificationName,
            classificationName,
            classificationName switch
            {
                "HTTP_200_Unified_Groups_Settings" =>
                    "M365.HTTP.200.UnifiedGroupsEnabled",
                "HTTP_200_Unified_Groups_Settings_User_Cannot_Create_Groups" =>
                    "M365.HTTP.200.UnifiedGroupsDisabled",
                _ => "M365.HTTP.200.UnifiedGroupsSettingMissing"
            },
            "The EWS request queried GetUnifiedGroupsSettings.");
    }
}

public sealed class Http200FreeBusyRule : Http200RuleBase
{
    public Http200FreeBusyRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.FreeBusy";

    public override int Order => 800;

    protected override bool Matches(AnalysisContext context)
    {
        var facts = context.Facts;
        return facts.UrlContains("WSSecurity")
            || facts.UrlContains("GetUserAvailability")
            || facts.ResponseContains("GetUserAvailability")
            || facts.UrlContains("outlook.office.com/CalendarService/")
            || facts.UrlContains("outlook.office.com/outlookgatewayb2/")
            || facts.UrlContains("outlook.office.com/SchedulingB2/");
    }

    public override void Evaluate(AnalysisContext context)
    {
        var facts = context.Facts;

        if (facts.UrlContains("outlook.office365.com")
            && facts.RequestOrResponseContains("GetUserAvailability")
            && facts.RequestOrResponseContains(
                "The result set contains too many calendar entries"))
        {
            ApplyByPrefix(
                context,
                "HTTP_200_FreeBusy_Result_Set_Too_Many_Calendar_Items",
                "HTTP_200_FreeBusy_Result_Set_Too_Many_Calendar_Items",
                "M365.HTTP.200.FreeBusyTooManyItems",
                "The Free/Busy response reported too many calendar entries.");
            return;
        }

        if (facts.UrlContains("WSSecurity")
            || facts.UrlContains("GetUserAvailability")
            || facts.ResponseContains("GetUserAvailability"))
        {
            ApplyByPrefix(
                context,
                "HTTP_200_Legacy_FreeBusy",
                "HTTP_200_Legacy_FreeBusy",
                "M365.HTTP.200.LegacyFreeBusy",
                "A legacy Free/Busy request pattern was detected.");
            return;
        }

        if (facts.UrlContains("outlook.office.com/CalendarService/api/"))
        {
            ApplyByPrefix(
                context,
                "HTTP_200_Outlook_For_Windows_FreeBusy",
                "HTTP_200_Outlook_For_Windows_FreeBusy",
                "M365.HTTP.200.OutlookFreeBusy",
                "An Outlook for Windows cloud Free/Busy request was detected.");
            return;
        }

        ApplyByPrefix(
            context,
            "HTTP_200_OWA_FreeBusy",
            "HTTP_200_OWA_FreeBusy",
            "M365.HTTP.200.OwaFreeBusy",
            "An OWA cloud Free/Busy request was detected.");
    }
}
