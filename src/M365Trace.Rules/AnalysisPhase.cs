namespace M365Trace.Rules;

public enum AnalysisPhase
{
    BroadChecks = 100,
    DerivedMetadata = 200,
    ResponseCode = 300,
    Authentication = 400,
    SessionType = 500,
    ResponseServer = 600,
    Performance = 700
}
