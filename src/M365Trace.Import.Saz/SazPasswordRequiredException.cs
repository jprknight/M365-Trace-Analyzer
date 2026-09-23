namespace M365Trace.Import.Saz;

public sealed class SazPasswordRequiredException : SazImportException
{
    public SazPasswordRequiredException()
        : base("This SAZ archive is password protected. Enter its password to continue.")
    {
    }
}
