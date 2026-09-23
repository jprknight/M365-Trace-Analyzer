using System.Text.RegularExpressions;
using M365Trace.Core;

namespace M365Trace.Rules;

public static partial class TraceSessionExtensions
{
    public static bool UrlContains(this TraceSession session, string value)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return session.Url.AbsoluteUri.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ResponseContainsWord(this TraceSession session, string word)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        var responseText = session.ResponseContent?.Text;
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        return Regex.IsMatch(
            responseText,
            $@"(?<![\p{{L}}\p{{Nd}}_]){Regex.Escape(word)}(?![\p{{L}}\p{{Nd}}_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static bool ResponseContains(this TraceSession session, string value)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return session.ResponseContent?.Text?.Contains(
            value,
            StringComparison.OrdinalIgnoreCase) == true;
    }

    public static string? GetRequestHeader(this TraceSession session, string name)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return session.RequestHeaders
            .FirstOrDefault(header =>
                string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    public static string? GetResponseHeader(this TraceSession session, string name)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return session.ResponseHeaders
            .FirstOrDefault(header =>
                string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }
}
