namespace M365Trace.Rules.Legacy;

public sealed class AuthenticationRule : ITraceRule
{
    private readonly LegacyRulesetData _data;

    public AuthenticationRule(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.Authentication";

    public AnalysisPhase Phase => AnalysisPhase.Authentication;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.AuthenticationConfidence < 10;

    public void Evaluate(AnalysisContext context)
    {
        var session = context.Session;
        var authorization = session.GetRequestHeader("Authorization") ?? string.Empty;
        var hasSaml = session.ResponseContains("Issuer=")
            && session.ResponseContains("Attribute AttributeName=")
            && session.ResponseContains("NameIdentifier Format=");

        if (hasSaml)
        {
            var adfs = session.UrlContains("adfs/ls");
            context.SetAuthentication(
                adfs
                    ? _data.GetString(
                        "Authentication_SAML_Response_Parser_Authentication")
                    : _data.GetString(
                        "Authentication_3rd_Party_Saml_Response_AuthenticationType"),
                10);
            context.SetSessionType(
                adfs
                    ? _data.GetString(
                        "Authentication_SAML_Response_Parser_SessionType")
                    : _data.GetString(
                        "Authentication_3rd_Party_Saml_Response_SessionType"),
                10);
            return;
        }

        if (authorization.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase))
        {
            context.SetAuthentication(
                _data.GetString(
                    "Authentication_Modern_Auth_Client_Using_Token_Authentication"),
                10);
            return;
        }

        if (authorization.StartsWith(
                "Basic ",
                StringComparison.OrdinalIgnoreCase))
        {
            var modernAuthUnavailable =
                !session.ResponseContainsWord("4000000")
                && !session.ResponseContainsWord("Flighting")
                && !session.ResponseContainsWord("enabled")
                && !session.ResponseContainsWord("domain")
                && !session.ResponseContainsWord("oauth_not_available");

            context.SetAuthentication(
                modernAuthUnavailable
                    ? _data.GetString(
                        "Authentication_Modern_Auth_Disabled_Authentication")
                    : _data.GetString(
                        "Authentication_Basic_Auth_Client_Using_Token_Authentication"),
                10);
            return;
        }

        context.SetAuthentication(
            _data.GetString("Authentication_No_Auth_Headers"),
            10);
    }
}
