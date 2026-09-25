using System.IO.Compression;
using System.Text;
using M365Trace.Core;
using M365Trace.Import.Saz;
using SharpZipEntry = ICSharpCode.SharpZipLib.Zip.ZipEntry;
using SharpZipOutputStream = ICSharpCode.SharpZipLib.Zip.ZipOutputStream;

namespace M365Trace.Import.Saz.Tests;

public sealed class SazTraceImporterTests
{
    [Fact]
    public async Task ImportAsync_ValidSaz_ReturnsNormalizedSession()
    {
        await using var stream = CreateArchive(archive =>
        {
            AddEntry(
                archive,
                "raw/1_c.txt",
                "GET /owa/service.svc/CreateAttachment HTTP/1.1\r\n"
                + "Host: outlook.office.com\r\n"
                + "X-User-Identity: user@contoso.com\r\n"
                + "\r\n");
            AddEntry(
                archive,
                "raw/1_s.txt",
                "HTTP/1.1 503 Service Unavailable\r\n"
                + "Content-Type: text/plain; charset=utf-8\r\n"
                + "\r\n"
                + "FederatedSTSUnreachable");
            AddEntry(
                archive,
                "raw/1_m.xml",
                """
                <Session>
                  <SessionTimers
                    ClientBeginRequest="2026-09-23T10:00:00.0000000-04:00"
                    ClientDoneRequest="2026-09-23T10:00:00.0100000-04:00"
                    FiddlerBeginRequest="2026-09-23T10:00:00.0150000-04:00"
                    ServerGotRequest="2026-09-23T10:00:00.0200000-04:00"
                    ServerBeginResponse="2026-09-23T10:00:01.0000000-04:00"
                    ServerDoneResponse="2026-09-23T10:00:01.2000000-04:00"
                    ClientDoneResponse="2026-09-23T10:00:01.2500000-04:00"
                    DNSTime="10"
                    TCPConnectTime="20"
                    HTTPSHandshakeTime="30" />
                  <SessionFlags>
                    <SessionFlag N="x-HTTPS" V="true" />
                    <SessionFlag N="x-clientip" V="10.0.0.5" />
                    <SessionFlag N="x-clientport" V="50000" />
                    <SessionFlag N="x-hostIP" V="52.96.10.10:443" />
                    <SessionFlag N="x-processinfo" V="OUTLOOK.EXE:1234" />
                    <SessionFlag N="x-connectionid" V="connection-7" />
                    <SessionFlag N="x-socketid" V="socket-9" />
                    <SessionFlag N="x-tlsversion" V="TLS 1.3" />
                    <SessionFlag N="x-tlscipher" V="TLS_AES_256_GCM_SHA384" />
                    <SessionFlag N="x-servercertcn" V="CN=outlook.office.com" />
                  </SessionFlags>
                </Session>
                """);
        });

        var sessions = await new SazTraceImporter().ImportAsync(stream);

        var session = Assert.Single(sessions);
        Assert.Equal(1, session.Id);
        Assert.Equal("GET", session.Method);
        Assert.Equal(
            "https://outlook.office.com/owa/service.svc/CreateAttachment",
            session.Url.AbsoluteUri);
        Assert.Equal(503, session.StatusCode);
        Assert.Equal(TimeSpan.FromMilliseconds(1250), session.Duration);
        Assert.Equal("FederatedSTSUnreachable", session.ResponseContent?.Text?.Trim());
        Assert.Equal(TraceSourceFormat.Saz, session.Metadata.Source?.Format);
        Assert.Equal("1", session.Metadata.Source?.SessionReference);
        Assert.Equal("HTTP/1.1", session.Metadata.Protocol?.RequestVersion);
        Assert.Equal("HTTP/1.1", session.Metadata.Protocol?.ResponseVersion);
        Assert.Equal("10.0.0.5", session.Metadata.Endpoints?.Client?.Address);
        Assert.Equal(50000, session.Metadata.Endpoints?.Client?.Port);
        Assert.Equal("52.96.10.10", session.Metadata.Endpoints?.Server?.Address);
        Assert.Equal(443, session.Metadata.Endpoints?.Server?.Port);
        Assert.Equal("OUTLOOK.EXE", session.Metadata.Process?.Name);
        Assert.Equal(1234, session.Metadata.Process?.Id);
        Assert.Equal(
            "connection-7",
            session.Metadata.Connection?.ConnectionId);
        Assert.Equal("socket-9", session.Metadata.Connection?.SocketId);
        Assert.Equal("TLS 1.3", session.Metadata.Tls?.Protocol);
        Assert.Equal(
            "TLS_AES_256_GCM_SHA384",
            session.Metadata.Tls?.Cipher);
        Assert.Equal(
            TimeSpan.FromMilliseconds(10),
            session.Metadata.Timings?.Dns);
        Assert.Equal(
            TimeSpan.FromMilliseconds(20),
            session.Metadata.Timings?.Connect);
        Assert.Equal(
            TimeSpan.FromMilliseconds(30),
            session.Metadata.Timings?.Tls);
        Assert.Equal(
            TimeSpan.FromMilliseconds(5),
            session.Metadata.Timings?.Send);
        Assert.Equal(
            TimeSpan.FromMilliseconds(980),
            session.Metadata.Timings?.Wait);
        Assert.Equal(
            TimeSpan.FromMilliseconds(200),
            session.Metadata.Timings?.Receive);
        Assert.Equal(
            TraceCaptureState.Complete,
            session.Metadata.Completeness?.Response.Capture);
        Assert.Equal(
            TraceContentAvailability.Available,
            session.Metadata.Completeness?.Response.Body);
    }

    [Fact]
    public async Task ImportAsync_MultipleSessions_OrdersBySessionId()
    {
        await using var stream = CreateArchive(archive =>
        {
            AddBasicSession(archive, 10, "https://example.test/ten", 200);
            AddBasicSession(archive, 2, "https://example.test/two", 404);
        });

        var sessions = await new SazTraceImporter().ImportAsync(stream);

        Assert.Equal([2, 10], sessions.Select(session => session.Id));
    }

    [Fact]
    public async Task ImportAsync_MapiHttpDiagnostics_ExtractsTextFromBinaryBody()
    {
        await using var stream = CreateArchive(archive =>
        {
            AddEntry(
                archive,
                "raw/1_c.txt",
                "POST /mapi/emsmdb/?MailboxId=1 HTTP/1.1\r\n"
                + "Host: outlook.office365.com\r\n"
                + "\r\n");

            var headers = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n"
                + "Content-Type: application/mapi-http\r\n"
                + "\r\n");
            var prefix = new byte[] { 0, 0, 216, 7, 0, 0, 0, 0 };
            var diagnostic = Encoding.ASCII.GetBytes(
                "Microsoft.Exchange.RpcClientAccess.RpcServerException: "
                + "Connection blocked by Client Access Rules. "
                + "(StoreError=ProtocolDisabled)");
            AddBinaryEntry(
                archive,
                "raw/1_s.txt",
                [.. headers, .. prefix, .. diagnostic, 0, 0]);
        });

        var sessions = await new SazTraceImporter().ImportAsync(stream);

        var session = Assert.Single(sessions);
        Assert.Equal("application/mapi-http", session.ResponseContent?.MimeType);
        Assert.Contains(
            "Connection blocked by Client Access Rules",
            session.ResponseContent?.Text);
    }

    [Fact]
    public async Task ImportAsync_ImageBody_RetainsBoundedPreviewData()
    {
        var image = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        await using var stream = CreateArchive(archive =>
        {
            AddEntry(
                archive,
                "raw/1_c.txt",
                "GET /image.png HTTP/1.1\r\n"
                + "Host: example.test\r\n"
                + "\r\n");
            var headers = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n"
                + "Content-Type: image/png\r\n"
                + "\r\n");
            AddBinaryEntry(
                archive,
                "raw/1_s.txt",
                [.. headers, .. image]);
        });

        var session = Assert.Single(
            await new SazTraceImporter().ImportAsync(stream));

        Assert.Null(session.ResponseContent?.Text);
        Assert.Equal(
            Convert.ToBase64String(image),
            session.ResponseContent?.Base64Data);
    }

    [Fact]
    public async Task ImportAsync_ChunkedCompressedBody_DecodesInWireOrder()
    {
        var compressed = CompressGzip(
            Encoding.UTF8.GetBytes("{\"status\":\"ok\"}"));
        var chunked = EncodeChunked(compressed, 5);

        await using var stream = CreateArchive(archive =>
        {
            AddEntry(
                archive,
                "raw/1_c.txt",
                "GET /compressed HTTP/1.1\r\n"
                + "Host: example.test\r\n"
                + "\r\n");
            var headers = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n"
                + "Content-Type: application/json\r\n"
                + "Transfer-Encoding: chunked\r\n"
                + "Content-Encoding: gzip\r\n"
                + "\r\n");
            AddBinaryEntry(
                archive,
                "raw/1_s.txt",
                [.. headers, .. chunked]);
        });

        var session = Assert.Single(
            await new SazTraceImporter().ImportAsync(stream));

        Assert.Equal("{\"status\":\"ok\"}", session.ResponseContent?.Text);
        Assert.Equal(
            TraceContentAvailability.Available,
            session.Metadata.Completeness?.Response.Body);
    }

    [Theory]
    [InlineData("zstd", "encoded", TraceContentAvailability.UnsupportedEncoding)]
    [InlineData("gzip", "not-gzip", TraceContentAvailability.InvalidEncoding)]
    public async Task ImportAsync_ContentEncodingFailure_IsExplicit(
        string contentEncoding,
        string body,
        TraceContentAvailability expectedAvailability)
    {
        await using var stream = CreateArchive(archive =>
        {
            AddEntry(
                archive,
                "raw/1_c.txt",
                "GET /encoded HTTP/1.1\r\n"
                + "Host: example.test\r\n"
                + "\r\n");
            AddEntry(
                archive,
                "raw/1_s.txt",
                "HTTP/1.1 200 OK\r\n"
                + "Content-Type: text/plain\r\n"
                + $"Content-Encoding: {contentEncoding}\r\n"
                + "\r\n"
                + body);
        });

        var session = Assert.Single(
            await new SazTraceImporter().ImportAsync(stream));

        Assert.Null(session.ResponseContent?.Text);
        Assert.Equal(
            expectedAvailability,
            session.Metadata.Completeness?.Response.Body);
    }

    [Fact]
    public async Task ImportAsync_MalformedChunkedBody_IsExplicitlyInvalid()
    {
        await using var stream = CreateArchive(archive =>
        {
            AddEntry(
                archive,
                "raw/1_c.txt",
                "GET /chunked HTTP/1.1\r\n"
                + "Host: example.test\r\n"
                + "\r\n");
            AddEntry(
                archive,
                "raw/1_s.txt",
                "HTTP/1.1 200 OK\r\n"
                + "Content-Type: text/plain\r\n"
                + "Transfer-Encoding: chunked\r\n"
                + "\r\n"
                + "A\r\nshort\r\n0\r\n\r\n");
        });

        var session = Assert.Single(
            await new SazTraceImporter().ImportAsync(stream));

        Assert.Null(session.ResponseContent?.Text);
        Assert.Equal(
            TraceContentAvailability.InvalidEncoding,
            session.Metadata.Completeness?.Response.Body);
    }

    [Fact]
    public async Task ImportAsync_RequestOnlySession_PreservesMissingResponse()
    {
        await using var stream = CreateArchive(archive =>
            AddEntry(
                archive,
                "raw/1_c.txt",
                "POST /request-only HTTP/1.1\r\n"
                + "Host: example.test\r\n"
                + "Content-Type: text/plain\r\n"
                + "\r\n"
                + "request body"));

        var session = Assert.Single(
            await new SazTraceImporter().ImportAsync(stream));

        Assert.Equal(0, session.StatusCode);
        Assert.Equal("No response", session.StatusText);
        Assert.Equal("request body", session.RequestContent?.Text);
        Assert.Null(session.ResponseContent);
        Assert.Equal(
            TraceCaptureState.Missing,
            session.Metadata.Completeness?.Response.Capture);
        Assert.Equal(
            TraceContentAvailability.NotPresent,
            session.Metadata.Completeness?.Response.Body);
    }

    [Fact]
    public async Task ImportAsync_ConnectSession_PreservesProtocolAndHttpsTarget()
    {
        await using var stream = CreateArchive(archive =>
        {
            AddEntry(
                archive,
                "raw/1_c.txt",
                "CONNECT example.test:443 HTTP/1.1\r\n"
                + "Host: example.test:443\r\n"
                + "\r\n");
            AddEntry(
                archive,
                "raw/1_s.txt",
                "HTTP/1.1 200 Connection Established\r\n\r\n");
        });

        var session = Assert.Single(
            await new SazTraceImporter().ImportAsync(stream));

        Assert.Equal("CONNECT", session.Method);
        Assert.Equal("https://example.test/", session.Url.AbsoluteUri);
        Assert.Equal("HTTP/1.1", session.Metadata.Protocol?.RequestVersion);
        Assert.Equal("HTTP/1.1", session.Metadata.Protocol?.ResponseVersion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    [InlineData(256)]
    public async Task ImportAsync_EncryptedSaz_DecryptsSupportedEncryption(
        int aesKeySize)
    {
        const string password = "P@ss# word&<>\"'";
        await using var stream = CreateEncryptedArchive(password, aesKeySize);

        var session = Assert.Single(
            await new SazTraceImporter().ImportAsync(
                stream,
                new TraceImportOptions { Password = password }));

        Assert.Equal("http://example.test/encrypted", session.Url.AbsoluteUri);
        Assert.Equal("decrypted body", session.ResponseContent?.Text);
    }

    [Fact]
    public async Task ImportAsync_EncryptedSazWithoutPassword_RequestsPassword()
    {
        await using var stream = CreateEncryptedArchive("secret", 256);

        await Assert.ThrowsAsync<SazPasswordRequiredException>(
            () => new SazTraceImporter().ImportAsync(stream));
    }

    [Fact]
    public async Task ImportAsync_EncryptedSazWithWrongPassword_RejectsPassword()
    {
        await using var stream = CreateEncryptedArchive("secret", 256);

        await Assert.ThrowsAsync<SazInvalidPasswordException>(
            () => new SazTraceImporter().ImportAsync(
                stream,
                new TraceImportOptions { Password = "incorrect" }));
    }

    [Fact]
    public async Task ImportAsync_ArchiveWithoutRawSessions_ThrowsUsefulError()
    {
        await using var stream = CreateArchive(archive =>
            AddEntry(archive, "_index.htm", "<html></html>"));

        var exception = await Assert.ThrowsAsync<SazImportException>(
            () => new SazTraceImporter().ImportAsync(stream));

        Assert.Contains("does not contain", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_UnsafeEntryPath_IsRejected()
    {
        await using var stream = CreateArchive(archive =>
            AddEntry(archive, "raw/../1_c.txt", "GET / HTTP/1.1\r\nHost: example.test\r\n\r\n"));

        var exception = await Assert.ThrowsAsync<SazImportException>(
            () => new SazTraceImporter().ImportAsync(stream));

        Assert.Contains("unsafe path", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_EntryCountBeyondLimit_IsRejected()
    {
        await using var stream = CreateArchive(archive =>
        {
            AddEntry(archive, "raw/1_c.txt", "GET / HTTP/1.1\r\nHost: example.test\r\n\r\n");
            AddEntry(archive, "raw/1_s.txt", "HTTP/1.1 200 OK\r\n\r\n");
        });
        var importer = new SazTraceImporter(
            new SazImportOptions { MaximumEntryCount = 1 });

        var exception = await Assert.ThrowsAsync<SazImportException>(
            () => importer.ImportAsync(stream));

        Assert.Contains("more than 1 files", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_ExpandedArchiveBeyondLimit_IsRejected()
    {
        await using var stream = CreateArchive(archive =>
            AddEntry(
                archive,
                "raw/1_c.txt",
                "GET / HTTP/1.1\r\nHost: example.test\r\n\r\n"));
        var importer = new SazTraceImporter(
            new SazImportOptions { MaximumExpandedSize = 10 });

        var exception = await Assert.ThrowsAsync<SazImportException>(
            () => importer.ImportAsync(stream));

        Assert.Contains("expanded SAZ archive exceeds", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_SessionFileBeyondLimit_IsRejected()
    {
        await using var stream = CreateArchive(archive =>
            AddEntry(
                archive,
                "raw/1_c.txt",
                "GET / HTTP/1.1\r\nHost: example.test\r\n\r\n"));
        var importer = new SazTraceImporter(
            new SazImportOptions { MaximumSessionFileSize = 10 });

        var exception = await Assert.ThrowsAsync<SazImportException>(
            () => importer.ImportAsync(stream));

        Assert.Contains("per-file size limit", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_MalformedMetadata_IsRejected()
    {
        await using var stream = CreateArchive(archive =>
        {
            AddBasicSession(archive, 1, "https://example.test/", 200);
            AddEntry(archive, "raw/1_m.xml", "<Session>");
        });

        var exception = await Assert.ThrowsAsync<SazImportException>(
            () => new SazTraceImporter().ImportAsync(stream));

        Assert.Contains("metadata file contains invalid XML", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_CancelledImport_StopsProcessing()
    {
        await using var stream = CreateArchive(archive =>
            AddBasicSession(archive, 1, "https://example.test/", 200));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SazTraceImporter().ImportAsync(
                stream,
                cancellationToken: cancellation.Token));
    }

    private static MemoryStream CreateArchive(Action<ZipArchive> configure)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            configure(archive);
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateEncryptedArchive(
        string password,
        int aesKeySize)
    {
        var stream = new MemoryStream();
        using (var archive = new SharpZipOutputStream(stream)
        {
            IsStreamOwner = false,
            Password = password
        })
        {
            AddEncryptedEntry(
                archive,
                "raw/1_c.txt",
                "GET /encrypted HTTP/1.1\r\n"
                + "Host: example.test\r\n"
                + "\r\n",
                aesKeySize);
            AddEncryptedEntry(
                archive,
                "raw/1_s.txt",
                "HTTP/1.1 200 OK\r\n"
                + "Content-Type: text/plain; charset=utf-8\r\n"
                + "\r\n"
                + "decrypted body",
                aesKeySize);
            archive.Finish();
        }

        stream.Position = 0;
        return stream;
    }

    private static void AddEncryptedEntry(
        SharpZipOutputStream archive,
        string path,
        string content,
        int aesKeySize)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var entry = new SharpZipEntry(path)
        {
            AESKeySize = aesKeySize,
            DateTime = new DateTime(2026, 9, 23, 12, 0, 0),
            Size = bytes.Length
        };

        archive.PutNextEntry(entry);
        archive.Write(bytes);
        archive.CloseEntry();
    }

    private static void AddBasicSession(
        ZipArchive archive,
        int id,
        string url,
        int statusCode)
    {
        var uri = new Uri(url);
        AddEntry(
            archive,
            $"raw/{id}_c.txt",
            $"GET {uri.PathAndQuery} HTTP/1.1\r\nHost: {uri.Host}\r\n\r\n");
        AddEntry(
            archive,
            $"raw/{id}_s.txt",
            $"HTTP/1.1 {statusCode} Status\r\nContent-Type: text/plain\r\n\r\nbody");
    }

    private static void AddEntry(
        ZipArchive archive,
        string path,
        string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(
            entry.Open(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content.ReplaceLineEndings("\r\n"));
    }

    private static void AddBinaryEntry(
        ZipArchive archive,
        string path,
        byte[] content)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static byte[] CompressGzip(byte[] content)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(
            output,
            CompressionLevel.SmallestSize,
            leaveOpen: true))
        {
            gzip.Write(content);
        }

        return output.ToArray();
    }

    private static byte[] EncodeChunked(byte[] content, int chunkSize)
    {
        using var output = new MemoryStream();

        for (var offset = 0; offset < content.Length; offset += chunkSize)
        {
            var count = Math.Min(chunkSize, content.Length - offset);
            var prefix = Encoding.ASCII.GetBytes(
                $"{count:X}\r\n");
            output.Write(prefix);
            output.Write(content, offset, count);
            output.Write("\r\n"u8);
        }

        output.Write("0\r\n\r\n"u8);
        return output.ToArray();
    }
}
