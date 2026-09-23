using System.Text.RegularExpressions;
using M365Trace.Core;

namespace M365Trace.Rules;

public sealed class SessionFacts
{
    private readonly IReadOnlyDictionary<string, string> _requestHeaders;
    private readonly IReadOnlyDictionary<string, string> _responseHeaders;

    public SessionFacts(TraceSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        Session = session;
        Url = session.Url.AbsoluteUri;
        Host = session.Url.Host;
        PathAndQuery = session.Url.PathAndQuery;
        RequestText = session.RequestContent?.Text ?? string.Empty;
        ResponseText = session.ResponseContent?.Text ?? string.Empty;
        _requestHeaders = CreateHeaderIndex(session.RequestHeaders);
        _responseHeaders = CreateHeaderIndex(session.ResponseHeaders);
        ContentType = GetResponseHeader("Content-Type") ?? string.Empty;
    }

    public TraceSession Session { get; }

    public string Url { get; }

    public string Host { get; }

    public string PathAndQuery { get; }

    public string RequestText { get; }

    public string ResponseText { get; }

    public string ContentType { get; }

    public bool UrlContains(string value) =>
        Url.Contains(value, StringComparison.OrdinalIgnoreCase);

    public bool RequestContains(string value) =>
        RequestText.Contains(value, StringComparison.OrdinalIgnoreCase);

    public bool ResponseContains(string value) =>
        ResponseText.Contains(value, StringComparison.OrdinalIgnoreCase);

    public bool RequestOrResponseContains(string value) =>
        RequestContains(value) || ResponseContains(value);

    public int CountResponseWord(string word)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        return Regex.Matches(
            ResponseText,
            $@"(?<![\p{{L}}\p{{Nd}}_]){Regex.Escape(word)}(?![\p{{L}}\p{{Nd}}_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
    }

    public string? GetRequestHeader(string name) =>
        GetHeader(_requestHeaders, name);

    public string? GetResponseHeader(string name) =>
        GetHeader(_responseHeaders, name);

    private static IReadOnlyDictionary<string, string> CreateHeaderIndex(
        IReadOnlyList<TraceHeader> headers)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            result.TryAdd(header.Name, header.Value);
        }

        return result;
    }

    private static string? GetHeader(
        IReadOnlyDictionary<string, string> headers,
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return headers.GetValueOrDefault(name);
    }
}
