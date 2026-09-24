namespace M365Trace.Web.Services;

public sealed record SessionQuery
{
    public string FreeText { get; init; } = string.Empty;

    public SessionSortColumn SortColumn { get; init; } = SessionSortColumn.Id;

    public bool SortAscending { get; init; } = true;
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
