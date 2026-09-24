namespace M365Trace.Core;

public sealed record TraceSessionMetadata
{
    public static TraceSessionMetadata Empty { get; } = new();

    public TraceSourceMetadata? Source { get; init; }

    public TraceProtocolMetadata? Protocol { get; init; }

    public TraceSizeMetadata? Sizes { get; init; }

    public TraceEndpointMetadata? Endpoints { get; init; }

    public TraceProcessMetadata? Process { get; init; }

    public TraceConnectionMetadata? Connection { get; init; }

    public TraceTlsMetadata? Tls { get; init; }

    public TraceRedirectMetadata? Redirect { get; init; }

    public TraceCacheMetadata? Cache { get; init; }

    public TraceTimingMetadata? Timings { get; init; }

    public TraceSessionCompleteness? Completeness { get; init; }
}

public sealed record TraceSourceMetadata(
    TraceSourceFormat Format,
    string? SessionReference = null,
    string? PageReference = null);

public sealed record TraceProtocolMetadata(
    string? RequestVersion,
    string? ResponseVersion,
    TraceMetadataSource Source);

public sealed record TraceMessageSize(
    long? Headers,
    long? Body);

public sealed record TraceSizeMetadata(
    TraceMessageSize? Request,
    TraceMessageSize? Response,
    TraceMetadataSource Source);

public sealed record TraceEndpoint(
    string? Address,
    int? Port);

public sealed record TraceEndpointMetadata(
    TraceEndpoint? Client,
    TraceEndpoint? Server,
    TraceMetadataSource Source);

public sealed record TraceProcessMetadata(
    string? Name,
    int? Id,
    TraceMetadataSource Source);

public sealed record TraceConnectionMetadata(
    string? ConnectionId,
    string? SocketId,
    TraceMetadataSource Source);

public sealed record TraceTlsMetadata(
    string? Protocol,
    string? Cipher,
    string? CertificateSubject,
    string? CertificateIssuer,
    string? CertificateThumbprint,
    TraceMetadataSource Source);

public sealed record TraceRedirectMetadata(
    Uri Target,
    TraceMetadataSource Source);

public sealed record TraceCacheMetadata(
    TraceCacheDisposition Disposition,
    string? EntryReference,
    TraceMetadataSource Source);

public sealed record TraceMessageCompleteness(
    TraceCaptureState Capture,
    TraceContentAvailability Body);

public sealed record TraceSessionCompleteness(
    TraceMessageCompleteness Request,
    TraceMessageCompleteness Response,
    TraceMetadataSource Source);
