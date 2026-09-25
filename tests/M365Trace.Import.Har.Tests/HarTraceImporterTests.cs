using System.Text;
using M365Trace.Core;
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
        Assert.Equal(TraceSourceFormat.Har, session.Metadata.Source?.Format);
        Assert.Equal("1", session.Metadata.Source?.SessionReference);
        Assert.Equal(
            TraceCaptureState.Partial,
            session.Metadata.Completeness?.Request.Capture);
        Assert.Equal(
            TraceContentAvailability.NotPresent,
            session.Metadata.Completeness?.Request.Body);
        Assert.Equal(
            TraceContentAvailability.Available,
            session.Metadata.Completeness?.Response.Body);
    }

    [Fact]
    public async Task ImportAsync_FullHarMetadata_MapsNormalizedGroups()
    {
        await using var stream = OpenTestData("full-metadata.har");

        var session = Assert.Single(
            await new HarTraceImporter().ImportAsync(stream));
        var metadata = session.Metadata;

        Assert.Equal("page-1", metadata.Source?.PageReference);
        Assert.Equal("HTTP/2", metadata.Protocol?.RequestVersion);
        Assert.Equal("HTTP/2", metadata.Protocol?.ResponseVersion);
        Assert.Equal(120, metadata.Sizes?.Request?.Headers);
        Assert.Equal(12, metadata.Sizes?.Request?.Body);
        Assert.Equal(180, metadata.Sizes?.Response?.Headers);
        Assert.Equal(11, metadata.Sizes?.Response?.Body);
        Assert.Equal("52.96.10.10", metadata.Endpoints?.Server?.Address);
        Assert.Equal("42", metadata.Connection?.ConnectionId);
        Assert.Equal(
            "https://login.microsoftonline.com/",
            metadata.Redirect?.Target.AbsoluteUri);
        Assert.Equal(TraceCacheDisposition.Miss, metadata.Cache?.Disposition);
        Assert.Equal("\"after\"", metadata.Cache?.EntryReference);
        Assert.Equal(2, metadata.Cache?.BeforeRequest?.HitCount);
        Assert.Equal(3, metadata.Cache?.AfterRequest?.HitCount);
        Assert.Equal("view", metadata.Http?.QueryEntries[0].Name);
        Assert.Equal("inbox", metadata.Http?.QueryEntries[0].Value);
        Assert.Equal(
            "ClientId",
            metadata.Http?.RequestCookies[0].Name);
        Assert.True(metadata.Http?.RequestCookies[0].HttpOnly);
        Assert.Equal(
            "SessionId",
            metadata.Http?.ResponseCookies[0].Name);
        Assert.Equal("page-1", metadata.Page?.Reference);
        Assert.Equal("Microsoft 365 inbox", metadata.Page?.Title);
        Assert.Equal(
            TimeSpan.FromMilliseconds(250.5),
            metadata.Page?.DomContentLoaded);
        Assert.Equal(
            TimeSpan.FromMilliseconds(800.25),
            metadata.Page?.Load);
        Assert.Equal(
            TimeSpan.FromMilliseconds(2),
            metadata.Timings?.Queued);
        Assert.Equal(
            TimeSpan.FromMilliseconds(12),
            metadata.Timings?.Tls);
        Assert.Equal(
            TimeSpan.FromMilliseconds(40),
            metadata.Timings?.Wait);
        Assert.Equal(
            TraceCaptureState.Complete,
            metadata.Completeness?.Request.Capture);
        Assert.Equal(
            TraceCaptureState.Complete,
            metadata.Completeness?.Response.Capture);
        Assert.Equal(
            TraceContentAvailability.Available,
            metadata.Completeness?.Request.Body);
        Assert.Equal(
            TraceContentAvailability.Available,
            metadata.Completeness?.Response.Body);
    }

    [Fact]
    public async Task ImportAsync_Base64TextBody_DecodesContent()
    {
        await using var stream = OpenTestData("body-base64.har");
        var importer = new HarTraceImporter();

        var session = Assert.Single(await importer.ImportAsync(stream));

        Assert.True(session.ResponseContent?.IsBase64Encoded);
        Assert.Equal("{\"status\":\"ok\"}", session.ResponseContent?.Text);
        Assert.Equal(
            TraceContentAvailability.Available,
            session.Metadata.Completeness?.Response.Body);
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
        Assert.Equal(
            TraceContentAvailability.Truncated,
            session.Metadata.Completeness?.Response.Body);
    }

    [Fact]
    public async Task ImportAsync_MissingOptionalResponseBody_IsExplicitlyUnavailable()
    {
        const string json =
            """
            {
              "log": {
                "entries": [{
                  "startedDateTime": "2026-09-25T10:00:00-04:00",
                  "time": 1,
                  "request": {
                    "method": "GET",
                    "url": "https://example.test/",
                    "httpVersion": "HTTP/1.1",
                    "headers": []
                  },
                  "response": {
                    "status": 200,
                    "statusText": "OK",
                    "httpVersion": "HTTP/1.1",
                    "headers": []
                  }
                }]
              }
            }
            """;
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(json));

        var session = Assert.Single(
            await new HarTraceImporter().ImportAsync(stream));

        Assert.Null(session.ResponseContent);
        Assert.Equal(
            TraceContentAvailability.Unavailable,
            session.Metadata.Completeness?.Response.Body);
    }

    [Fact]
    public async Task ImportAsync_MalformedOptionalMetadata_IsIgnored()
    {
        const string json =
            """
            {
              "log": {
                "pages": [{
                  "id": "page-1",
                  "startedDateTime": "not-a-date",
                  "pageTimings": {
                    "onContentLoad": -1,
                    "onLoad": "invalid"
                  }
                }],
                "entries": [{
                  "pageref": "page-1",
                  "startedDateTime": "2026-09-25T10:00:00-04:00",
                  "time": 1,
                  "serverIPAddress": 123,
                  "request": {
                    "method": "GET",
                    "url": "https://example.test/",
                    "httpVersion": 2,
                    "headers": [],
                    "headersSize": -1,
                    "bodySize": -1,
                    "queryString": [
                      null,
                      { "name": "", "value": "ignored" },
                      { "name": "valid", "value": "retained" }
                    ],
                    "cookies": [{
                      "name": "cookie",
                      "value": "value",
                      "expires": "not-a-date"
                    }]
                  },
                  "response": {
                    "status": 200,
                    "headers": [],
                    "redirectURL": "not-a-url",
                    "content": {
                      "size": 0,
                      "text": ""
                    }
                  },
                  "timings": {
                    "blocked": -1,
                    "dns": -1,
                    "connect": -1,
                    "ssl": -1,
                    "send": -1,
                    "wait": -1,
                    "receive": -1
                  }
                }]
              }
            }
            """;
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(json));

        var session = Assert.Single(
            await new HarTraceImporter().ImportAsync(stream));

        Assert.Null(session.Metadata.Protocol);
        Assert.Null(session.Metadata.Sizes);
        Assert.Null(session.Metadata.Endpoints);
        Assert.Null(session.Metadata.Redirect);
        Assert.Null(session.Metadata.Timings);
        Assert.Null(session.Metadata.Page?.StartedAt);
        Assert.Null(session.Metadata.Page?.Load);
        Assert.Equal("valid", session.Metadata.Http?.QueryEntries[0].Name);
        Assert.Null(session.Metadata.Http?.RequestCookies[0].Expires);
    }

    [Fact]
    public async Task ImportAsync_MetadataItemsBeyondLimit_AreRejected()
    {
        const string json =
            """
            {
              "log": {
                "entries": [{
                  "startedDateTime": "2026-09-25T10:00:00-04:00",
                  "time": 1,
                  "request": {
                    "method": "GET",
                    "url": "https://example.test/",
                    "headers": [],
                    "queryString": [
                      { "name": "one", "value": "1" },
                      { "name": "two", "value": "2" }
                    ]
                  },
                  "response": {
                    "status": 200,
                    "headers": [],
                    "content": {}
                  }
                }]
              }
            }
            """;
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(json));
        var importer = new HarTraceImporter(
            new HarImportOptions
            {
                MaximumMetadataItemCount = 1
            });

        var exception = await Assert.ThrowsAsync<HarImportException>(
            () => importer.ImportAsync(stream));

        Assert.Contains("more than 1 metadata items", exception.Message);
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

    [Fact]
    public async Task ImportWithReportAsync_MalformedEntry_SkipsOnlyThatEntry()
    {
        const string json =
            """
            {
              "log": {
                "entries": [
                  {
                    "startedDateTime": "2026-09-25T10:00:00-04:00",
                    "time": 1,
                    "request": {
                      "method": "GET",
                      "url": "https://example.test/valid",
                      "httpVersion": "HTTP/1.1",
                      "headers": []
                    },
                    "response": {
                      "status": 200,
                      "httpVersion": "HTTP/1.1",
                      "headers": [],
                      "content": {
                        "size": 2,
                        "mimeType": "text/plain",
                        "text": "ok"
                      }
                    }
                  },
                  {
                    "startedDateTime": "2026-09-25T10:00:01-04:00",
                    "time": 1,
                    "response": {
                      "status": 500,
                      "headers": [],
                      "content": {}
                    }
                  }
                ]
              }
            }
            """;
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(json));

        var result = await new HarTraceImporter()
            .ImportWithReportAsync(stream);

        var session = Assert.Single(result.Sessions);
        Assert.Equal("https://example.test/valid", session.Url.AbsoluteUri);
        Assert.Equal(2, result.Quality.SourceSessions);
        Assert.Equal(1, result.Quality.ImportedSessions);
        Assert.Equal(1, result.Quality.SkippedSessions);
        Assert.Contains(
            result.Issues,
            issue =>
                issue.Category == TraceImportIssueCategory.SkippedSession
                && issue.SessionReference == "2"
                && issue.Message.Contains(
                    "request",
                    StringComparison.OrdinalIgnoreCase));
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
