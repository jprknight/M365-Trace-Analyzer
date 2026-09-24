using M365Trace.Core;

namespace M365Trace.Core.Tests;

public sealed class TraceSessionMetadataTests
{
    [Fact]
    public void TraceSession_DefaultMetadataPreservesLegacyConstruction()
    {
        var session = CreateSession();

        Assert.Same(TraceSessionMetadata.Empty, session.Metadata);
        Assert.Null(session.Metadata.Source);
        Assert.Null(session.Metadata.Protocol);
        Assert.Null(session.Metadata.Timings);
        Assert.Null(session.Metadata.Completeness);
    }

    [Fact]
    public void Metadata_RepresentsOptionalDiagnosticGroupsWithProvenance()
    {
        var metadata = new TraceSessionMetadata
        {
            Source = new TraceSourceMetadata(
                TraceSourceFormat.Har,
                "entry-42",
                "page-1"),
            Protocol = new TraceProtocolMetadata(
                "HTTP/2",
                "HTTP/2",
                TraceMetadataSource.HarEntry),
            Sizes = new TraceSizeMetadata(
                new TraceMessageSize(350, 1024),
                new TraceMessageSize(420, 2048),
                TraceMetadataSource.HarEntry),
            Endpoints = new TraceEndpointMetadata(
                new TraceEndpoint("10.0.0.5", 52100),
                new TraceEndpoint("52.96.10.10", 443),
                TraceMetadataSource.HarEntry),
            Process = new TraceProcessMetadata(
                "OUTLOOK.EXE",
                1234,
                TraceMetadataSource.SazSessionFlag),
            Connection = new TraceConnectionMetadata(
                "connection-7",
                "socket-9",
                TraceMetadataSource.SazSessionFlag),
            Tls = new TraceTlsMetadata(
                "TLS 1.3",
                "TLS_AES_256_GCM_SHA384",
                "CN=outlook.office.com",
                "CN=Microsoft Azure RSA TLS Issuing CA",
                "ABC123",
                TraceMetadataSource.SazMetadata),
            Redirect = new TraceRedirectMetadata(
                new Uri("https://login.microsoftonline.com/"),
                TraceMetadataSource.HttpMessage),
            Cache = new TraceCacheMetadata(
                TraceCacheDisposition.Revalidated,
                "entry-17",
                TraceMetadataSource.HarEntry)
        };

        Assert.Equal(TraceSourceFormat.Har, metadata.Source.Format);
        Assert.Equal("entry-42", metadata.Source.SessionReference);
        Assert.Equal("HTTP/2", metadata.Protocol.RequestVersion);
        Assert.Equal(1024, metadata.Sizes.Request?.Body);
        Assert.Equal("52.96.10.10", metadata.Endpoints.Server?.Address);
        Assert.Equal("OUTLOOK.EXE", metadata.Process.Name);
        Assert.Equal("connection-7", metadata.Connection.ConnectionId);
        Assert.Equal("TLS 1.3", metadata.Tls.Protocol);
        Assert.Equal(
            "https://login.microsoftonline.com/",
            metadata.Redirect.Target.AbsoluteUri);
        Assert.Equal(
            TraceMetadataSource.HarEntry,
            metadata.Cache.Source);
    }

    [Fact]
    public void Timings_PreservePartialPhasesAndProvenance()
    {
        var timings = new TraceTimingMetadata
        {
            Blocked = TimeSpan.FromMilliseconds(5),
            Dns = TimeSpan.FromMilliseconds(10),
            Connect = TimeSpan.FromMilliseconds(20),
            Tls = TimeSpan.FromMilliseconds(15),
            Send = TimeSpan.FromMilliseconds(2),
            Wait = TimeSpan.FromMilliseconds(80),
            Receive = TimeSpan.FromMilliseconds(8),
            Source = TraceMetadataSource.HarTiming
        };

        Assert.Null(timings.Queued);
        Assert.Equal(TimeSpan.FromMilliseconds(5), timings.Blocked);
        Assert.Equal(TimeSpan.FromMilliseconds(15), timings.Tls);
        Assert.Equal(TimeSpan.FromMilliseconds(80), timings.Wait);
        Assert.Equal(TraceMetadataSource.HarTiming, timings.Source);
    }

    [Fact]
    public void Completeness_RepresentsPartialOrMissingMessages()
    {
        var completeness = new TraceSessionCompleteness(
            new TraceMessageCompleteness(
                TraceCaptureState.Complete,
                TraceContentAvailability.NotPresent),
            new TraceMessageCompleteness(
                TraceCaptureState.Missing,
                TraceContentAvailability.UnsupportedEncoding),
            TraceMetadataSource.SazMetadata);

        Assert.Equal(
            TraceCaptureState.Complete,
            completeness.Request.Capture);
        Assert.Equal(
            TraceContentAvailability.NotPresent,
            completeness.Request.Body);
        Assert.Equal(
            TraceCaptureState.Missing,
            completeness.Response.Capture);
        Assert.Equal(
            TraceContentAvailability.UnsupportedEncoding,
            completeness.Response.Body);
    }

    private static TraceSession CreateSession() =>
        new()
        {
            Id = 1,
            StartedAt = DateTimeOffset.UnixEpoch,
            Method = "GET",
            Url = new Uri("https://example.test/"),
            StatusCode = 200,
            Duration = TimeSpan.Zero
        };
}
