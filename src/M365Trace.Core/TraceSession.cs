namespace M365Trace.Core;

public sealed record TraceSession
{
    public required int Id { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required string Method { get; init; }

    public required Uri Url { get; init; }

    public required int StatusCode { get; init; }

    public string? StatusText { get; init; }

    public required TimeSpan Duration { get; init; }

    public IReadOnlyList<TraceHeader> RequestHeaders { get; init; } = [];

    public IReadOnlyList<TraceHeader> ResponseHeaders { get; init; } = [];

    public TraceContent? RequestContent { get; init; }

    public TraceContent? ResponseContent { get; init; }

    public TraceSessionMetadata Metadata { get; init; } =
        TraceSessionMetadata.Empty;

    public TraceAnalysisResult Analysis { get; init; } = TraceAnalysisResult.Empty;
}
