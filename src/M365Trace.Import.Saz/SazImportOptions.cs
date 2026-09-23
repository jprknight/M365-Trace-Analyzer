namespace M365Trace.Import.Saz;

public sealed class SazImportOptions
{
    public const long DefaultMaximumFileSize = 250L * 1024 * 1024;

    public const int DefaultMaximumEntryCount = 300_000;

    public const long DefaultMaximumExpandedSize = 1024L * 1024 * 1024;

    public const int DefaultMaximumSessionFileSize = 100 * 1024 * 1024;

    public const int DefaultMaximumTextLength = 5 * 1024 * 1024;

    public long MaximumFileSize { get; init; } = DefaultMaximumFileSize;

    public int MaximumEntryCount { get; init; } = DefaultMaximumEntryCount;

    public long MaximumExpandedSize { get; init; } = DefaultMaximumExpandedSize;

    public int MaximumSessionFileSize { get; init; } = DefaultMaximumSessionFileSize;

    public int MaximumTextLength { get; init; } = DefaultMaximumTextLength;
}
