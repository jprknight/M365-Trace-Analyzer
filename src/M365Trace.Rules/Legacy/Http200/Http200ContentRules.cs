using System.Text.Json;

namespace M365Trace.Rules.Legacy.Http200;

public sealed class Http200JsonRule : Http200RuleBase
{
    public Http200JsonRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.Json";

    public override int Order => 1700;

    protected override bool Matches(AnalysisContext context) =>
        context.Facts.ContentType.Contains(
            "json",
            StringComparison.OrdinalIgnoreCase);

    public override void Evaluate(AnalysisContext context)
    {
        var body = context.Facts.ResponseText;
        if (string.IsNullOrEmpty(body))
        {
            ApplyByPrefix(
                context,
                "HTTP_200_Json_Empty",
                "HTTP_200_Json_EmptyResponseBody",
                "M365.HTTP.200.JsonEmpty",
                "The response Content-Type indicated JSON, but the body was empty.");
            return;
        }

        try
        {
            using var _ = JsonDocument.Parse(body);
            ApplyByPrefix(
                context,
                "HTTP_200_Json",
                "HTTP_200_Json",
                Id,
                "The response contained valid JSON.");
        }
        catch (JsonException)
        {
            ApplyByPrefix(
                context,
                "HTTP_200_Json_Invalid",
                "HTTP_200_Json_Invalid",
                "M365.HTTP.200.JsonInvalid",
                "The response Content-Type indicated JSON, but the body was invalid JSON.");
        }
    }
}

public sealed class Http200JavascriptRule : Http200RuleBase
{
    public Http200JavascriptRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.Javascript";

    public override int Order => 1800;

    protected override bool Matches(AnalysisContext context) =>
        context.Facts.ContentType.Contains(
            "javascript",
            StringComparison.OrdinalIgnoreCase);

    public override void Evaluate(AnalysisContext context) =>
        ApplyByPrefix(
            context,
            "HTTP_200_Javascript",
            "HTTP_200_Javascript",
            Id,
            "The response Content-Type identified JavaScript.");
}

public sealed class Http200LurkingErrorsRule : Http200RuleBase
{
    public Http200LurkingErrorsRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.LurkingErrors";

    public override int Order => 1900;

    protected override bool Matches(AnalysisContext context) =>
        CountWords(context) > 0;

    public override void Evaluate(AnalysisContext context)
    {
        var errorCount = context.Facts.CountResponseWord("error");
        var failedCount = context.Facts.CountResponseWord("failed");
        var exceptionCount = context.Facts.CountResponseWord("exception");
        var description =
            $"{Data.GetPlainText("HTTP_200_Lurking_Errors_ResponseCommentsStart")} "
            + $"Error: {errorCount}; Failed: {failedCount}; Exception: {exceptionCount}. "
            + Data.GetPlainText("HTTP_200_Lurking_Errors_ResponseCommentsEnd");

        Apply(
            context,
            "HTTP_200_Lurking_Errors",
            Id,
            Data.GetString("HTTP_200_Lurking_Errors_SessionType"),
            Data.GetPlainText("HTTP_200_Lurking_Errors_ResponseAlert"),
            description,
            "The successful response body contained one or more error-related words.");
    }

    private static int CountWords(AnalysisContext context) =>
        context.Facts.CountResponseWord("error")
        + context.Facts.CountResponseWord("failed")
        + context.Facts.CountResponseWord("exception");
}

public sealed class Http200FallbackRule : Http200RuleBase
{
    public Http200FallbackRule(LegacyRulesetData data) : base(data)
    {
    }

    public override string Id => "M365.HTTP.200.Ok";

    public override int Order => 10000;

    protected override bool Matches(AnalysisContext context) => true;

    public override void Evaluate(AnalysisContext context) =>
        Apply(
            context,
            "HTTP_200_Actually_OK",
            Id,
            Data.GetString("HTTP_200_Actually_OK SessionType"),
            Data.GetString("HTTP_200_Actually_OK ResponseAlert"),
            Data.GetString("HTTP_200_Actually_OK ResponseComments"),
            "The response was HTTP 200 and no recognized hidden failure was detected.");
}
