using M365Trace.Core;

namespace M365Trace.Web.Services;

public sealed class DiagnosticHeaderService
{
    private static readonly HashSet<string> RecognizedHeaderNames = new(
        [
            "request-id",
            "client-request-id",
            "x-ms-request-id",
            "x-ms-correlation-id",
            "x-feserver",
            "x-beserver",
            "x-calculatedbetarget",
            "x-diaginfo",
            "x-ms-diagnostics",
            "retry-after",
            "location",
            "www-authenticate"
        ],
        StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<DiagnosticHeader> GetHeaders(TraceSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return GetHeaders(
                session.RequestHeaders,
                DiagnosticHeaderSource.Request)
            .Concat(GetHeaders(
                session.ResponseHeaders,
                DiagnosticHeaderSource.Response))
            .ToArray();
    }

    private static IEnumerable<DiagnosticHeader> GetHeaders(
        IEnumerable<TraceHeader> headers,
        DiagnosticHeaderSource source) =>
        headers
            .Where(header => RecognizedHeaderNames.Contains(header.Name))
            .Select(header => new DiagnosticHeader(
                source,
                header.Name,
                header.Value));
}

public sealed record DiagnosticHeader(
    DiagnosticHeaderSource Source,
    string Name,
    string Value);

public enum DiagnosticHeaderSource
{
    Request,
    Response
}
