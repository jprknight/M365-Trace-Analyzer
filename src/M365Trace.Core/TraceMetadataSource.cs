namespace M365Trace.Core;

public enum TraceSourceFormat
{
    Unknown,
    Har,
    Saz
}

public enum TraceMetadataSource
{
    Unknown,
    HarEntry,
    HarPage,
    HarTiming,
    HttpMessage,
    SazMetadata,
    SazSessionFlag,
    Derived
}

public enum TraceCacheDisposition
{
    Unknown,
    Miss,
    Hit,
    Revalidated,
    Bypassed
}

public enum TraceCaptureState
{
    Unknown,
    Complete,
    Partial,
    Missing
}

public enum TraceContentAvailability
{
    Unknown,
    Available,
    NotPresent,
    Truncated,
    Unavailable,
    UnsupportedEncoding,
    InvalidEncoding
}
