using M365Trace.Core;

namespace M365Trace.Rules.Legacy;

public sealed record LegacyClassificationDefinition(
    string? SectionTitle,
    string? SessionType,
    string? ResponseAlert,
    RuleClassification Classification)
{
    public int AuthenticationConfidence => Classification.AuthenticationConfidence;

    public int SessionTypeConfidence => Classification.SessionTypeConfidence;

    public int ResponseServerConfidence => Classification.ResponseServerConfidence;

    public TraceSeverity Severity => Classification.Severity;
}
