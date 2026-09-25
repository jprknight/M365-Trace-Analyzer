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

        if (options.MaximumMetadataItemCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum metadata item count must be positive.");
        }

        _options = options;
    }

    public string FormatName => "HTTP Archive";

    public bool CanImport(string fileName) =>
        string.Equals(Path.GetExtension(fileName), ".har", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<TraceSession>> ImportAsync(
        Stream stream,
        TraceImportOptions? options = null,
        CancellationToken cancellationToken = default) =>
        (await ImportWithReportAsync(
            stream,
            options,
            cancellationToken)).Sessions;

    public async Task<TraceImportResult> ImportWithReportAsync(
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

    private TraceImportResult ParseDocument(JsonElement root)
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

        var pages = ParsePages(log);
        var sessions = new List<TraceSession>(entries.GetArrayLength());
        var issues = new List<TraceImportIssue>();
        var id = 1;

        foreach (var entry in entries.EnumerateArray())
        {
            try
            {
                sessions.Add(ParseEntry(entry, id, pages));
            }
            catch (HarImportException exception)
            {
                issues.Add(new TraceImportIssue(
                    TraceImportIssueCategory.SkippedSession,
                    $"HAR entry {id} was skipped: {exception.Message}",
                    id.ToString(CultureInfo.InvariantCulture)));
            }

            id++;
        }

        if (sessions.Count == 0)
        {
            var detail = issues.Count == 0
                ? string.Empty
                : $" {issues[0].Message}";
            throw new HarImportException(
                "The HAR file does not contain any usable sessions."
                + detail);
        }

        return TraceImportResult.Create(
            sessions,
            issues,
            entries.GetArrayLength());
    }

    private TraceSession ParseEntry(
        JsonElement entry,
        int id,
        IReadOnlyDictionary<string, TracePageMetadata> pages)
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

        var method = GetRequiredString(request, "method", id);
        var statusCode = GetRequiredInt32(response, "status", id);
        var requestContent = ParseContent(
            request,
            "postData",
            TraceContentAvailability.NotPresent);
        var responseContent = ParseContent(
            response,
            "content",
            ResponseCanHaveBody(method, statusCode)
                ? TraceContentAvailability.Unavailable
                : TraceContentAvailability.NotPresent);
        var pageReference = GetOptionalString(entry, "pageref");

        return new TraceSession
        {
            Id = id,
            StartedAt = ParseStartedAt(entry, id),
            Method = method,
            Url = url,
            StatusCode = statusCode,
            StatusText = GetOptionalString(response, "statusText"),
            Duration = TimeSpan.FromMilliseconds(Math.Max(0, GetOptionalDouble(entry, "time") ?? 0)),
            RequestHeaders = ParseHeaders(request, "headers"),
            ResponseHeaders = ParseHeaders(response, "headers"),
            RequestContent = requestContent.Content,
            ResponseContent = responseContent.Content,
            Metadata = new TraceSessionMetadata
            {
                Source = new TraceSourceMetadata(
                    TraceSourceFormat.Har,
                    id.ToString(CultureInfo.InvariantCulture),
                    pageReference),
                Protocol = ParseProtocol(request, response),
                Sizes = ParseSizes(request, response),
                Endpoints = ParseEndpoints(entry),
                Connection = ParseConnection(entry),
                Redirect = ParseRedirect(response),
                Cache = ParseCache(entry, response),
                Http = ParseHttp(request, response, id),
                Page = pageReference is not null
                    && pages.TryGetValue(pageReference, out var page)
                        ? page
                        : null,
                Timings = ParseTimings(entry),
                Completeness = new TraceSessionCompleteness(
                    new TraceMessageCompleteness(
                        GetCaptureState(request, "httpVersion"),
                        requestContent.Availability),
                    new TraceMessageCompleteness(
                        statusCode > 0
                            ? GetCaptureState(response, "httpVersion")
                            : TraceCaptureState.Partial,
                        responseContent.Availability),
                    TraceMetadataSource.HarEntry)
            }
        };
    }

    private ParsedContent ParseContent(
        JsonElement container,
        string propertyName,
        TraceContentAvailability missingAvailability)
    {
        if (!container.TryGetProperty(propertyName, out var content)
            || content.ValueKind != JsonValueKind.Object)
        {
            return new ParsedContent(null, missingAvailability);
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
            return new ParsedContent(
                new TraceContent(null, mimeType, size, isBase64, false),
                size == 0
                    ? TraceContentAvailability.NotPresent
                    : TraceContentAvailability.Unavailable);
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

                    return new ParsedContent(
                        new TraceContent(
                            null,
                            mimeType,
                            size,
                            true,
                            false,
                            base64Data),
                        base64Data is null
                            ? TraceContentAvailability.Unavailable
                            : TraceContentAvailability.Available);
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

        return new ParsedContent(
            new TraceContent(text, mimeType, size, isBase64, isTruncated),
            isTruncated
                ? TraceContentAvailability.Truncated
                : string.IsNullOrEmpty(text)
                    ? TraceContentAvailability.NotPresent
                    : TraceContentAvailability.Available);
    }

    private IReadOnlyDictionary<string, TracePageMetadata> ParsePages(
        JsonElement log)
    {
        if (!log.TryGetProperty("pages", out var pages)
            || pages.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, TracePageMetadata>(
                StringComparer.Ordinal);
        }

        EnsureMetadataItemCount(pages, "pages");
        var result = new Dictionary<string, TracePageMetadata>(
            StringComparer.Ordinal);

        foreach (var page in pages.EnumerateArray())
        {
            if (page.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var reference = GetOptionalString(page, "id");
            if (string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            var timings = TryGetObject(page, "pageTimings");
            result.TryAdd(
                reference,
                new TracePageMetadata(
                    reference,
                    GetOptionalString(page, "title"),
                    GetOptionalDateTimeOffset(page, "startedDateTime"),
                    timings is null
                        ? null
                        : GetOptionalDuration(
                            timings.Value,
                            "onContentLoad"),
                    timings is null
                        ? null
                        : GetOptionalDuration(timings.Value, "onLoad"),
                    TraceMetadataSource.HarPage));
        }

        return result;
    }

    private static TraceProtocolMetadata? ParseProtocol(
        JsonElement request,
        JsonElement response)
    {
        var requestVersion = GetOptionalString(request, "httpVersion");
        var responseVersion = GetOptionalString(response, "httpVersion");

        return requestVersion is null && responseVersion is null
            ? null
            : new TraceProtocolMetadata(
                requestVersion,
                responseVersion,
                TraceMetadataSource.HarEntry);
    }

    private static TraceSizeMetadata? ParseSizes(
        JsonElement request,
        JsonElement response)
    {
        var requestSize = ParseMessageSize(request);
        var responseSize = ParseMessageSize(response);

        return requestSize is null && responseSize is null
            ? null
            : new TraceSizeMetadata(
                requestSize,
                responseSize,
                TraceMetadataSource.HarEntry);
    }

    private static TraceMessageSize? ParseMessageSize(JsonElement message)
    {
        var headers = GetOptionalNonNegativeInt64(message, "headersSize");
        var body = GetOptionalNonNegativeInt64(message, "bodySize");
        return headers is null && body is null
            ? null
            : new TraceMessageSize(headers, body);
    }

    private static TraceEndpointMetadata? ParseEndpoints(JsonElement entry)
    {
        var serverAddress = GetOptionalString(entry, "serverIPAddress");
        return string.IsNullOrWhiteSpace(serverAddress)
            ? null
            : new TraceEndpointMetadata(
                null,
                new TraceEndpoint(serverAddress, null),
                TraceMetadataSource.HarEntry);
    }

    private static TraceConnectionMetadata? ParseConnection(JsonElement entry)
    {
        var connectionId = GetOptionalScalarString(entry, "connection");
        return string.IsNullOrWhiteSpace(connectionId)
            ? null
            : new TraceConnectionMetadata(
                connectionId,
                null,
                TraceMetadataSource.HarEntry);
    }

    private static TraceRedirectMetadata? ParseRedirect(JsonElement response)
    {
        var redirectUrl = GetOptionalString(response, "redirectURL");
        return Uri.TryCreate(redirectUrl, UriKind.Absolute, out var target)
            ? new TraceRedirectMetadata(
                target,
                TraceMetadataSource.HarEntry)
            : null;
    }

    private static TraceCacheMetadata? ParseCache(
        JsonElement entry,
        JsonElement response)
    {
        var cache = TryGetObject(entry, "cache");
        var beforeRequest = cache is null
            ? null
            : ParseCacheEntry(cache.Value, "beforeRequest");
        var afterRequest = cache is null
            ? null
            : ParseCacheEntry(cache.Value, "afterRequest");
        var disposition = ParseCacheDisposition(response);

        if (cache is null
            && beforeRequest is null
            && afterRequest is null
            && disposition == TraceCacheDisposition.Unknown)
        {
            return null;
        }

        return new TraceCacheMetadata(
            disposition,
            afterRequest?.ETag ?? beforeRequest?.ETag,
            TraceMetadataSource.HarEntry,
            beforeRequest,
            afterRequest);
    }

    private static TraceCacheEntryMetadata? ParseCacheEntry(
        JsonElement cache,
        string propertyName)
    {
        var entry = TryGetObject(cache, propertyName);
        if (entry is null)
        {
            return null;
        }

        return new TraceCacheEntryMetadata(
            GetOptionalDateTimeOffset(entry.Value, "expires"),
            GetOptionalDateTimeOffset(entry.Value, "lastAccess"),
            GetOptionalString(entry.Value, "eTag"),
            GetOptionalNonNegativeInt32(entry.Value, "hitCount"));
    }

    private static TraceCacheDisposition ParseCacheDisposition(
        JsonElement response)
    {
        if (GetOptionalBoolean(response, "_fromCache")
            ?? GetOptionalBoolean(response, "_servedFromCache")
            ?? false)
        {
            return TraceCacheDisposition.Hit;
        }

        if (response.TryGetProperty("_fromCache", out var fromCache)
            && fromCache.ValueKind == JsonValueKind.False)
        {
            return TraceCacheDisposition.Miss;
        }

        return GetOptionalInt32(response, "status") == 304
            ? TraceCacheDisposition.Revalidated
            : TraceCacheDisposition.Unknown;
    }

    private TraceHttpMetadata? ParseHttp(
        JsonElement request,
        JsonElement response,
        int id)
    {
        var queryEntries = ParseNameValues(
            request,
            "queryString",
            id);
        var requestCookies = ParseCookies(
            request,
            "cookies",
            id);
        var responseCookies = ParseCookies(
            response,
            "cookies",
            id);

        return queryEntries.Count == 0
            && requestCookies.Count == 0
            && responseCookies.Count == 0
                ? null
                : new TraceHttpMetadata(
                    queryEntries,
                    requestCookies,
                    responseCookies,
                    TraceMetadataSource.HarEntry);
    }

    private IReadOnlyList<TraceNameValue> ParseNameValues(
        JsonElement container,
        string propertyName,
        int id)
    {
        if (!container.TryGetProperty(propertyName, out var values)
            || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        EnsureMetadataItemCount(values, $"entry {id} {propertyName}");
        var result = new List<TraceNameValue>(values.GetArrayLength());

        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetOptionalString(value, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(new TraceNameValue(
                    name,
                    GetOptionalString(value, "value") ?? string.Empty));
            }
        }

        return result;
    }

    private IReadOnlyList<TraceCookieMetadata> ParseCookies(
        JsonElement container,
        string propertyName,
        int id)
    {
        if (!container.TryGetProperty(propertyName, out var cookies)
            || cookies.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        EnsureMetadataItemCount(cookies, $"entry {id} {propertyName}");
        var result = new List<TraceCookieMetadata>(
            cookies.GetArrayLength());

        foreach (var cookie in cookies.EnumerateArray())
        {
            if (cookie.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetOptionalString(cookie, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            result.Add(new TraceCookieMetadata(
                name,
                GetOptionalString(cookie, "value") ?? string.Empty,
                GetOptionalString(cookie, "path"),
                GetOptionalString(cookie, "domain"),
                GetOptionalDateTimeOffset(cookie, "expires"),
                GetOptionalBoolean(cookie, "httpOnly"),
                GetOptionalBoolean(cookie, "secure"),
                GetOptionalString(cookie, "sameSite")));
        }

        return result;
    }

    private static TraceTimingMetadata? ParseTimings(JsonElement entry)
    {
        var timings = TryGetObject(entry, "timings");
        if (timings is null)
        {
            return null;
        }

        var metadata = new TraceTimingMetadata
        {
            Queued = GetOptionalDuration(
                timings.Value,
                "_blocked_queueing")
                ?? GetOptionalDuration(timings.Value, "_queued"),
            Blocked = GetOptionalDuration(timings.Value, "blocked"),
            Dns = GetOptionalDuration(timings.Value, "dns"),
            Connect = GetOptionalDuration(timings.Value, "connect"),
            Tls = GetOptionalDuration(timings.Value, "ssl"),
            Send = GetOptionalDuration(timings.Value, "send"),
            Wait = GetOptionalDuration(timings.Value, "wait"),
            Receive = GetOptionalDuration(timings.Value, "receive"),
            Source = TraceMetadataSource.HarTiming
        };

        return metadata.Queued is null
            && metadata.Blocked is null
            && metadata.Dns is null
            && metadata.Connect is null
            && metadata.Tls is null
            && metadata.Send is null
            && metadata.Wait is null
            && metadata.Receive is null
                ? null
                : metadata;
    }

    private void EnsureMetadataItemCount(
        JsonElement array,
        string description)
    {
        if (array.GetArrayLength() > _options.MaximumMetadataItemCount)
        {
            throw new HarImportException(
                $"The HAR {description} contains more than "
                + $"{_options.MaximumMetadataItemCount:N0} metadata items.");
        }
    }

    private static TraceCaptureState GetCaptureState(
        JsonElement message,
        string protocolProperty)
    {
        var hasHeaders = message.TryGetProperty(
            "headers",
            out var headers)
            && headers.ValueKind == JsonValueKind.Array;
        var hasProtocol = !string.IsNullOrWhiteSpace(
            GetOptionalString(message, protocolProperty));

        return hasHeaders && hasProtocol
            ? TraceCaptureState.Complete
            : TraceCaptureState.Partial;
    }

    private static bool ResponseCanHaveBody(
        string method,
        int statusCode) =>
        !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase)
        && statusCode is not (>= 100 and < 200)
        && statusCode is not 204
        && statusCode is not 304;

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

    private static string? GetOptionalScalarString(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static JsonElement? TryGetObject(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    private static DateTimeOffset? GetOptionalDateTimeOffset(
        JsonElement element,
        string propertyName)
    {
        var value = GetOptionalString(element, propertyName);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var result)
                ? result
                : null;
    }

    private static bool? GetOptionalBoolean(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
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

    private static int? GetOptionalInt32(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetInt32(out var result) ? result : null;
    }

    private static int? GetOptionalNonNegativeInt32(
        JsonElement element,
        string propertyName)
    {
        var value = GetOptionalInt32(element, propertyName);
        return value >= 0 ? value : null;
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

    private static long? GetOptionalNonNegativeInt64(
        JsonElement element,
        string propertyName)
    {
        var value = GetOptionalInt64(element, propertyName);
        return value >= 0 ? value : null;
    }

    private static TimeSpan? GetOptionalDuration(
        JsonElement element,
        string propertyName)
    {
        var milliseconds = GetOptionalDouble(element, propertyName);
        return milliseconds is >= 0
            && double.IsFinite(milliseconds.Value)
                ? TimeSpan.FromMilliseconds(milliseconds.Value)
                : null;
    }

    private sealed record ParsedContent(
        TraceContent? Content,
        TraceContentAvailability Availability);

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
