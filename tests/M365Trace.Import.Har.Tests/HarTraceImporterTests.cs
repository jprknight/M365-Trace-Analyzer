using System.Text;
using M365Trace.Import.Har;

namespace M365Trace.Import.Har.Tests;

public sealed class HarTraceImporterTests
{
    [Fact]
    public async Task ImportAsync_ValidHar_ReturnsNormalizedSession()
    {
        await using var stream = OpenTestData("minimal-valid.har");
        var importer = new HarTraceImporter();

        var sessions = await importer.ImportAsync(stream);

        var session = Assert.Single(sessions);
        Assert.Equal(1, session.Id);
        Assert.Equal("GET", session.Method);
        Assert.Equal("https://outlook.office.com/owa/", session.Url.AbsoluteUri);
        Assert.Equal(503, session.StatusCode);
        Assert.Equal("Service Unavailable", session.StatusText);
        Assert.Equal(TimeSpan.FromMilliseconds(1250.5), session.Duration);
        Assert.Contains(session.RequestHeaders, header => header.Name == "Accept");
        Assert.Contains("FederatedSTSUnreachable", session.ResponseContent?.Text);
    }

    [Fact]
    public async Task ImportAsync_Base64TextBody_DecodesContent()
    {
        await using var stream = OpenTestData("body-base64.har");
        var importer = new HarTraceImporter();

        var session = Assert.Single(await importer.ImportAsync(stream));

        Assert.True(session.ResponseContent?.IsBase64Encoded);
        Assert.Equal("{\"status\":\"ok\"}", session.ResponseContent?.Text);
    }

    [Fact]
    public async Task ImportAsync_Base64ImageBody_RetainsBoundedPreviewData()
    {
        const string imageData = "iVBORw0KGgo=";
        var json =
            $$"""
              {
                "log": {
                  "entries": [{
                    "startedDateTime": "2026-09-23T10:00:00.000-04:00",
                    "time": 1,
                    "request": {
                      "method": "GET",
                      "url": "https://example.test/image.png",
                      "headers": []
                    },
                    "response": {
                      "status": 200,
                      "statusText": "OK",
                      "headers": [],
                      "content": {
                        "mimeType": "image/png",
                        "encoding": "base64",
                        "text": "{{imageData}}"
                      }
                    }
                  }]
                }
              }
              """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var session = Assert.Single(await new HarTraceImporter().ImportAsync(stream));

        Assert.Null(session.ResponseContent?.Text);
        Assert.Equal(imageData, session.ResponseContent?.Base64Data);
    }

    [Fact]
    public async Task ImportAsync_MissingEntries_ThrowsUsefulError()
    {
        await using var stream = OpenTestData("invalid-missing-entries.har");
        var importer = new HarTraceImporter();

        var exception = await Assert.ThrowsAsync<HarImportException>(
            () => importer.ImportAsync(stream));

        Assert.Contains("log.entries", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_TextBeyondLimit_TruncatesBody()
    {
        var body = new string('x', 20);
        var json =
            $$"""
              {
                "log": {
                  "entries": [{
                    "startedDateTime": "2026-09-23T10:00:00.000-04:00",
                    "time": 1,
                    "request": {
                      "method": "GET",
                      "url": "https://example.test/",
                      "headers": []
                    },
                    "response": {
                      "status": 200,
                      "statusText": "OK",
                      "headers": [],
                      "content": {
                        "mimeType": "text/plain",
                        "text": "{{body}}"
                      }
                    }
                  }]
                }
              }
              """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var importer = new HarTraceImporter(new HarImportOptions { MaximumTextLength = 10 });

        var session = Assert.Single(await importer.ImportAsync(stream));

        Assert.True(session.ResponseContent?.IsTruncated);
        Assert.Equal(10, session.ResponseContent?.Text?.Length);
    }

    private static FileStream OpenTestData(string fileName) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", fileName));
}
