using System.Reflection;
using System.Text.Json;

namespace M365Trace.Rules;

public sealed class RulesetManifestProvider
{
    private const string ResourceName =
        "M365Trace.Rules.Data.RulesetManifest.json";

    public RulesetManifestProvider()
    {
        Manifest = Load();
    }

    public RulesetManifest Manifest { get; }

    private static RulesetManifest Load()
    {
        var assembly = typeof(RulesetManifestProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded ruleset manifest '{ResourceName}' was not found.");

        return JsonSerializer.Deserialize<RulesetManifest>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? throw new InvalidDataException(
                "The embedded ruleset manifest is empty or invalid.");
    }
}
