namespace M365Trace.Import.Saz;

public sealed class SazInvalidPasswordException : SazImportException
{
    public SazInvalidPasswordException(Exception innerException)
        : base("The password is incorrect or the SAZ archive cannot be decrypted.", innerException)
    {
    }
}
