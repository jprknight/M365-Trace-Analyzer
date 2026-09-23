namespace M365Trace.Core;

public enum TraceSeverity
{
    InternalError = 0,
    Uninteresting = 10,
    FalsePositive = 20,
    Normal = 30,
    Warning = 40,
    Concerning = 50,
    Severe = 60
}
