using System.IO.Compression;
using System.Text;
using M365Trace.Import.Saz;

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
                    ClientDoneResponse="2026-09-23T10:00:01.2500000-04:00" />
                  <SessionFlags>
                    <SessionFlag N="x-HTTPS" V="true" />
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
}
