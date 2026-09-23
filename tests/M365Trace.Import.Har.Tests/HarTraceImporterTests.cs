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

    [Fact]
    public async Task ImportAsync_SeekableFileBeyondLimit_IsRejected()
    {
        await using var stream = new MemoryStream(new byte[11]);
        var importer = new HarTraceImporter(
            new HarImportOptions { MaximumFileSize = 10 });

        var exception = await Assert.ThrowsAsync<HarImportException>(
            () => importer.ImportAsync(stream));

        Assert.Contains("exceeds", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_NonSeekableFileBeyondLimit_IsRejected()
    {
        await using var inner = new MemoryStream(new byte[11]);
        await using var stream = new NonSeekableReadStream(inner);
        var importer = new HarTraceImporter(
            new HarImportOptions { MaximumFileSize = 10 });

        var exception = await Assert.ThrowsAsync<HarImportException>(
            () => importer.ImportAsync(stream));

        Assert.Contains("exceeds", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_EntryCountBeyondLimit_IsRejected()
    {
        const string json =
            """
            {
              "log": {
                "entries": [
                  {
                    "startedDateTime": "2026-09-23T10:00:00-04:00",
                    "time": 1,
                    "request": {
                      "method": "GET",
                      "url": "https://example.test/one",
                      "headers": []
                    },
                    "response": {
                      "status": 200,
                      "headers": [],
                      "content": {}
                    }
                  },
                  {
                    "startedDateTime": "2026-09-23T10:00:01-04:00",
                    "time": 1,
                    "request": {
                      "method": "GET",
                      "url": "https://example.test/two",
                      "headers": []
                    },
                    "response": {
                      "status": 200,
                      "headers": [],
                      "content": {}
                    }
                  }
                ]
              }
            }
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var importer = new HarTraceImporter(
            new HarImportOptions { MaximumEntryCount = 1 });

        var exception = await Assert.ThrowsAsync<HarImportException>(
            () => importer.ImportAsync(stream));

        Assert.Contains("more than 1 sessions", exception.Message);
    }

    [Fact]
    public async Task ImportAsync_InvalidBase64_IsRejected()
    {
        const string json =
            """
            {
              "log": {
                "entries": [{
                  "startedDateTime": "2026-09-23T10:00:00-04:00",
                  "time": 1,
                  "request": {
                    "method": "GET",
                    "url": "https://example.test/",
                    "headers": []
                  },
                  "response": {
                    "status": 200,
                    "headers": [],
                    "content": {
                      "mimeType": "text/plain",
                      "encoding": "base64",
                      "text": "not-valid-base64"
                    }
                  }
                }]
              }
            }
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var exception = await Assert.ThrowsAsync<HarImportException>(
            () => new HarTraceImporter().ImportAsync(stream));

        Assert.Contains("invalid data", exception.Message);
    }

    private static FileStream OpenTestData(string fileName) =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", fileName));

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableReadStream(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _inner.Read(buffer);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
