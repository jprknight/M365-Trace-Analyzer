namespace M365Trace.Import.Har;

public sealed class HarImportOptions
{
    public const long DefaultMaximumFileSize = 250L * 1024 * 1024;

    public const int DefaultMaximumEntryCount = 100_000;

    public const int DefaultMaximumTextLength = 5 * 1024 * 1024;

    public long MaximumFileSize { get; init; } = DefaultMaximumFileSize;

    public int MaximumEntryCount { get; init; } = DefaultMaximumEntryCount;

    public int MaximumTextLength { get; init; } = DefaultMaximumTextLength;
}
