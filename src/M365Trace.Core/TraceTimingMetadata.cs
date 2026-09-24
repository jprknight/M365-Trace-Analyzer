namespace M365Trace.Core;

public sealed record TraceTimingMetadata
{
    public TimeSpan? Queued { get; init; }

    public TimeSpan? Blocked { get; init; }

    public TimeSpan? Dns { get; init; }

    public TimeSpan? Connect { get; init; }

    public TimeSpan? Tls { get; init; }

    public TimeSpan? Send { get; init; }

    public TimeSpan? Wait { get; init; }

    public TimeSpan? Receive { get; init; }

    public TraceMetadataSource Source { get; init; }
}
