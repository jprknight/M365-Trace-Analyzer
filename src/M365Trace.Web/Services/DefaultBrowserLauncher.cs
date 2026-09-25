using System.ComponentModel;
using System.Diagnostics;

namespace M365Trace.Web.Services;

public sealed class DefaultBrowserLauncher(
    ILogger<DefaultBrowserLauncher> logger)
{
    public const string DisableBrowserLaunchEnvironmentVariable =
        "M365_TRACE_DISABLE_BROWSER_LAUNCH";

    public void TryLaunch(IEnumerable<string> serverAddresses)
    {
        ArgumentNullException.ThrowIfNull(serverAddresses);

        if (IsBrowserLaunchDisabled() || !OperatingSystem.IsWindows())
        {
            return;
        }

        var browserUrl = GetBrowserUrl(serverAddresses);
        if (browserUrl is null)
        {
            logger.LogWarning(
                "The application started, but no HTTP address was available "
                + "to open in the default browser.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = browserUrl,
                UseShellExecute = true
            });
            logger.LogInformation(
                "Opened {BrowserUrl} in the default browser.",
                browserUrl);
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException)
        {
            logger.LogWarning(
                exception,
                "The application started at {BrowserUrl}, but the default "
                + "browser could not be opened.",
                browserUrl);
        }
    }

    public static string? GetBrowserUrl(IEnumerable<string> serverAddresses)
    {
        ArgumentNullException.ThrowIfNull(serverAddresses);

        foreach (var address in serverAddresses)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp
                    && uri.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }

            var builder = new UriBuilder(uri);
            if (uri.Host is "0.0.0.0" or "[::]" or "::" or "*")
            {
                builder.Host = "localhost";
            }

            return builder.Uri.AbsoluteUri;
        }

        return null;
    }

    private static bool IsBrowserLaunchDisabled()
    {
        var value = Environment.GetEnvironmentVariable(
            DisableBrowserLaunchEnvironmentVariable);
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
