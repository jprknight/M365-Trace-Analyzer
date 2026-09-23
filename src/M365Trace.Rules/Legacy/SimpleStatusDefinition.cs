namespace M365Trace.Rules.Legacy;

public sealed record SimpleStatusDefinition(
    int StatusCode,
    string ClassificationPath,
    LegacyClassificationDefinition Definition);
