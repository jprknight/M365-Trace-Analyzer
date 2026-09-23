using M365Trace.Core;

namespace M365Trace.Rules.Legacy;

public sealed class LongRunningSessionRule : ITraceRule
{
    private const double WarningThresholdMilliseconds = 2500;
    private const double SlowThresholdMilliseconds = 5000;

    private readonly LegacyRulesetData _data;

    public LongRunningSessionRule(LegacyRulesetData data)
    {
        _data = data;
    }

    public string Id => "M365.Performance.Duration";

    public AnalysisPhase Phase => AnalysisPhase.Performance;

    public int Order => 100;

    public bool AppliesTo(AnalysisContext context) =>
        context.Session.Duration.TotalMilliseconds >=
        WarningThresholdMilliseconds;

    public void Evaluate(AnalysisContext context)
    {
        var slow = context.Session.Duration.TotalMilliseconds >=
            SlowThresholdMilliseconds;
        var prefix = slow
            ? "LongRunningSessionsClientSlow"
            : "LongRunningSessionsWarning";
        var severity = slow
            ? TraceSeverity.Severe
            : TraceSeverity.Warning;

        if (context.SessionTypeConfidence < 10)
        {
            context.SetSessionType(
                _data.GetString($"{prefix}_SessionType"),
                10);
        }

        context.AddFinding(new TraceFinding
        {
            RuleId = Id,
            Severity = severity,
            Title = _data.GetString($"{prefix}_SessionType"),
            Description =
                $"{_data.GetPlainText($"{prefix}_ResponseAlert")} "
                + _data.GetPlainText($"{prefix}_ResponseComments"),
            Evidence =
                $"Total trace duration was {context.Session.Duration.TotalMilliseconds:N0} ms."
        });
    }
}
