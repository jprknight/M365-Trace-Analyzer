using System.Net;
using System.Text;
using Bunit;
using M365Trace.Core;
using M365Trace.Rules;
using M365Trace.Web.Components.Pages;
using M365Trace.Web.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace M365Trace.Web.Tests;

public sealed class HomeTests : IDisposable
{
    private readonly BunitContext _context = new();

    public HomeTests()
    {
        _context.Services.AddSingleton<ITraceImporter>(new StubTraceImporter());
        _context.Services.AddSingleton(
            new TraceAnalysisEngine([new NoOpRule()]));
        _context.Services.AddSingleton(
            new VersionUpdateService(new StubHttpClientFactory()));
    }

    [Fact]
    public void EmptyState_ProvidesTwoOpenTraceControls()
    {
        var component = _context.Render<Home>();

        component.WaitForAssertion(() =>
        {
            Assert.Equal(2, component.FindAll("label.file-button").Count);
            Assert.Equal(
                2,
                component.FindAll("label.file-button span")
                    .Count(element => element.TextContent == "Open Trace"));
            Assert.DoesNotContain("Confidence", component.Markup);
        });
    }

    [Fact]
    public void FilterClearButton_RestoresAllImportedSessions()
    {
        var component = _context.Render<Home>();
        var fileInput = component.FindComponents<InputFile>().First();

        fileInput.UploadFiles(
            InputFileContent.CreateFromText(
                "{}",
                "sample.har",
                contentType: "application/json"));

        component.WaitForAssertion(() =>
            Assert.Equal(2, component.FindAll("tbody tr").Count));

        component.Find("input.search-box").Input("missing");

        component.WaitForAssertion(() =>
        {
            Assert.Empty(component.FindAll("tbody tr"));
            Assert.NotNull(component.Find("button.search-clear-button"));
        });

        component.Find("button.search-clear-button").Click();

        component.WaitForAssertion(() =>
        {
            Assert.Equal(2, component.FindAll("tbody tr").Count);
            Assert.Empty(component.FindAll("button.search-clear-button"));
        });
    }

    [Fact]
    public void TrafficSelector_ShowsOnlyRequestedDetails()
    {
        var component = _context.Render<Home>();
        var fileInput = component.FindComponents<InputFile>().First();

        fileInput.UploadFiles(
            InputFileContent.CreateFromText(
                "{}",
                "sample.har",
                contentType: "application/json"));

        component.WaitForElement(".traffic-view-selector");

        component.FindAll(".traffic-view-selector button")
            .Single(button => button.TextContent == "Request")
            .Click();

        component.WaitForAssertion(() =>
        {
            Assert.Contains("Request headers", component.Markup);
            Assert.DoesNotContain("Response headers", component.Markup);
        });

        component.FindAll(".traffic-view-selector button")
            .Single(button => button.TextContent == "Response")
            .Click();

        component.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Request headers", component.Markup);
            Assert.Contains("Response headers", component.Markup);
        });
    }

    public void Dispose() => _context.Dispose();

    private sealed class StubTraceImporter : ITraceImporter
    {
        public string FormatName => "Test trace";

        public bool CanImport(string fileName) => true;

        public Task<IReadOnlyList<TraceSession>> ImportAsync(
            Stream stream,
            TraceImportOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TraceSession>>(
            [
                CreateSession(1, "https://outlook.office.com/owa/", 200),
                CreateSession(2, "https://login.microsoftonline.com/common/oauth2/token", 503)
            ]);
    }

    private sealed class NoOpRule : ITraceRule
    {
        public string Id => "M365.Test.NoOp";

        public AnalysisPhase Phase => AnalysisPhase.BroadChecks;

        public int Order => 1;

        public bool AppliesTo(AnalysisContext context) => false;

        public void Evaluate(AnalysisContext context)
        {
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new StubHttpMessageHandler())
            {
                BaseAddress = new Uri("https://api.github.test/")
            };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "tag_name": "v1.0.2",
                      "html_url": "https://github.test/release"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
    }

    private static TraceSession CreateSession(
        int id,
        string url,
        int statusCode) =>
        new()
        {
            Id = id,
            StartedAt = DateTimeOffset.Parse("2026-09-23T10:00:00-04:00"),
            Method = "GET",
            Url = new Uri(url),
            StatusCode = statusCode,
            StatusText = statusCode == 200 ? "OK" : "Service Unavailable",
            Duration = TimeSpan.FromMilliseconds(id * 100),
            RequestHeaders = [new TraceHeader("Accept", "application/json")],
            ResponseHeaders = [new TraceHeader("Content-Type", "application/json")],
            ResponseContent = new TraceContent(
                """{"status":"ok"}""",
                "application/json",
                15,
                false,
                false)
        };
}
