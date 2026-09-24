using M365Trace.Core;
using M365Trace.Web.Services;

namespace M365Trace.Web.Tests;

public sealed class DiagnosticHeaderServiceTests
{
    private readonly DiagnosticHeaderService _service = new();

    [Fact]
    public void GetHeaders_ReturnsRecognizedValuesCaseInsensitively()
    {
        var session = new TraceSession
        {
            Id = 1,
            StartedAt = DateTimeOffset.UnixEpoch,
            Method = "GET",
            Url = new Uri("https://example.test/"),
            StatusCode = 200,
            Duration = TimeSpan.Zero,
            RequestHeaders =
            [
                new TraceHeader("X-MS-REQUEST-ID", "request-1"),
                new TraceHeader("Accept", "application/json")
            ],
            ResponseHeaders =
            [
                new TraceHeader("retry-after", "15"),
                new TraceHeader("Server", "example")
            ]
        };

        var result = _service.GetHeaders(session);

        Assert.Equal(2, result.Count);
        Assert.Equal(DiagnosticHeaderSource.Request, result[0].Source);
        Assert.Equal("X-MS-REQUEST-ID", result[0].Name);
        Assert.Equal("request-1", result[0].Value);
        Assert.Equal(DiagnosticHeaderSource.Response, result[1].Source);
        Assert.Equal("retry-after", result[1].Name);
        Assert.Equal("15", result[1].Value);
    }
}
