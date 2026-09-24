namespace M365Trace.Web.Services;

public sealed record SessionQuery
{
    public string FreeText { get; init; } = string.Empty;

    public IReadOnlyList<TraceSeverityFilter> Severities { get; init; } = [];

    public IReadOnlyList<TraceStatusFamily> StatusFamilies { get; init; } = [];

    public IReadOnlyList<int> StatusCodes { get; init; } = [];

    public IReadOnlyList<string> Methods { get; init; } = [];

    public IReadOnlyList<string> Hosts { get; init; } = [];

    public SessionDurationFilter? Duration { get; init; }

    public IReadOnlyList<string> SessionTypes { get; init; } = [];

    public IReadOnlyList<string> Authentications { get; init; } = [];

    public IReadOnlyList<string> FindingRuleIds { get; init; } = [];

    public SessionFindingFilter? Findings { get; init; }

    public SessionSortColumn SortColumn { get; init; } = SessionSortColumn.Id;

    public bool SortAscending { get; init; } = true;

    public bool HasStructuredFilters =>
        Severities.Count > 0
        || StatusFamilies.Count > 0
        || StatusCodes.Count > 0
        || Methods.Count > 0
        || Hosts.Count > 0
        || Duration is not null
        || SessionTypes.Count > 0
        || Authentications.Count > 0
        || FindingRuleIds.Count > 0
        || Findings is not null;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(FreeText) || HasStructuredFilters;
}

public enum TraceSeverityFilter
{
    Severe,
    Concerning,
    Warning,
    Normal,
    Other
}

public enum TraceStatusFamily
{
    NoResponse,
    Success,
    Redirect,
    ClientError,
    ServerError,
    Other
}

public enum SessionDurationFilter
{
    UnderOneSecond,
    OneToFiveSeconds,
    FiveToThirtySeconds,
    ThirtySecondsOrMore
}

public enum SessionFindingFilter
{
    HasFindings,
    NoFindings
}

public enum SessionSortColumn
{
    Id,
    Analysis,
    Status,
    Method,
    Host,
    Path,
    Duration
}
