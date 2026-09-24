using System.Text;
using M365Trace.Core;

namespace M365Trace.Web.Services;

public sealed class SessionCopyFormatter
{
    public ClipboardPayload FormatUrl(TraceSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new ClipboardPayload(session.Url.AbsoluteUri, false);
    }

    public ClipboardPayload FormatHeaders(
        IReadOnlyList<TraceHeader> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        return new ClipboardPayload(
            string.Join(
                "\n",
                headers.Select(header =>
                    $"{header.Name}: {header.Value}")),
            false);
    }

    public ClipboardPayload? FormatBody(TraceContent? content)
    {
        var text = content?.Text ?? content?.Base64Data;
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (content!.IsTruncated)
        {
            text += "\n\n[Content truncated during import.]";
        }

        return new ClipboardPayload(text, content.IsTruncated);
    }

    public ClipboardPayload? FormatFindings(TraceSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.Analysis.HasFindings)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.Append(session.Method);
        builder.Append(' ');
        builder.AppendLine(session.Url.AbsoluteUri);

        foreach (var finding in session.Analysis.Findings)
        {
            builder.AppendLine();
            builder.Append('[');
            builder.Append(finding.Severity);
            builder.Append("] ");
            builder.AppendLine(finding.Title);
            builder.Append("Rule: ");
            builder.AppendLine(finding.RuleId);
            AppendValue(builder, "Description", finding.Description);
            AppendValue(builder, "Evidence", finding.Evidence);
            AppendValue(
                builder,
                "Recommended investigation",
                finding.Recommendation);
        }

        return new ClipboardPayload(
            builder.ToString().TrimEnd(),
            false);
    }

    private static void AppendValue(
        StringBuilder builder,
        string label,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(value.Trim());
    }
}

public sealed record ClipboardPayload(
    string Text,
    bool IncludesTruncationNotice);
