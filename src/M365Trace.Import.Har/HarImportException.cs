using M365Trace.Core;

namespace M365Trace.Import.Har;

public sealed class HarImportException : TraceImportException
{
    public HarImportException(string message)
        : base(message)
    {
    }

    public HarImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
