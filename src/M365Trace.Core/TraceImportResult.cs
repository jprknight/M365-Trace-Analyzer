namespace M365Trace.Core;

public sealed record TraceImportResult
{
    public required IReadOnlyList<TraceSession> Sessions { get; init; }

    public required IReadOnlyList<TraceImportIssue> Issues { get; init; }

    public required TraceImportQuality Quality { get; init; }

    public static TraceImportResult Create(
        IReadOnlyList<TraceSession> sessions,
        IReadOnlyList<TraceImportIssue>? issues = null,
        int? sourceSessionCount = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        var importIssues = issues?.ToList() ?? [];
        var completeSessions = 0;
        var partialSessions = 0;
        var missingResponses = 0;
        var truncatedBodies = 0;

        foreach (var session in sessions)
        {
            var completeness = session.Metadata.Completeness;
            if (completeness is null)
            {
                continue;
            }

            var sessionReference = session.Metadata.Source?.SessionReference
                ?? session.Id.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            var isComplete = IsComplete(completeness.Request)
                && IsComplete(completeness.Response);

            if (isComplete)
            {
                completeSessions++;
            }
            else
            {
                partialSessions++;
                importIssues.Add(new TraceImportIssue(
                    TraceImportIssueCategory.PartialSession,
                    $"Session {sessionReference} is incomplete.",
                    sessionReference));
            }

            if (completeness.Response.Capture == TraceCaptureState.Missing)
            {
                missingResponses++;
            }

            if (completeness.Request.Body == TraceContentAvailability.Truncated)
            {
                truncatedBodies++;
                importIssues.Add(new TraceImportIssue(
                    TraceImportIssueCategory.TruncatedContent,
                    $"Session {sessionReference} has a truncated request body.",
                    sessionReference));
            }

            if (completeness.Response.Body == TraceContentAvailability.Truncated)
            {
                truncatedBodies++;
                importIssues.Add(new TraceImportIssue(
                    TraceImportIssueCategory.TruncatedContent,
                    $"Session {sessionReference} has a truncated response body.",
                    sessionReference));
            }

            AddContentIssue(
                importIssues,
                completeness.Request.Body,
                sessionReference,
                "request");
            AddContentIssue(
                importIssues,
                completeness.Response.Body,
                sessionReference,
                "response");
        }

        var totalSourceSessions = Math.Max(
            sessions.Count,
            sourceSessionCount ?? sessions.Count);

        return new TraceImportResult
        {
            Sessions = sessions,
            Issues = importIssues,
            Quality = new TraceImportQuality(
                totalSourceSessions,
                sessions.Count,
                completeSessions,
                partialSessions,
                missingResponses,
                truncatedBodies,
                totalSourceSessions - sessions.Count,
                importIssues.Count(issue =>
                    issue.Category
                    == TraceImportIssueCategory.UnsupportedFeature))
        };
    }

    private static bool IsComplete(TraceMessageCompleteness message) =>
        message.Capture == TraceCaptureState.Complete
        && message.Body is
            TraceContentAvailability.Available
            or TraceContentAvailability.NotPresent;

    private static void AddContentIssue(
        ICollection<TraceImportIssue> issues,
        TraceContentAvailability availability,
        string sessionReference,
        string messageName)
    {
        var category = availability switch
        {
            TraceContentAvailability.UnsupportedEncoding =>
                TraceImportIssueCategory.UnsupportedFeature,
            TraceContentAvailability.InvalidEncoding =>
                TraceImportIssueCategory.InvalidContent,
            _ => (TraceImportIssueCategory?)null
        };
        if (category is null)
        {
            return;
        }

        issues.Add(new TraceImportIssue(
            category.Value,
            $"Session {sessionReference} has "
            + $"{availability.ToString().ToLowerInvariant()} "
            + $"{messageName} content.",
            sessionReference));
    }
}

public sealed record TraceImportQuality(
    int SourceSessions,
    int ImportedSessions,
    int CompleteSessions,
    int PartialSessions,
    int MissingResponses,
    int TruncatedBodies,
    int SkippedSessions,
    int UnsupportedFeatures)
{
    public bool HasWarnings =>
        PartialSessions > 0
        || MissingResponses > 0
        || TruncatedBodies > 0
        || SkippedSessions > 0
        || UnsupportedFeatures > 0;
}

public sealed record TraceImportIssue(
    TraceImportIssueCategory Category,
    string Message,
    string? SessionReference = null);

public enum TraceImportIssueCategory
{
    SkippedSession,
    PartialSession,
    TruncatedContent,
    UnsupportedFeature,
    InvalidContent
}
