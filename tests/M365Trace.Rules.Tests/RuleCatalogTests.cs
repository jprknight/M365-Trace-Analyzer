using M365Trace.Rules.Legacy;

namespace M365Trace.Rules.Tests;

public sealed class RuleCatalogTests
{
    [Fact]
    public void Catalog_LoadsEveryConcreteRuleWithUniqueStableIds()
    {
        var catalog = CreateCatalog();

        Assert.True(catalog.Rules.Count > 30);
        Assert.Equal(
            catalog.Rules.Count,
            catalog.Rules.Select(rule => rule.Id).Distinct().Count());
        Assert.All(
            catalog.Rules,
            rule => Assert.StartsWith("M365.", rule.Id));
    }

    [Fact]
    public void Catalog_UsesDeterministicPhaseOrderAndManifest()
    {
        var catalog = CreateCatalog();
        var expectedOrder = catalog.Rules
            .OrderBy(rule => rule.Phase)
            .ThenBy(rule => rule.Order)
            .ThenBy(rule => rule.Id, StringComparer.Ordinal)
            .Select(rule => rule.Id);

        Assert.Equal(expectedOrder, catalog.Rules.Select(rule => rule.Id));
        Assert.Equal("1.0.2", catalog.Manifest.RulesetVersion);
        Assert.Equal(1, catalog.Manifest.SchemaVersion);
        Assert.Equal("0.1.0", catalog.Manifest.MinimumApplicationVersion);
        Assert.Equal(151, catalog.Manifest.SessionClassificationCount);
        Assert.Equal(new DateOnly(2026, 9, 23), catalog.Manifest.Released);
    }

    private static RuleCatalog CreateCatalog()
    {
        var data = new LegacyRulesetData();
        var rules = typeof(ITraceRule).Assembly
            .GetTypes()
            .Where(type =>
                !type.IsAbstract
                && !type.IsInterface
                && typeof(ITraceRule).IsAssignableFrom(type))
            .Select(type => CreateRule(type, data))
            .ToArray();

        return new RuleCatalog(rules, new RulesetManifestProvider());
    }

    private static ITraceRule CreateRule(
        Type ruleType,
        LegacyRulesetData data)
    {
        var constructors = ruleType.GetConstructors();
        var parameterless = constructors.FirstOrDefault(
            constructor => constructor.GetParameters().Length == 0);
        if (parameterless is not null)
        {
            return (ITraceRule)parameterless.Invoke([]);
        }

        var withLegacyData = constructors.FirstOrDefault(constructor =>
        {
            var parameters = constructor.GetParameters();
            return parameters.Length == 1
                && parameters[0].ParameterType == typeof(LegacyRulesetData);
        });

        return withLegacyData is not null
            ? (ITraceRule)withLegacyData.Invoke([data])
            : throw new InvalidOperationException(
                $"Unsupported rule constructor for '{ruleType.FullName}'.");
    }
}
