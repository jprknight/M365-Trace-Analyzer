using M365Trace.Core;

namespace M365Trace.Import.Saz;

public sealed class SazImportException : TraceImportException
{
    public SazImportException(string message)
        : base(message)
    {
    }

    public SazImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
