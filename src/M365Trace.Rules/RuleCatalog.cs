namespace M365Trace.Rules;

public sealed class RuleCatalog
{
    public RuleCatalog(
        IEnumerable<ITraceRule> rules,
        RulesetManifestProvider manifestProvider)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(manifestProvider);

        Rules = rules
            .OrderBy(rule => rule.Phase)
            .ThenBy(rule => rule.Order)
            .ThenBy(rule => rule.Id, StringComparer.Ordinal)
            .ToArray();
        Manifest = manifestProvider.Manifest;

        Validate();
    }

    public IReadOnlyList<ITraceRule> Rules { get; }

    public RulesetManifest Manifest { get; }

    private void Validate()
    {
        if (Rules.Count == 0)
        {
            throw new InvalidOperationException(
                "The ruleset catalog does not contain any rules.");
        }

        if (Manifest.SessionClassificationCount <= 0)
        {
            throw new InvalidDataException(
                "The ruleset manifest must declare at least one supported session classification.");
        }

        if (Manifest.SchemaVersion <= 0)
        {
            throw new InvalidDataException(
                "The ruleset manifest must declare a positive schema version.");
        }

        var invalidId = Rules.FirstOrDefault(rule =>
            string.IsNullOrWhiteSpace(rule.Id)
            || !rule.Id.StartsWith("M365.", StringComparison.Ordinal));
        if (invalidId is not null)
        {
            throw new InvalidOperationException(
                $"Rule type '{invalidId.GetType().FullName}' has an invalid stable rule ID.");
        }

        var duplicateRule = Rules
            .GroupBy(rule => rule.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateRule is not null)
        {
            throw new InvalidOperationException(
                $"The rule ID '{duplicateRule.Key}' is registered more than once.");
        }
    }
}
