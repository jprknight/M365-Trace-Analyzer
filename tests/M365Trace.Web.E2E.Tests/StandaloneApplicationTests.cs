using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;

namespace M365Trace.Web.E2E.Tests;

public sealed class StandaloneApplicationTests
{
    [Fact]
    public async Task StandalonePackage_SupportsCriticalInvestigationWorkflow()
    {
        var publishDirectory = Environment.GetEnvironmentVariable(
            "M365_TRACE_PUBLISH_DIR");
        Assert.False(
            string.IsNullOrWhiteSpace(publishDirectory),
            "M365_TRACE_PUBLISH_DIR must identify the tested publish directory.");

        var executablePath = Path.Combine(
            publishDirectory!,
            "M365Trace.Web.exe");
        Assert.True(
            File.Exists(executablePath),
            $"Published executable was not found at '{executablePath}'.");

        var port = GetAvailablePort();
        var url = $"http://localhost:{port}";
        var outputPath = Path.GetTempFileName();
        var errorPath = Path.GetTempFileName();
        var harPath = Path.Combine(
            Path.GetTempPath(),
            $"m365-trace-e2e-{Guid.NewGuid():N}.har");
        Process? process = null;

        try
        {
            await File.WriteAllTextAsync(harPath, CreateHar());
            process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"--urls \"{url}\"",
                WorkingDirectory = publishDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            Assert.NotNull(process);

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await WaitForApplicationAsync(url, process);

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions { Headless = true });
            await using var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            await page.GotoAsync(
                url,
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForFunctionAsync(
                "() => typeof window.Blazor !== 'undefined'");
            await page.WaitForTimeoutAsync(500);
            await page.Locator("input[type=file]").First.SetInputFilesAsync(harPath);
            await WaitForRowCountWithDiagnosticsAsync(page, 2);

            var diagnosticHeader = page
                .Locator(".diagnostic-header-row")
                .Filter(new LocatorFilterOptions { HasText = "request-id" });
            await diagnosticHeader.WaitForAsync();
            Assert.Contains("e2e-request-1", await diagnosticHeader.InnerTextAsync());

            var severityMenu = page.Locator(
                "details[data-filter-menu=severity]");
            var statusMenu = page.Locator(
                "details[data-filter-menu=status]");
            var durationMenu = page.Locator(
                "details[data-filter-menu=duration]");
            var findingsMenu = page.Locator(
                "details[data-filter-menu=findings]");
            var ruleMenu = page.Locator(
                "details[data-filter-menu=finding-rule]");
            await severityMenu.Locator("summary").ClickAsync();
            Assert.NotNull(await severityMenu.GetAttributeAsync("open"));

            await statusMenu.Locator("summary").ClickAsync();
            Assert.Null(await severityMenu.GetAttributeAsync("open"));
            Assert.NotNull(await statusMenu.GetAttributeAsync("open"));

            await page.Locator(".panel-heading h2").ClickAsync();
            Assert.Null(await statusMenu.GetAttributeAsync("open"));

            var menuBounds = await severityMenu
                .Locator("summary")
                .BoundingBoxAsync();
            var durationBounds = await durationMenu
                .Locator("summary")
                .BoundingBoxAsync();
            var findingsBounds = await findingsMenu
                .Locator("summary")
                .BoundingBoxAsync();
            var ruleBounds = await ruleMenu
                .Locator("summary")
                .BoundingBoxAsync();
            Assert.NotNull(menuBounds);
            Assert.NotNull(durationBounds);
            Assert.NotNull(findingsBounds);
            Assert.NotNull(ruleBounds);
            Assert.InRange(
                Math.Abs(menuBounds.Y - durationBounds.Y),
                0,
                0.5);
            Assert.InRange(
                Math.Abs(menuBounds.Height - durationBounds.Height),
                0,
                0.5);
            Assert.InRange(
                Math.Abs(ruleBounds.Y - findingsBounds.Y),
                0,
                0.5);
            Assert.InRange(
                Math.Abs(menuBounds.Height - findingsBounds.Height),
                0,
                0.5);

            await page
                .Locator("input.search-box")
                .FillAsync("SERVICE UNAVAILABLE");
            await WaitForRowCountWithDiagnosticsAsync(page, 1);
            await page
                .Locator("tbody tr[data-session-id='2']")
                .WaitForAsync();

            await page.Locator("button.search-clear-button").ClickAsync();
            await WaitForRowCountWithDiagnosticsAsync(page, 2);

            await page.Locator("input.search-box").FillAsync("missing");
            await WaitForRowCountWithDiagnosticsAsync(page, 0);
            Assert.True(await page.Locator("button.search-clear-button").IsVisibleAsync());

            await page.Locator("button.search-clear-button").ClickAsync();
            await WaitForRowCountWithDiagnosticsAsync(page, 2);

            var firstSession = page.Locator(
                "tbody tr[data-session-id='1']");
            var secondSession = page.Locator(
                "tbody tr[data-session-id='2']");
            await firstSession.FocusAsync();
            await firstSession.PressAsync("ArrowDown");
            await secondSession.WaitForAsync();
            await page
                .Locator("tbody tr.selected[data-session-id='2']")
                .WaitForAsync();
            Assert.Equal(
                "2",
                await page.EvaluateAsync<string>(
                    "() => document.activeElement?.dataset.sessionId"));
            Assert.Equal(
                "true",
                await secondSession.GetAttributeAsync("aria-selected"));

            await secondSession.PressAsync("Home");
            await page
                .Locator("tbody tr.selected[data-session-id='1']")
                .WaitForAsync();
            Assert.Equal(
                "1",
                await page.EvaluateAsync<string>(
                    "() => document.activeElement?.dataset.sessionId"));

            await firstSession.PressAsync("End");
            await page
                .Locator("tbody tr.selected[data-session-id='2']")
                .WaitForAsync();
            await secondSession.PressAsync("ArrowRight");
            Assert.True(await page
                .Locator("[data-session-detail]")
                .EvaluateAsync<bool>("element => element === document.activeElement"));

            await page.Locator("[data-session-detail]").PressAsync("ArrowLeft");
            Assert.Equal(
                "2",
                await page.EvaluateAsync<string>(
                    "() => document.activeElement?.dataset.sessionId"));

            await page.GetByRole(
                    AriaRole.Button,
                    new PageGetByRoleOptions { Name = "Request", Exact = true })
                .ClickAsync();
            var requestSummaries = page.Locator("summary").Filter(
                new LocatorFilterOptions { HasText = "Request headers" });
            var responseSummaries = page.Locator("summary").Filter(
                new LocatorFilterOptions { HasText = "Response headers" });

            await requestSummaries.First.WaitForAsync(
                new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await responseSummaries.First.WaitForAsync(
                new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });

            Assert.Equal(1, await requestSummaries.CountAsync());
            Assert.Equal(
                0,
                await responseSummaries.CountAsync());

            if (process.HasExited)
            {
                await File.WriteAllTextAsync(outputPath, await outputTask);
                await File.WriteAllTextAsync(errorPath, await errorTask);
                Assert.Fail(
                    $"Application exited unexpectedly. Output: {await File.ReadAllTextAsync(outputPath)} "
                    + $"Error: {await File.ReadAllTextAsync(errorPath)}");
            }
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            process?.Dispose();
            File.Delete(harPath);
            File.Delete(outputPath);
            File.Delete(errorPath);
        }
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task WaitForApplicationAsync(
        string url,
        Process process)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    "The packaged application exited before becoming available.");
            }

            try
            {
                using var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"The packaged application did not respond at '{url}'.");
    }

    private static async Task WaitForRowCountWithDiagnosticsAsync(
        IPage page,
        int expectedCount)
    {
        try
        {
            await page.WaitForFunctionAsync(
                "expected => document.querySelectorAll('tbody tr[data-session-id]').length === expected",
                expectedCount);
        }
        catch (TimeoutException exception)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected {expectedCount} session rows. "
                + $"Page content: {await page.ContentAsync()}",
                exception);
        }
    }

    private static string CreateHar() =>
        """
        {
          "log": {
            "version": "1.2",
            "creator": {
              "name": "M365 Trace Analyzer E2E",
              "version": "1.0"
            },
            "entries": [
              {
                "startedDateTime": "2026-09-23T10:00:00-04:00",
                "time": 100,
                "request": {
                  "method": "GET",
                  "url": "https://outlook.office.com/owa/",
                  "headers": [
                    { "name": "Accept", "value": "application/json" },
                    { "name": "request-id", "value": "e2e-request-1" }
                  ]
                },
                "response": {
                  "status": 200,
                  "statusText": "OK",
                  "headers": [{ "name": "Content-Type", "value": "application/json" }],
                  "content": {
                    "mimeType": "application/json",
                    "text": "{\"status\":\"ok\"}"
                  }
                }
              },
              {
                "startedDateTime": "2026-09-23T10:00:01-04:00",
                "time": 250,
                "request": {
                  "method": "GET",
                  "url": "https://outlook.office.com/owa/service.svc",
                  "headers": []
                },
                "response": {
                  "status": 503,
                  "statusText": "Service Unavailable",
                  "headers": [{ "name": "Content-Type", "value": "text/plain" }],
                  "content": {
                    "mimeType": "text/plain",
                    "text": "Service unavailable"
                  }
                }
              }
            ]
          }
        }
        """;
}
