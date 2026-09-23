namespace M365Trace.Core;

public interface ITraceImporter
{
    string FormatName { get; }

    bool CanImport(string fileName);

    Task<IReadOnlyList<TraceSession>> ImportAsync(
        Stream stream,
        TraceImportOptions? options = null,
        CancellationToken cancellationToken = default);
}
