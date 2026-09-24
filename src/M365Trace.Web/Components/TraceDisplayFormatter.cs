using M365Trace.Core;

namespace M365Trace.Web.Components;

public static class TraceDisplayFormatter
{
    public static string GetStatusClass(int statusCode) => statusCode switch
    {
        >= 500 => "status-error",
        >= 400 => "status-warning",
        >= 300 => "status-redirect",
        >= 200 => "status-success",
        _ => "status-neutral"
    };

    public static string GetSeverityLabel(
        TraceSeverity? severity) => severity switch
        {
            TraceSeverity.InternalError => "Internal error",
            TraceSeverity.Uninteresting => "Uninteresting",
            TraceSeverity.FalsePositive => "False positive",
            TraceSeverity.Normal => "Normal",
            TraceSeverity.Warning => "Warning",
            TraceSeverity.Concerning => "Concerning",
            TraceSeverity.Severe => "Severe",
            _ => "Not analyzed"
        };

    public static string GetSeverityClass(
        TraceSeverity? severity) => severity switch
        {
            TraceSeverity.InternalError => "severity-internal-error",
            TraceSeverity.Uninteresting => "severity-uninteresting",
            TraceSeverity.FalsePositive => "severity-false-positive",
            TraceSeverity.Normal => "severity-normal",
            TraceSeverity.Warning => "severity-warning",
            TraceSeverity.Concerning => "severity-concerning",
            TraceSeverity.Severe => "severity-severe",
            _ => "severity-unclassified"
        };

    public static string GetAnalysisSeverityClass(
        TraceSeverity? severity) => severity switch
        {
            TraceSeverity.InternalError => "analysis-internal-error",
            TraceSeverity.Uninteresting => "analysis-uninteresting",
            TraceSeverity.FalsePositive => "analysis-false-positive",
            TraceSeverity.Normal => "analysis-normal",
            TraceSeverity.Warning => "analysis-warning",
            TraceSeverity.Concerning => "analysis-concerning",
            TraceSeverity.Severe => "analysis-severe",
            _ => "analysis-unclassified"
        };

    public static string FormatDuration(TimeSpan duration) =>
        duration.TotalSeconds >= 1
            ? $"{duration.TotalSeconds:N2} s"
            : $"{duration.TotalMilliseconds:N0} ms";
}
