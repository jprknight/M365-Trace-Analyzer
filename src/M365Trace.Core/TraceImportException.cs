namespace M365Trace.Core;

public class TraceImportException : Exception
{
    public TraceImportException(string message)
        : base(message)
    {
    }

    public TraceImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
