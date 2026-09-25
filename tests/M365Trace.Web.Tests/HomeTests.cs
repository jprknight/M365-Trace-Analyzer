using System.Net;
using System.Text;
using Bunit;
using M365Trace.Core;
using M365Trace.Import.Saz;
using M365Trace.Rules;
using M365Trace.Web.Components;
using M365Trace.Web.Components.Pages;
using M365Trace.Web.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace M365Trace.Web.Tests;

public sealed class HomeTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly StubTraceImporter _importer = new();
    private readonly StubHttpMessageHandler _httpHandler = new();

    public HomeTests()
    {
        _context.Services.AddSingleton<ITraceImporter>(_importer);
        _context.Services.AddSingleton(
            new TraceAnalysisEngine([new TestAnalysisRule()]));
        _context.Services.AddSingleton(
            new VersionUpdateService(
                new StubHttpClientFactory(_httpHandler)));
        _context.Services.AddSingleton<SessionQueryService>();
        _context.Services.AddSingleton<TraceSummaryService>();
        _context.Services.AddSingleton<DiagnosticHeaderService>();
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
    public void SuccessfulImport_ShowsFileAndSelectsFirstSession()
    {
        var component = RenderAndLoad();

        component.WaitForAssertion(() =>
        {
            Assert.NotNull(component.FindComponent<SessionTable>());
            Assert.NotNull(component.FindComponent<SessionDetailPanel>());
            Assert.Contains("sample.har", component.Markup);
            Assert.Contains("3 sessions", component.Markup);
            Assert.Equal("1", component.Find("tbody tr.selected td").TextContent);
            Assert.Contains("GET outlook.office.com", component.Markup);
        });
    }

    [Fact]
    public void SessionWorkspace_ExposesKeyboardAccessibilitySemantics()
    {
        var component = RenderAndLoad();

        component.WaitForAssertion(() =>
        {
            var grid = component.Find("table[data-session-grid]");
            Assert.Equal("grid", grid.GetAttribute("role"));
            Assert.Equal("Trace sessions", grid.GetAttribute("aria-label"));
            Assert.Equal(
                "session-grid-instructions",
                grid.GetAttribute("aria-describedby"));

            var rows = component.FindAll("tbody tr[data-session-row]");
            Assert.Equal(3, rows.Count);
            Assert.Equal("0", rows[0].GetAttribute("tabindex"));
            Assert.Equal("true", rows[0].GetAttribute("aria-selected"));
            Assert.Equal("-1", rows[1].GetAttribute("tabindex"));
            Assert.Equal("false", rows[1].GetAttribute("aria-selected"));
            Assert.Contains(
                "Session 1, Normal, HTTP 200",
                rows[0].GetAttribute("aria-label"));

            var detailPanel = component.Find("[data-session-detail]");
            Assert.Equal("0", detailPanel.GetAttribute("tabindex"));
            Assert.Equal(
                "Session 1 details for GET outlook.office.com",
                detailPanel.GetAttribute("aria-label"));
        });
    }

    [Fact]
    public void SuccessfulImport_ShowsTraceSummaryAndVisibleCount()
    {
        var component = RenderAndLoad();

        component.WaitForAssertion(() =>
        {
            var summary = component.Find("details.trace-summary");
            Assert.False(summary.HasAttribute("open"));
            Assert.Contains("3 visible of 3", summary.TextContent);
            Assert.Contains("Sessions with findings", summary.TextContent);
            Assert.Contains("Failing hosts", summary.TextContent);
            Assert.Contains("High-impact findings", summary.TextContent);
            Assert.Contains("M365.Test.Failure", summary.TextContent);
            Assert.Contains("Authentication", summary.TextContent);
        });

        component.Find("input.search-box").Input("missing");

        component.WaitForAssertion(() =>
            Assert.Contains(
                "0 visible of 3",
                component.Find("details.trace-summary").TextContent));
    }

    [Fact]
    public void ImportQuality_ShowsRecoverableWarningsAndCounts()
    {
        _importer.ImportReport = _ => Task.FromResult(
            TraceImportResult.Create(
                _importer.Sessions,
                [
                    new TraceImportIssue(
                        TraceImportIssueCategory.SkippedSession,
                        "Session 4 was skipped.",
                        "4")
                ],
                sourceSessionCount: 4));
        var component = RenderAndLoad();

        component.WaitForAssertion(() =>
        {
            var quality = component.Find("details.import-quality");
            Assert.True(quality.HasAttribute("open"));
            Assert.Contains("3 of 4 sessions imported", quality.TextContent);
            Assert.Contains("Source sessions4", quality.TextContent);
            Assert.Contains("Imported3", quality.TextContent);
            Assert.Contains("Skipped1", quality.TextContent);
            Assert.Contains("Session 4 was skipped.", quality.TextContent);
            Assert.Contains(
                "import-quality-warning",
                quality.GetAttribute("class"));
        });
    }

    [Fact]
    public void ImportQuality_DoesNotRenderForCleanImport()
    {
        var component = RenderAndLoad();

        component.WaitForAssertion(() =>
            Assert.Empty(component.FindAll("details.import-quality")));
    }

    [Fact]
    public void UnsupportedFile_ShowsActionableError()
    {
        _importer.CanImportFile = false;
        var component = _context.Render<Home>();

        Upload(component, "sample.txt");

        component.WaitForAssertion(() =>
        {
            Assert.Contains("Unable to open the trace.", component.Markup);
            Assert.Contains(
                "Select a supported .har or .saz trace file.",
                component.Markup);
        });
    }

    [Fact]
    public void FailedImport_CanRecoverByOpeningAnotherTrace()
    {
        var attempts = 0;
        _importer.Import = _ =>
        {
            attempts++;
            return attempts == 1
                ? Task.FromException<IReadOnlyList<TraceSession>>(
                    new TraceImportException("The trace is malformed."))
                : Task.FromResult(_importer.Sessions);
        };
        var component = _context.Render<Home>();

        Upload(component, "broken.har");
        component.WaitForAssertion(() =>
            Assert.Contains("The trace is malformed.", component.Markup));

        Upload(component, "recovered.har");

        component.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Unable to open the trace.", component.Markup);
            Assert.Contains("recovered.har", component.Markup);
            Assert.Equal(3, component.FindAll("tbody tr").Count);
        });
    }

    [Fact]
    public void PasswordProtectedSaz_ShowsPromptAndCanBeCancelled()
    {
        _importer.Import = options =>
            options?.Password is null
                ? Task.FromException<IReadOnlyList<TraceSession>>(
                    new SazPasswordRequiredException())
                : Task.FromResult(_importer.Sessions);
        var component = _context.Render<Home>();

        Upload(component, "protected.saz");

        component.WaitForAssertion(() =>
        {
            Assert.Contains("Password required", component.Markup);
            Assert.Contains("protected.saz", component.Markup);
        });

        component.FindAll("button")
            .Single(button => button.TextContent == "Cancel")
            .Click();

        component.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Password required", component.Markup);
            Assert.Contains("Open a HAR or SAZ trace", component.Markup);
        });
    }

    [Fact]
    public void PasswordRetry_PassesPasswordAndClearsPrompt()
    {
        _importer.Import = options =>
            options?.Password is null
                ? Task.FromException<IReadOnlyList<TraceSession>>(
                    new SazPasswordRequiredException())
                : Task.FromResult(_importer.Sessions);
        var component = _context.Render<Home>();

        Upload(component, "protected.saz");
        component.WaitForElement("input.password-input").Input("correct horse");
        component.Find("form").Submit();

        component.WaitForAssertion(() =>
        {
            Assert.Equal("correct horse", _importer.Options.Last()?.Password);
            Assert.DoesNotContain("Password required", component.Markup);
            Assert.Contains("protected.saz", component.Markup);
            Assert.Equal(3, component.FindAll("tbody tr").Count);
        });
    }

    [Fact]
    public void WrongPassword_ShowsErrorAndClearsPasswordField()
    {
        _importer.Import = options =>
            options?.Password is null
                ? Task.FromException<IReadOnlyList<TraceSession>>(
                    new SazPasswordRequiredException())
                : Task.FromException<IReadOnlyList<TraceSession>>(
                    new TraceImportException("The SAZ password is incorrect."));
        var component = _context.Render<Home>();

        Upload(component, "protected.saz");
        component.WaitForElement("input.password-input").Input("wrong");
        component.Find("form").Submit();

        component.WaitForAssertion(() =>
        {
            Assert.Contains("The SAZ password is incorrect.", component.Markup);
            Assert.Equal(
                string.Empty,
                component.Find("input.password-input").GetAttribute("value"));
            Assert.Contains("Password required", component.Markup);
        });
    }

    [Fact]
    public void FilterClearButton_RestoresAllImportedSessions()
    {
        var component = RenderAndLoad();

        component.Find("input.search-box").Input("missing");

        component.WaitForAssertion(() =>
        {
            Assert.Empty(GetVisibleSessionIds(component));
            Assert.Contains(
                "No sessions match the active filters.",
                component.Markup);
            Assert.NotNull(component.Find("button.search-clear-button"));
        });

        component.Find("button.search-clear-button").Click();

        component.WaitForAssertion(() =>
        {
            Assert.Equal(3, component.FindAll("tbody tr").Count);
            Assert.Empty(component.FindAll("button.search-clear-button"));
        });
    }

    [Theory]
    [InlineData("LOGIN.MICROSOFTONLINE.COM", 2)]
    [InlineData("/alpha", 2)]
    [InlineData("POST", 2)]
    [InlineData("503", 2)]
    [InlineData("Service Unavailable", 2)]
    [InlineData("Severe", 2)]
    [InlineData("Authentication", 2)]
    [InlineData("OAuth", 2)]
    [InlineData("FrontEnd-02", 2)]
    [InlineData("M365.Test.Failure", 2)]
    [InlineData("Token request failed", 2)]
    [InlineData("Expired token", 2)]
    [InlineData("Retry sign in", 2)]
    [InlineData("Diagnostic status header", 2)]
    public void FreeTextFilter_MatchesAllSupportedFields(
        string filter,
        int expectedSessionId)
    {
        var component = RenderAndLoad();

        component.Find("input.search-box").Input(filter);

        component.WaitForAssertion(() =>
            Assert.Equal([expectedSessionId], GetVisibleSessionIds(component)));
    }

    [Theory]
    [InlineData("X-Search-Request")]
    [InlineData("request-header-value")]
    [InlineData("response body search marker")]
    public void SessionSearch_MatchesHeadersAndBodiesAfterDebounce(string search)
    {
        _importer.Sessions =
        [
            CreateSession(
                1,
                "POST",
                "https://example.test/search",
                200,
                "OK",
                50,
                requestHeaders:
                [
                    new TraceHeader(
                        "X-Search-Request",
                        "request-header-value")
                ],
                responseContent: new TraceContent(
                    "Response body search marker",
                    "text/plain",
                    27,
                    false,
                    false)),
            CreateSession(
                2,
                "GET",
                "https://example.test/other",
                200,
                "OK",
                50)
        ];
        var component = RenderAndLoad();

        component.Find("input.search-box").Input(search);

        component.WaitForAssertion(
            () => Assert.Equal([1], GetVisibleSessionIds(component)),
            TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void StructuredFilters_CombineAcrossCategoriesAndRenderChips()
    {
        var component = RenderAndLoad();

        SelectFilterOption(component, "severity", "Severe");
        SelectFilterOption(component, "severity", "Warning");

        component.WaitForAssertion(() =>
        {
            Assert.Equal([2, 3], GetVisibleSessionIds(component));
            Assert.Contains(
                "Severity: Severe + Warning",
                component.Find(".filter-chip").TextContent);
        });

        SelectFilterOption(component, "method", "POST");

        component.WaitForAssertion(() =>
        {
            Assert.Equal([2], GetVisibleSessionIds(component));
            Assert.Equal(2, component.FindAll(".filter-chip").Count);
            Assert.Equal(
                "1 of 3 sessions",
                NormalizeWhitespace(
                    component.Find(".filter-result-count").TextContent));
        });
    }

    [Fact]
    public void SummaryDrillThrough_AppliesFilterAndChip()
    {
        var component = RenderAndLoad();

        ClickSummaryCount(component, "5xx");

        component.WaitForAssertion(() =>
        {
            Assert.Equal([2], GetVisibleSessionIds(component));
            Assert.Contains(
                "Status: 5xx",
                component.Find(".filter-chip").TextContent);
            Assert.Contains(
                "1 visible of 3",
                component.Find("details.trace-summary").TextContent);
        });

        component.Find(".filter-chip").Click();

        component.WaitForAssertion(() =>
        {
            Assert.Equal([1, 2, 3], GetVisibleSessionIds(component));
            Assert.Empty(component.FindAll(".filter-chip"));
        });
    }

    [Fact]
    public void ClearAll_RemovesFreeTextAndStructuredFilters()
    {
        var component = RenderAndLoad();

        ClickSummaryCount(component, "5xx");
        component.Find("input.search-box").Input("login");

        component.WaitForAssertion(() =>
            Assert.Equal([2], GetVisibleSessionIds(component)));

        component.Find("button.filter-clear-all").Click();

        component.WaitForAssertion(() =>
        {
            Assert.Equal([1, 2, 3], GetVisibleSessionIds(component));
            Assert.Equal(
                string.Empty,
                component.Find("input.search-box").GetAttribute("value"));
            Assert.Empty(component.FindAll(".filter-chip"));
        });
    }

    [Fact]
    public void FilterRemovingSelection_SelectsNextVisibleSession()
    {
        var component = RenderAndLoad();
        component.Find("tbody tr[data-session-id='2']").Click();

        ClickSummaryCount(component, "4xx");

        component.WaitForAssertion(() =>
        {
            Assert.Equal([3], GetVisibleSessionIds(component));
            Assert.Equal(
                "3",
                component.Find("tbody tr.selected td").TextContent);
            Assert.Contains("DELETE graph.microsoft.com", component.Markup);
        });
    }

    [Fact]
    public void CombinedFiltersWithNoMatches_ShowFilteredEmptyState()
    {
        var component = RenderAndLoad();

        ClickSummaryCount(component, "5xx");
        component.Find("input.search-box").Input("graph");

        component.WaitForAssertion(() =>
        {
            Assert.Empty(GetVisibleSessionIds(component));
            Assert.Contains(
                "No sessions match the active filters.",
                component.Markup);
            Assert.Contains(
                "Select a session to inspect its request and response.",
                component.Markup);
            Assert.DoesNotContain(
                "Open a HAR or SAZ trace",
                component.Markup);
        });
    }

    [Theory]
    [InlineData("#", new[] { 1, 2, 3 }, new[] { 3, 2, 1 })]
    [InlineData("Analysis", new[] { 1, 3, 2 }, new[] { 2, 3, 1 })]
    [InlineData("Status", new[] { 1, 3, 2 }, new[] { 2, 3, 1 })]
    [InlineData("Method", new[] { 3, 1, 2 }, new[] { 2, 1, 3 })]
    [InlineData("Host", new[] { 3, 2, 1 }, new[] { 1, 2, 3 })]
    [InlineData("Path", new[] { 2, 3, 1 }, new[] { 1, 3, 2 })]
    [InlineData("Duration", new[] { 2, 3, 1 }, new[] { 1, 3, 2 })]
    public void SortButtons_OrderSessionsAndExposeAriaState(
        string column,
        int[] ascending,
        int[] descending)
    {
        var component = RenderAndLoad();

        if (column != "#")
        {
            ClickSortButton(component, column);
        }

        component.WaitForAssertion(() =>
        {
            Assert.Equal(ascending, GetVisibleSessionIds(component));
            Assert.Equal("ascending", GetSortAriaState(component, column));
        });

        ClickSortButton(component, column);

        component.WaitForAssertion(() =>
        {
            Assert.Equal(descending, GetVisibleSessionIds(component));
            Assert.Equal("descending", GetSortAriaState(component, column));
        });
    }

    [Fact]
    public void SelectingSession_UpdatesSelectedRowAndDetails()
    {
        var component = RenderAndLoad();

        component.FindAll("tbody tr").Single(row =>
            row.QuerySelector("td")?.TextContent == "3").Click();

        component.WaitForAssertion(() =>
        {
            var selectedRow = component.Find("tbody tr.selected");
            Assert.Equal("3", selectedRow.QuerySelector("td")?.TextContent);
            Assert.Equal("0", selectedRow.GetAttribute("tabindex"));
            Assert.Equal("true", selectedRow.GetAttribute("aria-selected"));
            Assert.Contains("DELETE graph.microsoft.com", component.Markup);
            Assert.Contains("Unauthorized", component.Markup);
        });
    }

    [Fact]
    public void DiagnosticHeaders_AreElevatedWithoutHidingFullHeaders()
    {
        _importer.Sessions =
        [
            CreateSession(
                1,
                "GET",
                "https://example.test/diagnostics",
                429,
                "Too Many Requests",
                50,
                requestHeaders:
                [
                    new TraceHeader("X-MS-Request-ID", "request-123"),
                    new TraceHeader("Accept", "application/json")
                ],
                responseHeaders:
                [
                    new TraceHeader("retry-after", "30"),
                    new TraceHeader("Content-Type", "application/json")
                ])
        ];

        var component = RenderAndLoad();

        component.WaitForAssertion(() =>
        {
            var diagnosticRows = component.FindAll(
                ".diagnostic-header-row");
            Assert.Equal(2, diagnosticRows.Count);
            Assert.Contains("Request", diagnosticRows[0].TextContent);
            Assert.Contains("X-MS-Request-ID", diagnosticRows[0].TextContent);
            Assert.Contains("request-123", diagnosticRows[0].TextContent);
            Assert.Contains("Response", diagnosticRows[1].TextContent);
            Assert.Contains("retry-after", diagnosticRows[1].TextContent);
            Assert.Contains("30", diagnosticRows[1].TextContent);
            Assert.Contains("Accept", component.FindAll(".header-list")[0].TextContent);
            Assert.Contains(
                "Content-Type",
                component.FindAll(".header-list")[1].TextContent);
        });
    }

    [Fact]
    public void FindingDetails_RenderEvidenceRecommendationAndSafeLink()
    {
        var component = RenderAndLoad();

        component.FindAll("tbody tr").Single(row =>
            row.QuerySelector("td")?.TextContent == "2").Click();

        component.WaitForAssertion(() =>
        {
            Assert.Contains("Ruleset analysis", component.Markup);
            Assert.Empty(component.FindAll(".analysis-heading .severity-badge"));
            Assert.Empty(component.FindAll("[data-session-navigation]"));
            Assert.Empty(component.FindAll("[data-copy-action]"));
            Assert.Contains("Token request failed", component.Markup);
            Assert.Contains("M365.Test.Failure", component.Markup);
            Assert.Contains("Expired token", component.Markup);
            Assert.Contains("Retry sign in", component.Markup);
            Assert.Contains("Diagnostic status header", component.Markup);

            var link = component.Find("a.finding-link");
            Assert.Equal("Troubleshooting", link.TextContent);
            Assert.Equal("https://learn.example.test/", link.GetAttribute("href"));
            Assert.Equal("noopener noreferrer", link.GetAttribute("rel"));
        });
    }

    [Fact]
    public void TrafficSelector_ShowsOnlyRequestedDetails()
    {
        var component = RenderAndLoad();

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

    [Fact]
    public void RequestAndResponseBodies_AreExpandedByDefault()
    {
        _importer.Sessions =
        [
            CreateSession(
                1,
                "POST",
                "https://example.test/body",
                200,
                "OK",
                50,
                requestContent: new TraceContent(
                    "request body",
                    "text/plain",
                    12,
                    false,
                    false))
        ];

        var component = RenderAndLoad();

        component.WaitForAssertion(() =>
        {
            var bodySummaries = component.FindAll("summary")
                .Where(summary =>
                    summary.TextContent is "Request body" or "Response body")
                .ToArray();

            Assert.Equal(2, bodySummaries.Length);
            Assert.All(
                bodySummaries,
                summary => Assert.True(
                    summary.ParentElement?.HasAttribute("open")));
        });
    }

    [Fact]
    public void MissingHeadersAndBodies_AreRepresentedClearly()
    {
        _importer.Sessions =
        [
            CreateSession(
                1,
                "GET",
                "https://example.test/empty",
                204,
                "No Content",
                50,
                requestHeaders: [],
                responseHeaders: [],
                includeResponseContent: false)
        ];

        var component = RenderAndLoad();

        component.WaitForAssertion(() =>
        {
            Assert.Equal(
                2,
                component.FindAll("p.muted")
                    .Count(element =>
                        element.TextContent == "No headers were recorded."));
            var requestBody = component.FindAll("summary")
                .Single(summary => summary.TextContent == "Request body");
            Assert.True(requestBody.ParentElement?.HasAttribute("open"));
            Assert.Equal(
                "No request body was recorded for this session.",
                component.Find(".body-unavailable").TextContent.Trim());
            Assert.DoesNotContain("Response body", component.Markup);
        });
    }

    [Fact]
    public void UpdateAvailable_ShowsReleaseLink()
    {
        _httpHandler.Response = _ => JsonResponse(
            """
            {
              "tag_name": "v99.0.0",
              "html_url": "https://github.test/releases/v99.0.0"
            }
            """);

        var component = _context.Render<Home>();

        component.WaitForAssertion(() =>
        {
            var link = component.Find(".version-card a");
            Assert.Contains("Version 99.0.0 available", link.TextContent);
            Assert.Equal(
                "https://github.test/releases/v99.0.0",
                link.GetAttribute("href"));
        });
    }

    [Fact]
    public void MissingPublishedRelease_IsReported()
    {
        _httpHandler.Response = _ =>
            new HttpResponseMessage(HttpStatusCode.NotFound);

        var component = _context.Render<Home>();

        component.WaitForAssertion(() =>
            Assert.Contains("No published release yet", component.Markup));
    }

    [Fact]
    public void UnavailableUpdateCheck_DoesNotBlockAnalysis()
    {
        _httpHandler.Response = _ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var component = RenderAndLoad();

        component.WaitForAssertion(() =>
        {
            Assert.Contains("Update check unavailable", component.Markup);
            Assert.Equal(3, component.FindAll("tbody tr").Count);
        });
    }

    public void Dispose() => _context.Dispose();

    private IRenderedComponent<Home> RenderAndLoad(string fileName = "sample.har")
    {
        var component = _context.Render<Home>();
        Upload(component, fileName);
        component.WaitForAssertion(() =>
            Assert.Equal(_importer.Sessions.Count, component.FindAll("tbody tr").Count));
        return component;
    }

    private static void Upload(
        IRenderedComponent<Home> component,
        string fileName)
    {
        component.FindComponents<InputFile>().First().UploadFiles(
            InputFileContent.CreateFromText(
                "{}",
                fileName,
                contentType: "application/json"));
    }

    private static int[] GetVisibleSessionIds(
        IRenderedComponent<Home> component) =>
        component.FindAll("tbody tr[data-session-id]")
            .Select(row => int.Parse(
                row.QuerySelector("td")!.TextContent,
                System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

    private static void ClickSortButton(
        IRenderedComponent<Home> component,
        string column) =>
        component.FindAll("thead button")
            .Single(element => element.TextContent == column)
            .Click();

    private static void ClickSummaryCount(
        IRenderedComponent<Home> component,
        string label) =>
        component.FindAll("button.summary-count-button")
            .Single(button =>
                button.TextContent.Contains(
                    label,
                    StringComparison.Ordinal))
            .Click();

    private static void SelectFilterOption(
        IRenderedComponent<Home> component,
        string menu,
        string label)
    {
        var option = component
            .FindAll($"details[data-filter-menu='{menu}'] label")
            .Single(element =>
                element.TextContent.Trim().Equals(
                    label,
                    StringComparison.Ordinal));
        option.QuerySelector("input")!.Change(true);
    }

    private static string NormalizeWhitespace(string value) =>
        string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

    private static string? GetSortAriaState(
        IRenderedComponent<Home> component,
        string column) =>
        component.FindAll("thead button")
            .Single(element => element.TextContent == column)
            .ParentElement
            ?.GetAttribute("aria-sort");

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };

    private sealed class StubTraceImporter : ITraceImporter
    {
        public bool CanImportFile { get; set; } = true;

        public IReadOnlyList<TraceSession> Sessions { get; set; } =
        [
            CreateSession(
                1,
                "GET",
                "https://outlook.office.com/zeta",
                200,
                "OK",
                300),
            CreateSession(
                2,
                "POST",
                "https://login.microsoftonline.com/alpha",
                503,
                "Service Unavailable",
                100),
            CreateSession(
                3,
                "DELETE",
                "https://graph.microsoft.com/beta",
                401,
                "Unauthorized",
                200)
        ];

        public List<TraceImportOptions?> Options { get; } = [];

        public Func<TraceImportOptions?, Task<IReadOnlyList<TraceSession>>>? Import
        {
            get;
            set;
        }

        public Func<TraceImportOptions?, Task<TraceImportResult>>? ImportReport
        {
            get;
            set;
        }

        public string FormatName => "Test trace";

        public bool CanImport(string fileName) => CanImportFile;

        public Task<IReadOnlyList<TraceSession>> ImportAsync(
            Stream stream,
            TraceImportOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Options.Add(options);
            return Import?.Invoke(options)
                ?? Task.FromResult(Sessions);
        }

        public async Task<TraceImportResult> ImportWithReportAsync(
            Stream stream,
            TraceImportOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (ImportReport is not null)
            {
                Options.Add(options);
                return await ImportReport(options);
            }

            return TraceImportResult.Create(
                await ImportAsync(stream, options, cancellationToken));
        }
    }

    private sealed class TestAnalysisRule : ITraceRule
    {
        public string Id => "M365.Test.Analysis";

        public AnalysisPhase Phase => AnalysisPhase.BroadChecks;

        public int Order => 1;

        public bool AppliesTo(AnalysisContext context) => true;

        public void Evaluate(AnalysisContext context)
        {
            switch (context.Session.Id)
            {
                case 2:
                    context.SetSessionType("Authentication", 10);
                    context.SetAuthentication("OAuth", 10);
                    context.SetResponseServer("FrontEnd-02", 10);
                    context.AddFinding(new TraceFinding
                    {
                        RuleId = "M365.Test.Failure",
                        Title = "Token request failed",
                        Severity = TraceSeverity.Severe,
                        Description = "Expired token caused the endpoint error.",
                        Evidence = "Diagnostic status header was 503.",
                        Recommendation = "Retry sign in after validating identity.",
                        Links =
                        [
                            new FindingLink(
                                "Troubleshooting",
                                new Uri("https://learn.example.test/"))
                        ]
                    });
                    break;
                case 3:
                    context.SetSessionType("Microsoft Graph", 10);
                    context.SetAuthentication("Bearer", 10);
                    context.SetResponseServer("Graph-01", 10);
                    context.RaiseSeverity(TraceSeverity.Warning);
                    break;
                default:
                    context.SetSessionType("Exchange Online", 10);
                    context.SetAuthentication("None", 10);
                    context.SetResponseServer("FrontEnd-01", 10);
                    context.RaiseSeverity(TraceSeverity.Normal);
                    break;
            }
        }
    }

    private sealed class StubHttpClientFactory(
        StubHttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.github.test/")
            };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Response { get; set; } =
            _ => JsonResponse(
                """
                {
                  "tag_name": "v1.0.2",
                  "html_url": "https://github.test/release"
                }
                """);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(Response(request));
    }

    private static TraceSession CreateSession(
        int id,
        string method,
        string url,
        int statusCode,
        string statusText,
        double durationMilliseconds,
        IReadOnlyList<TraceHeader>? requestHeaders = null,
        IReadOnlyList<TraceHeader>? responseHeaders = null,
        TraceContent? requestContent = null,
        TraceContent? responseContent = null,
        bool includeResponseContent = true) =>
        new()
        {
            Id = id,
            StartedAt = DateTimeOffset.Parse("2026-09-23T10:00:00-04:00"),
            Method = method,
            Url = new Uri(url),
            StatusCode = statusCode,
            StatusText = statusText,
            Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
            RequestHeaders = requestHeaders
                ?? [new TraceHeader("Accept", "application/json")],
            ResponseHeaders = responseHeaders
                ?? [new TraceHeader("Content-Type", "application/json")],
            RequestContent = requestContent,
            ResponseContent = includeResponseContent
                ? responseContent
                    ?? new TraceContent(
                        """{"status":"ok"}""",
                        "application/json",
                        15,
                        false,
                        false)
                : null
        };
}
