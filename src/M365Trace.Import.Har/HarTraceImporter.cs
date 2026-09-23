using System.Globalization;
using System.Text;
using System.Text.Json;
using M365Trace.Core;

namespace M365Trace.Import.Har;

public sealed class HarTraceImporter : ITraceImporter
{
    private readonly HarImportOptions _options;

    public HarTraceImporter()
        : this(new HarImportOptions())
    {
    }

    public HarTraceImporter(HarImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaximumFileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum file size must be positive.");
        }

        if (options.MaximumEntryCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum entry count must be positive.");
        }

        if (options.MaximumTextLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum text length must be positive.");
        }

        _options = options;
    }

    public string FormatName => "HTTP Archive";

    public bool CanImport(string fileName) =>
        string.Equals(Path.GetExtension(fileName), ".har", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<TraceSession>> ImportAsync(
        Stream stream,
        TraceImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("The HAR stream must be readable.", nameof(stream));
        }

        if (stream.CanSeek && stream.Length > _options.MaximumFileSize)
        {
            throw new HarImportException(
                $"The HAR file exceeds the {_options.MaximumFileSize / (1024 * 1024)} MB limit.");
        }

        try
        {
            using var boundedStream = new MaximumLengthStream(stream, _options.MaximumFileSize);
            using var document = await JsonDocument.ParseAsync(
                boundedStream,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 128
                },
                cancellationToken);

            return ParseDocument(document.RootElement);
        }
        catch (HarImportException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new HarImportException(
                $"The selected file is not valid HAR JSON. {exception.Message}",
                exception);
        }
        catch (IOException exception)
        {
            throw new HarImportException(exception.Message, exception);
        }
    }

    private IReadOnlyList<TraceSession> ParseDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("log", out var log)
            || log.ValueKind != JsonValueKind.Object)
        {
            throw new HarImportException("The HAR file must contain a top-level 'log' object.");
        }

        if (!log.TryGetProperty("entries", out var entries)
            || entries.ValueKind != JsonValueKind.Array)
        {
            throw new HarImportException("The HAR file must contain a 'log.entries' array.");
        }

        if (entries.GetArrayLength() > _options.MaximumEntryCount)
        {
            throw new HarImportException(
                $"The HAR file contains more than {_options.MaximumEntryCount:N0} sessions.");
        }

        var sessions = new List<TraceSession>(entries.GetArrayLength());
        var id = 1;

        foreach (var entry in entries.EnumerateArray())
        {
            sessions.Add(ParseEntry(entry, id));
            id++;
        }

        return sessions;
    }

    private TraceSession ParseEntry(JsonElement entry, int id)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            throw new HarImportException($"HAR entry {id} must be an object.");
        }

        var request = GetRequiredObject(entry, "request", id);
        var response = GetRequiredObject(entry, "response", id);
        var urlText = GetRequiredString(request, "url", id);

        if (!Uri.TryCreate(urlText, UriKind.Absolute, out var url))
        {
            throw new HarImportException($"HAR entry {id} contains an invalid request URL.");
        }

        return new TraceSession
        {
            Id = id,
            StartedAt = ParseStartedAt(entry, id),
            Method = GetRequiredString(request, "method", id),
            Url = url,
            StatusCode = GetRequiredInt32(response, "status", id),
            StatusText = GetOptionalString(response, "statusText"),
            Duration = TimeSpan.FromMilliseconds(Math.Max(0, GetOptionalDouble(entry, "time") ?? 0)),
            RequestHeaders = ParseHeaders(request, "headers"),
            ResponseHeaders = ParseHeaders(response, "headers"),
            RequestContent = ParseContent(request, "postData"),
            ResponseContent = ParseContent(response, "content")
        };
    }

    private TraceContent? ParseContent(JsonElement container, string propertyName)
    {
        if (!container.TryGetProperty(propertyName, out var content)
            || content.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var mimeType = GetOptionalString(content, "mimeType");
        var size = GetOptionalInt64(content, "size");
        var text = GetOptionalString(content, "text");
        var isBase64 = string.Equals(
            GetOptionalString(content, "encoding"),
            "base64",
            StringComparison.OrdinalIgnoreCase);

        if (text is null)
        {
            return new TraceContent(null, mimeType, size, isBase64, false);
        }

        if (isBase64)
        {
            try
            {
                var bytes = Convert.FromBase64String(text);
                size ??= bytes.LongLength;

                if (!IsTextContent(mimeType))
                {
                    var base64Data = IsPreviewableImage(mimeType)
                        && bytes.Length <= _options.MaximumTextLength
                            ? Convert.ToBase64String(bytes)
                            : null;

                    return new TraceContent(
                        null,
                        mimeType,
                        size,
                        true,
                        false,
                        base64Data);
                }

                text = Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException exception)
            {
                throw new HarImportException("A HAR body marked as Base64 contains invalid data.", exception);
            }
        }

        var isTruncated = text.Length > _options.MaximumTextLength;
        if (isTruncated)
        {
            text = text[.._options.MaximumTextLength];
        }

        return new TraceContent(text, mimeType, size, isBase64, isTruncated);
    }

    private static bool IsTextContent(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return true;
        }

        return mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || mimeType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || mimeType.Contains("xml", StringComparison.OrdinalIgnoreCase)
            || mimeType.Contains("javascript", StringComparison.OrdinalIgnoreCase)
            || mimeType.Contains("form-urlencoded", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPreviewableImage(string? mimeType) =>
        mimeType?.ToLowerInvariant() is
            "image/png"
            or "image/jpeg"
            or "image/gif"
            or "image/webp"
            or "image/bmp"
            or "image/x-icon"
            or "image/vnd.microsoft.icon"
            or "image/avif";

    private static IReadOnlyList<TraceHeader> ParseHeaders(JsonElement container, string propertyName)
    {
        if (!container.TryGetProperty(propertyName, out var headers)
            || headers.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<TraceHeader>();

        foreach (var header in headers.EnumerateArray())
        {
            if (header.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetOptionalString(header, "name");
            var value = GetOptionalString(header, "value");

            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(new TraceHeader(name, value ?? string.Empty));
            }
        }

        return result;
    }

    private static DateTimeOffset ParseStartedAt(JsonElement entry, int id)
    {
        var startedAtText = GetRequiredString(entry, "startedDateTime", id);

        if (!DateTimeOffset.TryParse(
                startedAtText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var startedAt))
        {
            throw new HarImportException($"HAR entry {id} contains an invalid startedDateTime value.");
        }

        return startedAt;
    }

    private static JsonElement GetRequiredObject(JsonElement element, string propertyName, int id)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Object)
        {
            throw new HarImportException($"HAR entry {id} must contain a '{propertyName}' object.");
        }

        return value;
    }

    private static string GetRequiredString(JsonElement element, string propertyName, int id)
    {
        var value = GetOptionalString(element, propertyName);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new HarImportException($"HAR entry {id} must contain a '{propertyName}' value.");
        }

        return value;
    }

    private static int GetRequiredInt32(JsonElement element, string propertyName, int id)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var result))
        {
            throw new HarImportException($"HAR entry {id} must contain a numeric '{propertyName}' value.");
        }

        return result;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static double? GetOptionalDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetDouble(out var result) ? result : null;
    }

    private static long? GetOptionalInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt64(out var result) ? result : null;
    }

    private sealed class MaximumLengthStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maximumLength;
        private long _bytesRead;

        public MaximumLengthStream(Stream inner, long maximumLength)
        {
            _inner = inner;
            _maximumLength = maximumLength;
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _bytesRead;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            TrackRead(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = _inner.Read(buffer);
            TrackRead(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = await _inner.ReadAsync(buffer, cancellationToken);
            TrackRead(read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
        }

        private void TrackRead(int read)
        {
            _bytesRead += read;
            if (_bytesRead > _maximumLength)
            {
                throw new IOException(
                    $"The HAR file exceeds the {_maximumLength / (1024 * 1024)} MB limit.");
            }
        }
    }
}
