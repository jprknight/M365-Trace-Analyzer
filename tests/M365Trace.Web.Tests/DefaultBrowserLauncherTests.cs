using M365Trace.Web.Services;

namespace M365Trace.Web.Tests;

public sealed class DefaultBrowserLauncherTests
{
    [Theory]
    [InlineData("http://localhost:8080", "http://localhost:8080/")]
    [InlineData("https://127.0.0.1:8443", "https://127.0.0.1:8443/")]
    [InlineData("http://0.0.0.0:8080", "http://localhost:8080/")]
    [InlineData("http://[::]:8080", "http://localhost:8080/")]
    public void GetBrowserUrl_ReturnsBrowsableLocalAddress(
        string serverAddress,
        string expected)
    {
        var result = DefaultBrowserLauncher.GetBrowserUrl([serverAddress]);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetBrowserUrl_IgnoresUnsupportedAddresses()
    {
        var result = DefaultBrowserLauncher.GetBrowserUrl(
            ["not-an-address", "ftp://localhost:8080"]);

        Assert.Null(result);
    }
}
