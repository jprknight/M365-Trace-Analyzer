using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using M365Trace.Core;

namespace M365Trace.Rules.Legacy;

public sealed partial class LegacyRulesetData
{
    private const string ClassificationResource =
        "M365Trace.Rules.Data.SessionClassification.json";
    private const string StringsResource =
        "M365Trace.Rules.Data.RulesetStrings.json";

    private static readonly IReadOnlySet<int> SpecializedStatusCodes =
        new HashSet<int>
        {
            0, 200, 302, 307, 400, 401, 403, 404, 456,
            500, 502, 503, 504
        };

    private readonly JsonElement _classificationRoot;
    private readonly IReadOnlyDictionary<string, string> _strings;
    private readonly IReadOnlyDictionary<int, SimpleStatusDefinition> _simpleStatuses;
    private readonly IReadOnlySet<int> _knownStatusCodes;

    public LegacyRulesetData()
    {
        _classificationRoot = LoadJsonResource(ClassificationResource);
        _strings = LoadStrings();
        _simpleStatuses = LoadSimpleStatuses();

        var knownStatuses = new HashSet<int>(_simpleStatuses.Keys);
        knownStatuses.UnionWith(SpecializedStatusCodes);
        _knownStatusCodes = knownStatuses;
    }

    public IReadOnlyDictionary<int, SimpleStatusDefinition> SimpleStatuses =>
        _simpleStatuses;

    public bool IsKnownStatusCode(int statusCode) =>
        _knownStatusCodes.Contains(statusCode);

    public LegacyClassificationDefinition GetClassification(params string[] path)
    {
        if (path.Length == 0)
        {
            throw new ArgumentException("A classification path is required.", nameof(path));
        }

        var current = _classificationRoot;

        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object
                || !current.TryGetProperty(segment, out current))
            {
                throw new KeyNotFoundException(
                    $"Legacy classification '{string.Join("|", path)}' was not found.");
            }
        }

        return ParseClassification(current, string.Join("|", path));
    }

    public string GetString(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _strings.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException(
                $"Legacy ruleset string '{key}' was not found.");
    }

    public string GetPlainText(string key)
    {
        var value = WebUtility.HtmlDecode(GetString(key));
        value = HtmlTagPattern().Replace(value, " ");
        return WhitespacePattern().Replace(value, " ").Trim();
    }

    private IReadOnlyDictionary<int, SimpleStatusDefinition> LoadSimpleStatuses()
    {
        var definitions = new Dictionary<int, SimpleStatusDefinition>();

        foreach (var property in _classificationRoot.EnumerateObject())
        {
            var match = StatusSectionPattern().Match(property.Name);
            if (!match.Success
                || !int.TryParse(match.Groups["status"].Value, out var statusCode)
                || SpecializedStatusCodes.Contains(statusCode)
                || property.Value.ValueKind != JsonValueKind.Object
                || !property.Value.TryGetProperty("SessionType", out _))
            {
                continue;
            }

            definitions[statusCode] = new SimpleStatusDefinition(
                statusCode,
                property.Name,
                ParseClassification(property.Value, property.Name));
        }

        return definitions;
    }

    private static LegacyClassificationDefinition ParseClassification(
        JsonElement element,
        string path)
    {
        var authenticationConfidence = GetRequiredInt32(
            element,
            "SessionAuthenticationConfidenceLevel",
            path);
        var sessionTypeConfidence = GetRequiredInt32(
            element,
            "SessionTypeConfidenceLevel",
            path);
        var responseServerConfidence = GetRequiredInt32(
            element,
            "SessionResponseServerConfidenceLevel",
            path);
        var severityValue = GetRequiredInt32(element, "SessionSeverity", path);

        if (!Enum.IsDefined(typeof(TraceSeverity), severityValue))
        {
            throw new InvalidDataException(
                $"Legacy classification '{path}' contains unsupported severity {severityValue}.");
        }

        return new LegacyClassificationDefinition(
            GetOptionalString(element, "SectionTitle"),
            GetOptionalString(element, "SessionType"),
            GetOptionalString(element, "ResponseAlert"),
            new RuleClassification(
                authenticationConfidence,
                sessionTypeConfidence,
                responseServerConfidence,
                (TraceSeverity)severityValue));
    }

    private static int GetRequiredInt32(
        JsonElement element,
        string propertyName,
        string path)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var result))
        {
            throw new InvalidDataException(
                $"Legacy classification '{path}' is missing numeric '{propertyName}'.");
        }

        return result;
    }

    private static string? GetOptionalString(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyDictionary<string, string> LoadStrings()
    {
        var root = LoadJsonResource(StringsResource);
        return root
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value.GetString() ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static JsonElement LoadJsonResource(string resourceName)
    {
        var assembly = typeof(LegacyRulesetData).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded ruleset resource '{resourceName}' was not found.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    [GeneratedRegex(
        @"^HTTP_?(?<status>\d+)s?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex StatusSectionPattern();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}
