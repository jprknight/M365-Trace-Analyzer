using M365Trace.Core;

namespace M365Trace.Rules;

public interface ITraceRule
{
    string Id { get; }

    AnalysisPhase Phase { get; }

    int Order { get; }

    bool AppliesTo(AnalysisContext context);

    void Evaluate(AnalysisContext context);
}
