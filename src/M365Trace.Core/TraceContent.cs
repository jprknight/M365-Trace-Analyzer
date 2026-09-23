namespace M365Trace.Core;

public sealed record TraceContent(
    string? Text,
    string? MimeType,
    long? Size,
    bool IsBase64Encoded,
    bool IsTruncated,
    string? Base64Data = null);
