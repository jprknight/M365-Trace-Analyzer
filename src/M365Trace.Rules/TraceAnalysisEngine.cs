using M365Trace.Core;

namespace M365Trace.Rules;

public sealed class TraceAnalysisEngine
{
    private readonly RuleCatalog _catalog;

    public TraceAnalysisEngine(RuleCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public TraceAnalysisEngine(IEnumerable<ITraceRule> rules)
        : this(new RuleCatalog(
            rules,
            new RulesetManifestProvider()))
    {
    }

    public TraceAnalysisResult Analyze(TraceSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var context = new AnalysisContext(session);

        foreach (var rule in _catalog.Rules)
        {
            if (rule.AppliesTo(context))
            {
                rule.Evaluate(context);
            }
        }

        return context.Build();
    }
}
