using M365Trace.Core;

namespace M365Trace.Rules;

public sealed record RuleClassification(
    int AuthenticationConfidence,
    int SessionTypeConfidence,
    int ResponseServerConfidence,
    TraceSeverity Severity);
