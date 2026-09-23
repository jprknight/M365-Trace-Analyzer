using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip;
using M365Trace.Core;
using SharpZipFile = ICSharpCode.SharpZipLib.Zip.ZipFile;

namespace M365Trace.Import.Saz;

public sealed partial class SazTraceImporter : ITraceImporter
{
    private readonly SazImportOptions _options;

    public SazTraceImporter()
        : this(new SazImportOptions())
    {
    }

    public SazTraceImporter(SazImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaximumFileSize <= 0
            || options.MaximumEntryCount <= 0
            || options.MaximumExpandedSize <= 0
            || options.MaximumSessionFileSize <= 0
            || options.MaximumTextLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "All SAZ import limits must be positive.");
        }

        _options = options;
    }

    public string FormatName => "Fiddler Session Archive";

    public bool CanImport(string fileName) =>
        string.Equals(Path.GetExtension(fileName), ".saz", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<TraceSession>> ImportAsync(
        Stream stream,
        TraceImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("The SAZ stream must be readable.", nameof(stream));
        }

        if (stream.CanSeek && stream.Length > _options.MaximumFileSize)
        {
            throw new SazImportException(
                $"The SAZ file exceeds the {_options.MaximumFileSize / (1024 * 1024)} MB limit.");
        }

        var temporaryPath = Path.Combine(
            Path.GetTempPath(),
            $"m365trace-{Guid.NewGuid():N}.saz");

        try
        {
            await using (var temporaryFile = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await CopyWithLimitAsync(
                    stream,
                    temporaryFile,
                    _options.MaximumFileSize,
                    cancellationToken);

                temporaryFile.Position = 0;

                try
                {
                    using var archive = new SharpZipFile(
                        temporaryFile,
                        leaveOpen: true);
                    var password = options?.Password;
                    if (!string.IsNullOrEmpty(password))
                    {
                        archive.Password = password;
                    }

                    return await ParseArchiveAsync(
                        archive,
                        !string.IsNullOrEmpty(password),
                        cancellationToken);
                }
                catch (SharpZipBaseException exception)
                {
                    throw new SazImportException(
                        "The selected file is not a supported SAZ archive.",
                        exception);
                }
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private async Task<IReadOnlyList<TraceSession>> ParseArchiveAsync(
        SharpZipFile archive,
        bool hasPassword,
        CancellationToken cancellationToken)
    {
        if (archive.Count > _options.MaximumEntryCount)
        {
            throw new SazImportException(
                $"The SAZ archive contains more than {_options.MaximumEntryCount:N0} files.");
        }

        var entries = archive.Cast<ZipEntry>().ToArray();
        if (!hasPassword && entries.Any(entry => entry.IsCrypted))
        {
            throw new SazPasswordRequiredException();
        }

        long expandedSize = 0;
        var sessionFiles = new Dictionary<int, SessionFiles>();

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateEntryPath(entry.Name);

            if (!entry.CanDecompress)
            {
                throw new SazImportException(
                    $"SAZ entry '{entry.Name}' uses an unsupported encryption or compression method.");
            }

            if (entry.Size < 0)
            {
                throw new SazImportException(
                    $"SAZ entry '{entry.Name}' does not declare its expanded size.");
            }

            expandedSize = checked(expandedSize + entry.Size);
            if (expandedSize > _options.MaximumExpandedSize)
            {
                throw new SazImportException(
                    $"The expanded SAZ archive exceeds the {_options.MaximumExpandedSize / (1024 * 1024)} MB limit.");
            }

            if (entry.Size > _options.MaximumSessionFileSize)
            {
                throw new SazImportException(
                    $"SAZ entry '{entry.Name}' exceeds the per-file size limit.");
            }

            var match = SessionFilePattern().Match(NormalizeEntryPath(entry.Name));
            if (!match.Success
                || !int.TryParse(
                    match.Groups["id"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var sessionId))
            {
                continue;
            }

            if (!sessionFiles.TryGetValue(sessionId, out var files))
            {
                files = new SessionFiles();
                sessionFiles.Add(sessionId, files);
            }

            switch (match.Groups["kind"].Value.ToLowerInvariant())
            {
                case "c":
                    files.Request = entry;
                    break;
                case "s":
                    files.Response = entry;
                    break;
                case "m":
                    files.Metadata = entry;
                    break;
            }
        }

        var sessions = new List<TraceSession>();

        foreach (var pair in sessionFiles.OrderBy(pair => pair.Key))
        {
            if (pair.Value.Request is null)
            {
                continue;
            }

            sessions.Add(await ParseSessionAsync(
                archive,
                pair.Key,
                pair.Value,
                cancellationToken));
        }

        if (sessions.Count == 0)
        {
            throw new SazImportException(
                "The SAZ archive does not contain any supported raw HTTP sessions.");
        }

        return sessions;
    }

    private async Task<TraceSession> ParseSessionAsync(
        SharpZipFile archive,
        int sessionId,
        SessionFiles files,
        CancellationToken cancellationToken)
    {
        var metadata = files.Metadata is null
            ? SazMetadata.Empty
            : ParseMetadata(await ReadEntryBytesAsync(
                archive,
                files.Metadata,
                cancellationToken));

        var request = ParseRequest(
            await ReadEntryBytesAsync(
                archive,
                files.Request!,
                cancellationToken),
            metadata);

        var response = files.Response is null
            ? ParsedResponse.Empty
            : ParseResponse(await ReadEntryBytesAsync(
                archive,
                files.Response,
                cancellationToken));

        return new TraceSession
        {
            Id = sessionId,
            StartedAt = metadata.StartedAt
                ?? new DateTimeOffset(files.Request!.DateTime),
            Method = request.Method,
            Url = request.Url,
            StatusCode = response.StatusCode,
            StatusText = response.StatusText,
            Duration = metadata.Duration ?? TimeSpan.Zero,
            RequestHeaders = request.Headers,
            ResponseHeaders = response.Headers,
            RequestContent = CreateContent(request.Body, request.Headers),
            ResponseContent = CreateContent(response.Body, response.Headers)
        };
    }

    private ParsedRequest ParseRequest(byte[] bytes, SazMetadata metadata)
    {
        var message = ParseHttpMessage(bytes);
        var firstLineParts = message.StartLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

        if (firstLineParts.Length < 2)
        {
            throw new SazImportException("A SAZ request contains an invalid HTTP request line.");
        }

        var method = firstLineParts[0];
        var target = firstLineParts[1];
        var url = BuildRequestUri(method, target, message.Headers, metadata.Flags);

        return new ParsedRequest(method, url, message.Headers, message.Body);
    }

    private static ParsedResponse ParseResponse(byte[] bytes)
    {
        var message = ParseHttpMessage(bytes);
        var firstLineParts = message.StartLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

        if (firstLineParts.Length < 2
            || !int.TryParse(
                firstLineParts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var statusCode))
        {
            throw new SazImportException("A SAZ response contains an invalid HTTP status line.");
        }

        return new ParsedResponse(
            statusCode,
            firstLineParts.Length == 3 ? firstLineParts[2] : null,
            message.Headers,
            message.Body);
    }

    private static ParsedHttpMessage ParseHttpMessage(byte[] bytes)
    {
        var headerEnd = FindHeaderEnd(bytes, out var delimiterLength);
        if (headerEnd < 0)
        {
            throw new SazImportException(
                "A SAZ raw session file does not contain a complete HTTP header block.");
        }

        var headerText = Encoding.Latin1.GetString(bytes, 0, headerEnd);
        var lines = headerText.Split(
            ["\r\n", "\n"],
            StringSplitOptions.None);

        if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0]))
        {
            throw new SazImportException("A SAZ raw session file has an empty start line.");
        }

        var headers = ParseHeaders(lines.Skip(1));
        var bodyOffset = headerEnd + delimiterLength;
        var body = bodyOffset >= bytes.Length ? [] : bytes[bodyOffset..];

        return new ParsedHttpMessage(lines[0], headers, body);
    }

    private TraceContent? CreateContent(
        byte[] body,
        IReadOnlyList<TraceHeader> headers)
    {
        if (body.Length == 0)
        {
            return null;
        }

        var contentType = GetHeader(headers, "Content-Type");
        var mimeType = contentType?.Split(';', 2)[0].Trim();
        var decodedBody = TryDecompressBody(body, GetHeader(headers, "Content-Encoding"));

        string text;
        if (string.Equals(
                mimeType,
                "application/mapi-http",
                StringComparison.OrdinalIgnoreCase))
        {
            text = DecodeMapiHttpDiagnostics(decodedBody);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new TraceContent(null, mimeType, body.LongLength, false, false);
            }
        }
        else if (!IsTextContent(mimeType))
        {
            var base64Data = IsPreviewableImage(mimeType)
                && decodedBody.Length <= _options.MaximumTextLength
                    ? Convert.ToBase64String(decodedBody)
                    : null;

            return new TraceContent(
                null,
                mimeType,
                body.LongLength,
                false,
                false,
                base64Data);
        }
        else
        {
            var encoding = GetTextEncoding(contentType);
            text = encoding.GetString(decodedBody);
        }

        var isTruncated = text.Length > _options.MaximumTextLength;

        if (isTruncated)
        {
            text = text[.._options.MaximumTextLength];
        }

        return new TraceContent(text, mimeType, body.LongLength, false, isTruncated);
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

    private static string DecodeMapiHttpDiagnostics(byte[] body)
    {
        var values = new List<string>();
        ExtractAsciiRuns(body, values);
        ExtractUtf16LittleEndianRuns(body, values);

        return string.Join(
            Environment.NewLine,
            values.Distinct(StringComparer.Ordinal));
    }

    private static void ExtractAsciiRuns(
        byte[] body,
        ICollection<string> values)
    {
        var start = -1;

        for (var index = 0; index <= body.Length; index++)
        {
            var isPrintable = index < body.Length
                && (body[index] is >= 0x20 and <= 0x7e
                    || body[index] is 0x09 or 0x0a or 0x0d);

            if (isPrintable && start < 0)
            {
                start = index;
            }
            else if (!isPrintable && start >= 0)
            {
                AddDiagnosticRun(
                    Encoding.ASCII.GetString(body, start, index - start),
                    values);
                start = -1;
            }
        }
    }

    private static void ExtractUtf16LittleEndianRuns(
        byte[] body,
        ICollection<string> values)
    {
        for (var start = 0; start + 7 < body.Length;)
        {
            var end = start;
            while (end + 1 < body.Length
                   && body[end] is >= 0x20 and <= 0x7e
                   && body[end + 1] == 0)
            {
                end += 2;
            }

            if (end - start >= 8)
            {
                AddDiagnosticRun(
                    Encoding.Unicode.GetString(body, start, end - start),
                    values);
                start = end;
            }
            else
            {
                start++;
            }
        }
    }

    private static void AddDiagnosticRun(
        string value,
        ICollection<string> values)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 4)
        {
            values.Add(trimmed);
        }
    }

    private byte[] TryDecompressBody(byte[] body, string? contentEncoding)
    {
        if (string.IsNullOrWhiteSpace(contentEncoding))
        {
            return body;
        }

        try
        {
            using var input = new MemoryStream(body);
            using Stream decompressionStream = contentEncoding.Trim().ToLowerInvariant() switch
            {
                "gzip" => new GZipStream(input, CompressionMode.Decompress),
                "deflate" => new DeflateStream(input, CompressionMode.Decompress),
                "br" => new BrotliStream(input, CompressionMode.Decompress),
                _ => input
            };

            if (ReferenceEquals(decompressionStream, input))
            {
                return body;
            }

            using var output = new MemoryStream();
            var buffer = new byte[81920];
            var maximumDecompressedBytes = Math.Min(
                _options.MaximumSessionFileSize,
                checked(_options.MaximumTextLength * 4));

            while (true)
            {
                var read = decompressionStream.Read(buffer);
                if (read == 0)
                {
                    break;
                }

                if (output.Length + read > maximumDecompressedBytes)
                {
                    throw new SazImportException(
                        "A compressed HTTP body exceeds the decompressed content limit.");
                }

                output.Write(buffer, 0, read);
            }

            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            return body;
        }
    }

    private static Uri BuildRequestUri(
        string method,
        string target,
        IReadOnlyList<TraceHeader> headers,
        IReadOnlyDictionary<string, string> flags)
    {
        foreach (var flagName in new[] { "x-fullUrl", "x-OriginalURL" })
        {
            if (flags.TryGetValue(flagName, out var flaggedUrl)
                && Uri.TryCreate(flaggedUrl, UriKind.Absolute, out var parsedFlaggedUrl))
            {
                return parsedFlaggedUrl;
            }
        }

        if (Uri.TryCreate(target, UriKind.Absolute, out var absoluteUrl)
            && (string.Equals(
                    absoluteUrl.Scheme,
                    Uri.UriSchemeHttp,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    absoluteUrl.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return absoluteUrl;
        }

        if (string.Equals(method, "CONNECT", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri($"https://{target.TrimEnd('/')}/");
        }

        var host = GetHeader(headers, "Host");
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new SazImportException(
                "A relative SAZ request URL does not contain a Host header.");
        }

        var isHttps = flags.TryGetValue("x-HTTPS", out var httpsValue)
            && !string.Equals(httpsValue, "false", StringComparison.OrdinalIgnoreCase);
        var scheme = isHttps || host.EndsWith(":443", StringComparison.Ordinal)
            ? "https"
            : "http";
        var path = target.StartsWith('/') ? target : $"/{target}";

        return new Uri($"{scheme}://{host}{path}");
    }

    private static SazMetadata ParseMetadata(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using var reader = XmlReader.Create(
                stream,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });
            var document = XDocument.Load(reader, LoadOptions.None);

            var flags = document
                .Descendants()
                .Where(element =>
                    string.Equals(element.Name.LocalName, "SessionFlag", StringComparison.Ordinal))
                .Select(element => new
                {
                    Name = (string?)element.Attribute("N"),
                    Value = (string?)element.Attribute("V")
                })
                .Where(flag => !string.IsNullOrWhiteSpace(flag.Name))
                .ToDictionary(
                    flag => flag.Name!,
                    flag => flag.Value ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

            var timers = document
                .Descendants()
                .FirstOrDefault(element =>
                    string.Equals(element.Name.LocalName, "SessionTimers", StringComparison.Ordinal));

            var startedAt = ParseTimestamp(
                (string?)timers?.Attribute("ClientBeginRequest")
                ?? (string?)timers?.Attribute("ClientConnected"));
            var completedAt = ParseTimestamp(
                (string?)timers?.Attribute("ClientDoneResponse"));
            var duration = startedAt is not null && completedAt is not null
                ? completedAt - startedAt
                : null;

            return new SazMetadata(
                flags,
                startedAt,
                duration is { } elapsed && elapsed >= TimeSpan.Zero ? elapsed : null);
        }
        catch (XmlException exception)
        {
            throw new SazImportException("A SAZ session metadata file contains invalid XML.", exception);
        }
    }

    private async Task<byte[]> ReadEntryBytesAsync(
        SharpZipFile archive,
        ZipEntry entry,
        CancellationToken cancellationToken)
    {
        if (entry.Size > _options.MaximumSessionFileSize)
        {
            throw new SazImportException(
                $"SAZ entry '{entry.Name}' exceeds the per-file size limit.");
        }

        try
        {
            await using var entryStream = archive.GetInputStream(entry);
            using var output = new MemoryStream((int)entry.Size);
            await CopyWithLimitAsync(
                entryStream,
                output,
                _options.MaximumSessionFileSize,
                cancellationToken);
            return output.ToArray();
        }
        catch (SharpZipBaseException exception) when (entry.IsCrypted)
        {
            throw new SazInvalidPasswordException(exception);
        }
        catch (SharpZipBaseException exception)
        {
            throw new SazImportException(
                $"SAZ entry '{entry.Name}' uses an unsupported compression method.",
                exception);
        }
    }

    private static async Task CopyWithLimitAsync(
        Stream source,
        Stream destination,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long copied = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            copied += read;
            if (copied > maximumBytes)
            {
                throw new SazImportException(
                    $"The input exceeds the {maximumBytes / (1024 * 1024)} MB limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static IReadOnlyList<TraceHeader> ParseHeaders(IEnumerable<string> lines)
    {
        var headers = new List<TraceHeader>();

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (char.IsWhiteSpace(line[0]) && headers.Count > 0)
            {
                var previous = headers[^1];
                headers[^1] = previous with { Value = $"{previous.Value} {line.Trim()}" };
                continue;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            headers.Add(new TraceHeader(
                line[..separator].Trim(),
                line[(separator + 1)..].Trim()));
        }

        return headers;
    }

    private static string? GetHeader(
        IReadOnlyList<TraceHeader> headers,
        string name) =>
        headers.FirstOrDefault(header =>
            string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;

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

    private static Encoding GetTextEncoding(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return Encoding.UTF8;
        }

        var charset = contentType
            .Split(';', StringSplitOptions.TrimEntries)
            .FirstOrDefault(part =>
                part.StartsWith("charset=", StringComparison.OrdinalIgnoreCase))
            ?.Split('=', 2)[1]
            .Trim('"', '\'');

        return charset?.ToLowerInvariant() switch
        {
            "iso-8859-1" or "latin1" => Encoding.Latin1,
            "us-ascii" or "ascii" => Encoding.ASCII,
            _ => Encoding.UTF8
        };
    }

    private static DateTimeOffset? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var timestamp)
            ? timestamp
            : null;
    }

    private static int FindHeaderEnd(byte[] bytes, out int delimiterLength)
    {
        for (var index = 0; index <= bytes.Length - 4; index++)
        {
            if (bytes[index] == '\r'
                && bytes[index + 1] == '\n'
                && bytes[index + 2] == '\r'
                && bytes[index + 3] == '\n')
            {
                delimiterLength = 4;
                return index;
            }
        }

        for (var index = 0; index <= bytes.Length - 2; index++)
        {
            if (bytes[index] == '\n' && bytes[index + 1] == '\n')
            {
                delimiterLength = 2;
                return index;
            }
        }

        delimiterLength = 0;
        return -1;
    }

    private static void ValidateEntryPath(string fullName)
    {
        var normalized = NormalizeEntryPath(fullName);

        if (normalized.StartsWith('/')
            || normalized.Split('/').Any(segment => segment == ".."))
        {
            throw new SazImportException(
                $"SAZ entry '{fullName}' contains an unsafe path.");
        }
    }

    private static string NormalizeEntryPath(string path) =>
        path.Replace('\\', '/');

    [GeneratedRegex(
        @"^raw/(?<id>\d+)_(?<kind>[csm])\.(?:txt|xml)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SessionFilePattern();

    private sealed class SessionFiles
    {
        public ZipEntry? Request { get; set; }

        public ZipEntry? Response { get; set; }

        public ZipEntry? Metadata { get; set; }
    }

    private sealed record SazMetadata(
        IReadOnlyDictionary<string, string> Flags,
        DateTimeOffset? StartedAt,
        TimeSpan? Duration)
    {
        public static SazMetadata Empty { get; } = new(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            null,
            null);
    }

    private sealed record ParsedHttpMessage(
        string StartLine,
        IReadOnlyList<TraceHeader> Headers,
        byte[] Body);

    private sealed record ParsedRequest(
        string Method,
        Uri Url,
        IReadOnlyList<TraceHeader> Headers,
        byte[] Body);

    private sealed record ParsedResponse(
        int StatusCode,
        string? StatusText,
        IReadOnlyList<TraceHeader> Headers,
        byte[] Body)
    {
        public static ParsedResponse Empty { get; } = new(
            0,
            "No response",
            [],
            []);
    }
}
