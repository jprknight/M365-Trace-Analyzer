# M365 Trace Analyzer support-engineering roadmap

## Problem and approach

M365 Trace Analyzer already provides a strong local foundation: bounded HAR and SAZ import, encrypted SAZ support, 151 migrated classifications, session filtering and sorting, request/response inspection, secure body previews, update checking, and a self-contained local web experience.

The remaining gaps are less about basic file viewing and more about helping a support engineer move efficiently from a large trace to a defensible diagnosis. The roadmap should therefore prioritize trace-wide triage, richer source fidelity, repeatable investigation workflows, safe sharing, and performance before adding optional automation surfaces.

## Current-state assessment

### Strong existing capabilities

- Local-only HAR and SAZ analysis with no trace upload.
- Bounded parsing and archive safety limits.
- ZipCrypto, AES-128, and AES-256 password-protected SAZ support.
- Deterministic per-session analysis with 151 classifications.
- Search across URLs, statuses, classifications, findings, evidence, and recommendations.
- Sortable session grid with request, response, or combined detail views.
- Raw, JSON, HTML, image, and XML body inspectors.
- Responsive viewport layout and accessible controls.
- Unified application versioning and release checking.

### Original highest-value gaps and current status

1. **Trace-wide triage is delivered.** The collapsible summary covers severe findings, failing hosts, status distribution, slowest sessions, authentication patterns, and high-impact rules, with drill-through into matching sessions.
2. **Composable filtering is delivered.** Free text combines with structured filters for severity, status family or exact code, method, host, duration, session type, authentication, finding rule, and finding presence.
3. **Source metadata fidelity is limited.** The normalized model omits protocol version, connection/process information, client/server IPs, TLS details, HAR timing phases, request/response byte sizes, redirect/cache data, and most SAZ metadata flags.
4. **Timing analysis is shallow.** Only total duration is retained; there is no timeline or waterfall for DNS, connect, TLS, send, wait, receive, queueing, or repeated/retry patterns.
5. **Investigation output is not portable.** Findings cannot be copied or exported as a support-ready report, and there is no redaction policy for secrets and customer data.
6. **Large-trace usability is unproven.** Import has limits, but there is no progress, cancellation, virtualization/paging, benchmark suite, or defined performance budget for 10,000-100,000 sessions.
7. **Import diagnostics are all-or-nothing.** A malformed session can fail an import without a structured summary of skipped, truncated, partially decoded, or unsupported content.
8. **Correlation workflows are manual.** The UI does not elevate correlation headers, group related calls, identify redirect/retry chains, or navigate between matching sessions.
9. **Investigation state is ephemeral.** There are no bookmarks, notes, marked sessions, or saved filter state for a support engineer working through a trace.
10. **Current UI behavior has component coverage, but future workflows remain untested.** Component tests cover import success and recovery, encrypted-SAZ password handling, free-text and structured filtering, active chips, summary drill-through, selection recovery, every sortable column, finding rendering, request/response view modes, update states, and body inspectors. A packaged Chromium smoke test covers HAR upload, filtering, filter clearing, and detail selection. Export, keyboard workflows, import-quality reporting, and representative SAZ browser tests require coverage when those features are implemented.

## Product decisions

- Deliver this as a **prioritized roadmap**, not one monolithic implementation.
- Exported reports are **sanitized by default**. Raw sensitive values require explicit user opt-in and a warning.
- Keep core models and analysis services suitable for a future MCP/API adapter, but **do not implement MCP or an external API in this roadmap**.
- Define and benchmark both ordinary traces and large traces because typical field size is not yet known.
- Do not add request replay or transmission features; the analyzer remains a passive, local diagnostic tool.

## Roadmap

### Implementation status — September 24, 2026

Status markers in this document apply only where the complete listed outcome has been delivered:

- `[x]` Completed and merged into `master`.
- `[ ]` Planned or only partially implemented. Partial coverage is described inline.

No Phase 1, Phase 2, or Phase 3 feature set is complete yet. Work completed since this roadmap was written is concentrated in release engineering and regression protection:

- Release `v1.0.2` delivered the encrypted SAZ, UI, port, classification, and unified-versioning work that formed the roadmap baseline.
- CI now validates formatting, warning-free builds, 161 solution tests, a 40% aggregate coverage floor, vulnerable NuGet packages, and a self-contained Windows package.
- A packaged Chromium test validates application startup, HAR upload, filtering, filter clearing, and request-only detail selection.
- CodeQL, Dependabot, tag-driven release packaging, checksums, and artifact provenance are configured.
- Importer boundary, malformed-input, cancellation, archive safety, component, secure body-preview, and packaged-browser tests have been expanded.
- XML display now handles diagnostic responses containing prohibited numeric character references without altering Raw content.
- JSON display now handles a leading UTF-8 BOM without altering Raw content.
- Phase 1 foundation now separates immutable session query state, filtering, sorting, selection, and visible-session navigation from `Home.razor` into unit-tested services without changing the current UI.
- The Fiddler-style workspace presentation is split into focused `SessionTable` and `SessionDetailPanel` components while `Home.razor` remains the import and composition root.
- A collapsible trace summary now reports trace timing, severity and HTTP status distributions, findings, slow sessions, failing hosts, high-impact rule IDs, slowest sessions, and authentication classifications.
- Structured filters now combine with free text using explicit AND/OR semantics, display removable chips and visible counts, reconcile selection when results change, and support summary-driven drill-through.

### Phase 1 — Support-engineer triage essentials

## Phase 1 detailed design

Phase 1 is intended to make the existing analyzer substantially more useful without changing how HAR or SAZ files are parsed. It operates on the normalized sessions and analysis results already produced today. This keeps the first phase lower risk: importer fidelity and timeline work remain in later phases.

### Expected support-engineer workflow

1. The engineer opens a HAR or SAZ exactly as they do today.
2. The application presents a compact trace summary above or alongside the session workspace.
3. The engineer immediately sees whether the trace contains severe findings, HTTP failures, authentication issues, repeated classifications, or unusually slow calls.
4. Selecting a summary item applies a visible structured filter to the existing session grid.
5. The engineer combines summary-driven filters with free-text search and sorting.
6. The engineer navigates matching sessions, copies relevant evidence, and optionally marks important findings.
7. The engineer exports a sanitized diagnostic report suitable for attaching to a support case or sharing with another engineer.

### Proposed screen changes

#### Trace summary panel

Add a collapsible summary region between the application header and the current two-panel workspace. It should avoid permanently reducing the session grid height when the engineer does not need it.

The first version should contain:

- Total sessions and currently visible sessions.
- Trace start, trace end, and elapsed trace window.
- Counts for Severe, Concerning, Warning, Normal, and other analysis states.
- HTTP status-family counts: no response/0, 2xx, 3xx, 4xx, and 5xx.
- Number of sessions with findings.
- Number of slow sessions using the existing rule thresholds.
- Top failing hosts.
- Most frequent concerning/severe rule IDs.
- Top slowest sessions.
- Authentication classification counts.

Each count or row should be a button or link that applies the corresponding filter. For example, selecting `5xx · 42` filters the grid to 5xx sessions; selecting a host filters to that exact host.

The summary should not claim a root cause. It summarizes observed evidence and existing rule results.

#### Filter bar

Keep the current free-text box and clear `×` control. Add a structured filter bar with:

- Severity multi-select.
- Status family and optional exact status code.
- Method.
- Host.
- Duration preset/range.
- Session type.
- Authentication classification.
- Has findings / No findings.

Applied filters appear as individual chips, for example:

```text
[Severity: Concerning + Severe ×] [Status: 5xx ×] [Host: outlook.office365.com ×]
```

The filter bar also shows:

```text
42 of 8,316 sessions
```

A `Clear all` action removes structured filters and free text. Clicking a summary metric replaces or augments the relevant filter category rather than building hidden state.

#### Investigation actions

Add a small action area in the session detail panel:

- Previous match and Next match.
- Copy URL.
- Copy request headers.
- Copy response headers.
- Copy request body when available.
- Copy response body when available.
- Copy finding summary.

Copy actions should provide a brief success state and should not silently copy truncated content as though it were complete. When content is truncated, copied text must say so.

Add a focused diagnostic-header section above the full header lists when any recognized values are present. Initial recognized headers should include:

- `request-id`
- `client-request-id`
- `x-ms-request-id`
- `x-ms-correlation-id`
- `x-feserver`
- `x-beserver`
- `x-calculatedbetarget`
- `x-diaginfo`
- `x-ms-diagnostics`
- `retry-after`
- `location`
- `www-authenticate`

Header names remain case-insensitive. This section is only a convenience view; the complete headers remain available below it.

#### Export dialog

Add an `Export report` action after a trace is loaded. The dialog should allow the engineer to choose:

- All sessions currently matching the filters.
- Only sessions with Warning-or-higher findings.
- Only specifically selected/marked sessions, once marking is available.
- HTML report.
- JSON report.

The dialog shows that sanitization is enabled and lists the data categories that will be removed or masked. An advanced explicit opt-in can include raw values, but it must require a confirmation each time and must never become a persisted default.

### Engineering changes

#### Separate page state from presentation

`Home.razor` currently handles update checks, file selection, password retry, import, analysis, filtering, sorting, session selection, and most rendering. Phase 1 should avoid adding all new logic directly to this component.

Introduce:

- `TraceWorkspaceState` — loaded file identity, analyzed sessions, selected session, sort state, active filters, and navigation among visible sessions.
- `SessionFilter` or `SessionQuery` — immutable structured filter values plus free text.
- `SessionQueryService` — filtering, sorting, visible counts, and previous/next matching-session resolution.
- `TraceSummaryService` — aggregate counts and ranked summary groups.
- `TraceSummary` — immutable result model consumed by the UI.
- `ClipboardService` — browser clipboard interop with explicit success/failure.
- `TraceReportService` — converts the current workspace selection into a sanitized export model.
- `TraceRedactionService` — central policy for sensitive names and values.

The exact namespaces may follow the existing project conventions, but filtering, aggregation, redaction, and report creation must be unit-testable without rendering Blazor components.

#### Suggested UI component split

Refactor the main page into focused components:

- `TraceSummaryPanel.razor`
- `TraceFilterBar.razor`
- `SessionTable.razor`
- `SessionDetailPanel.razor`
- `DiagnosticHeaders.razor`
- `TraceExportDialog.razor`

`Home.razor` remains the composition root for file opening and top-level workspace state. This split is not cosmetic; it prevents Phase 1 from making an already large page difficult to test and maintain.

#### Summary calculations

Summary calculations should be deterministic and computed from the analyzed session collection:

- Severity and finding counts come from `TraceAnalysisResult`.
- Status family comes from `StatusCode`.
- Host, method, authentication, session type, and response server come from existing fields.
- Slowest sessions use `Duration`.
- Trace window uses minimum `StartedAt` and maximum `StartedAt + Duration`.
- Repeated finding groups use stable `RuleId`, not localized titles.

The summary is recomputed after a trace is loaded. Lightweight visible-count information may update after filtering, but the original trace-wide totals must remain distinguishable from filtered totals.

#### Structured filtering behavior

Filter categories combine with logical AND. Multiple values inside one category combine with logical OR.

Example:

```text
(Severity is Severe OR Concerning)
AND (Status is 500 OR 503)
AND (Host is outlook.office365.com)
AND free text contains "authentication"
```

Filtering remains case-insensitive. Exact host and rule selections from the summary should not use substring matching. Sorting remains independent of filtering.

If the selected session is removed from the visible result:

- Select the next visible session by original trace order.
- If none follows, select the previous visible session.
- If no sessions remain, show a clear empty-filter result rather than the initial open-file state.

#### Redaction policy

The sanitized export must redact values from at least:

- `Authorization`
- `Proxy-Authorization`
- `Cookie`
- `Set-Cookie`
- Authentication tokens and bearer values found in text.
- Password, secret, token, assertion, and code-like query parameters.
- Common Microsoft identity tokens and SAML assertions.
- User-configurable additional header names.

Sanitization should preserve diagnostic usefulness where possible:

- Keep header names while replacing sensitive values with `[REDACTED]`.
- Preserve host and path by default.
- Redact sensitive query parameter values without removing non-sensitive parameter names.
- Replace detected email-like values and user principal names with stable report-local placeholders such as `[USER-1]`, allowing repeated occurrences to remain correlatable.
- Mark omitted bodies and truncated content explicitly.
- Do not include SAZ passwords, temporary paths, or application-local filesystem paths.

The report model should contain only fields deliberately approved for export. It must not serialize `TraceSession` directly.

#### HTML report content

The HTML export should be a self-contained static file with:

- Report generation metadata and application version.
- Source filename, not full source path.
- Sanitization state and warning banner.
- Trace-wide summary.
- Applied filters.
- Finding counts grouped by severity and rule ID.
- A session table containing sanitized method, URL, status, duration, and finding summary.
- Expandable sanitized evidence for included sessions.
- No script execution and no external resources.

#### JSON report content

The JSON export should have a versioned schema and contain:

- `schemaVersion`
- `applicationVersion`
- source filename
- generated timestamp
- sanitization mode
- applied filter description
- summary object
- included session records
- findings with stable rule IDs
- redaction/truncation indicators

The JSON schema must be deterministic so it can support future CLI or MCP consumption without requiring an MCP implementation now.

### Phase 1 implementation sequence

1. [x] Extract and test the session query/filter/sort behavior from `Home.razor`.
2. [x] Introduce workspace state and split the large page into focused components without intentionally changing behavior.
3. [x] Add the trace summary service and summary panel.
4. [x] Add structured filters, active chips, counts, and summary drill-through.
5. Add previous/next navigation, diagnostic-header emphasis, and clipboard actions.
6. Add the redaction service and comprehensive redaction tests before implementing export UI.
7. Add versioned JSON export.
8. Add self-contained static HTML export.
9. Add export selection and raw-data confirmation UI.
10. Add component and browser-level tests for the complete Phase 1 workflow.

### Phase 1 acceptance criteria

- Opening existing HAR and SAZ fixtures produces the same session count and per-session findings as before.
- A support engineer can identify all Severe and Concerning sessions without writing free-text queries.
- Every trace summary metric that represents sessions can drill into the exact matching session set.
- Filter chips accurately describe all active structured filters.
- Free-text search and structured filters combine predictably.
- Previous/next navigation never moves outside the visible filtered set.
- Copy actions clearly report success or failure and indicate truncated content.
- Sanitized reports contain no configured secret headers, cookie values, bearer tokens, password-like query values, or raw SAZ passwords.
- Repeated redacted user identifiers remain correlatable through stable placeholders within one report.
- HTML reports open without network access and execute no scripts.
- JSON reports conform to a versioned deterministic schema.
- Existing importer safety limits and body-preview protections remain unchanged.
- Targeted unit, component, and browser smoke tests pass.

### Phase 1 explicitly does not include

- New HAR or SAZ metadata parsing.
- Waterfall/timeline visualization.
- Cross-session root-cause or retry-chain analysis.
- Persistent notes or sidecar project files.
- MCP server, HTTP API, or cloud service.
- Request replay, live capture, or outbound diagnostic actions.
- Performance optimizations chosen without benchmark evidence.

#### 1. Add a trace-wide analysis summary

- Introduce a trace summary service over analyzed sessions rather than placing aggregation logic in the Razor page.
- Show counts by severity, status family, host, method, authentication type, response server, and session type.
- Highlight top severe/concerning findings, repeated rule IDs, failing hosts, and slowest sessions.
- Make every summary metric actionable by applying the corresponding session filter.
- Preserve per-session analysis as the source of truth; aggregate findings must link back to matching sessions.

#### 2. Replace text-only filtering with composable filters

- Retain free-text search and its clear control.
- Add multi-select severity filtering and common quick filters such as Errors, Warnings+, Slow, Authentication, and Has findings.
- Add structured filters for status family/code, method, host, duration range, session type, and authentication classification.
- Display active filters as removable chips with a single Clear all action.
- Show visible-session count versus total-session count.
- Keep filtering and sorting in a dedicated testable query model/service.

#### 3. Add investigation ergonomics

- Add previous/next matching-session navigation.
- Add Copy URL, Copy request headers, Copy response headers, Copy body, and Copy finding actions.
- Elevate commonly useful diagnostic headers such as request IDs, correlation IDs, diagnostic headers, retry headers, authentication challenges, and redirect locations.
- Add keyboard navigation for the session grid and detail panel.
- Preserve the selected session where possible when filters or sorting change.

#### 4. Add sanitized findings export

- Define an export model separate from `TraceSession` so raw trace content is never serialized accidentally.
- Export a support-ready HTML report and a machine-readable JSON report.
- Include application version, source filename, trace time range, aggregate summary, applied filters, selected findings, and session references.
- Redact authorization headers, cookies, tokens, passwords, query-string secrets, email-like identifiers, and configurable sensitive headers by default.
- Require an explicit opt-in confirmation before including raw header/body values.
- Add a report preview that clearly marks redacted and truncated values.

### Phase 2 — Import fidelity and explainability

#### 5. Expand the normalized trace model

- Add optional fields for HTTP protocol/version, request and response sizes, client/server endpoint, process information, connection ID, TLS protocol/cipher/certificate metadata, redirect target, cache state, and source-specific metadata.
- Add a structured timing model covering blocked/queued, DNS, connect, TLS, send, wait, receive, and total duration.
- Keep all new fields optional so HAR and SAZ can expose only what each source actually contains.
- Add source provenance to each field or metadata group where ambiguity would otherwise lead engineers to assume inferred values.
- Avoid storing arbitrary unbounded metadata dictionaries in the UI-facing model; explicitly map supported diagnostic fields.

#### 6. Improve HAR fidelity

- Parse HAR HTTP version, headers/body sizes, cookies, query-string entries, cache information, redirect URL, server IP, connection ID, and detailed timings.
- Parse page references and page timing where present so sessions can be grouped by navigation.
- Preserve unknown/unsupported HAR fields only when required for future compatibility, without weakening bounds.
- Record whether body content was missing from the HAR, Base64-decoded, truncated, or unavailable.

#### 7. Improve SAZ fidelity

- Map useful `SessionTimers` values instead of retaining only start and total duration.
- Map supported session flags for client/server IP, process name and ID, HTTPS/TLS, socket/connection identifiers, protocol, and relevant Fiddler diagnostic metadata.
- Decode chunked transfer bodies before content decoding.
- Distinguish unsupported content encoding from corrupt encoded content rather than silently treating it as ordinary text.
- Preserve request-only and response-missing sessions with explicit completeness state.
- Add fixtures representing real-world HTTP/1.1, CONNECT, compressed, chunked, encrypted, request-only, and partially malformed archives.

#### 8. Add import quality reporting

- Return an import result containing sessions plus warnings, truncations, skipped entries, unsupported features, and completeness counts.
- Present a post-import quality banner with an expandable details view.
- Continue importing recoverable sessions when one archive entry is malformed, while retaining strict failure for unsafe paths, archive bombs, invalid top-level formats, and unusable archives.
- Make partial-import behavior explicit and covered by tests.

### Phase 3 — Timing and correlation workflows

#### 9. Add a scalable timeline/waterfall

- Visualize session start, duration, and timing phases against the trace time range.
- Support zoom, horizontal navigation, severity/status coloring, and synchronized selection with the session grid.
- Group or color by host, process, connection, or page where metadata exists.
- Provide a table-only accessible alternative.
- Use rendering virtualization or aggregation so large traces do not create one unrestricted DOM element per timing segment.

#### 10. Add correlation and pattern analysis

- Detect and group redirect chains, authentication challenge/response sequences, retry patterns, repeated failures, and request-ID/correlation-ID families.
- Show related-session links in the detail panel.
- Add trace-level detectors separately from per-session `ITraceRule` implementations because they require cross-session context.
- Produce trace-level findings with supporting session IDs and deterministic evidence.
- Start with conservative, explainable heuristics and confidence values; do not infer causality from timing alone.

#### 11. Add bookmarks and investigation notes

- Allow engineers to mark sessions and add local notes.
- Support a Marked sessions filter and include marked sessions in sanitized exports.
- Keep investigation state in memory by default.
- If persistence is later added, store only an explicit sidecar file chosen by the user; never modify the source HAR or SAZ.

### Phase 4 — Scale, resilience, and release readiness

#### 12. Define performance budgets and benchmarks

- Add generated benchmark fixtures for representative 1,000, 10,000, and 100,000-session traces within configured size limits.
- Measure import time, analysis time, peak managed memory, initial render time, filter latency, sort latency, and timeline interaction latency.
- Establish pass/fail budgets before selecting optimization techniques.
- Run benchmarks for both HAR and SAZ, including compressed/encrypted SAZ scenarios where practical.

#### 13. Make long operations controllable

- Add import and analysis progress states.
- Add cancellation that propagates through browser stream reading, importers, analysis, aggregation, and UI state.
- Move analysis off the UI interaction path where necessary.
- Add table virtualization or server-side paging over in-memory data based on benchmark evidence.
- Prevent stale imports from replacing newer user selections.

#### 14. Add UI and integration regression coverage

- [ ] Add component tests for filters, clear actions, sorting, session selection, request/response view modes, password flow, import warnings, and export redaction.
  - Partial: all currently implemented component workflows are covered, including import/password states, free-text and structured filtering, chips, summary drill-through, selection reconciliation, sorting, finding details, request/response modes, version states, JSON/XML formatting, HTML sandboxing, images, binary fallbacks, and truncation messaging. Import warnings and export redaction remain pending because those product features are not implemented.
- [ ] Add browser-level smoke tests for opening representative HAR, unencrypted SAZ, and encrypted SAZ files.
  - Partial: the packaged Chromium test covers HAR upload, session rendering, filtering, filter clearing, and request-only detail selection. SAZ browser coverage remains planned.
- [ ] Test accessibility semantics and keyboard workflows for the main investigation path.
- [x] Retain importer safety, ruleset determinism, and secure HTML/XML preview tests.

#### 15. Complete release engineering

- [x] Commit and review the substantial post-`v0.1.0` work before beginning roadmap implementation.
- [x] Publish a release containing the already-completed encrypted SAZ, UI, port, classification, and versioning improvements before advertising later roadmap features.
- [x] Add repeatable publish/package verification for the Windows self-contained artifact.
- [x] Keep the local-only binding guidance and verify packaged static assets in release smoke tests.
- [ ] Update README capabilities and planned-work sections at each shipped phase.
  - Partial: the README documents current trace support and links to this roadmap; future shipped phases must continue updating it.

## Architecture notes

- Extract trace state, filtering, aggregation, export, and trace-level analysis from `Home.razor`; it is already carrying import, analysis, filtering, sorting, selection, password, update, and rendering responsibilities.
- Keep `M365Trace.Core` free of Blazor dependencies and suitable for later CLI, MCP, or API adapters.
- Model per-session analysis and trace-wide analysis separately:
  - `TraceAnalysisEngine` continues deterministic per-session classification.
  - A new trace analysis service consumes the complete analyzed session collection.
- Introduce a richer import return type without breaking safety boundaries:
  - normalized sessions
  - import warnings
  - skipped/partial counts
  - source capabilities and completeness
- Keep source-specific parsing in importer projects and expose only normalized optional fields to rules and UI.
- Centralize redaction in a reusable core/service layer so every future export or automation adapter applies the same policy.

## Deferred items

- MCP server or external web API implementation.
- Cloud-hosted trace processing or trace upload.
- Live traffic capture, proxying, or request replay.
- Collaborative cloud storage.
- Automatic remediation actions.
- Broad protocol support beyond the HTTP semantics represented in HAR and SAZ.

## Validation strategy

- Unit tests for every new normalized field and malformed/partial input path.
- Golden-file tests for sanitized HTML and JSON exports.
- Trace-level analysis fixtures proving deterministic grouping and evidence.
- Component tests for triage filters and investigation state.
- Browser smoke tests for primary support-engineer workflows.
- Performance benchmarks at 1,000, 10,000, and 100,000 sessions.
- Security tests verifying redaction, bounded parsing, path safety, decompression limits, HTML sandboxing, XML restrictions, and password non-retention.

## Recommended delivery order

1. [x] Commit and release the completed current-state work.
2. [ ] Build Phase 1 triage, structured filters, ergonomics, and sanitized export.
3. [ ] Expand the core model and importer fidelity with import-quality reporting.
4. [ ] Add timeline and cross-session correlation on top of the richer model.
5. [ ] Harden scale, cancellation, UI regression coverage, and packaging.
   - Partial: UI regression coverage and Windows packaging automation are in place; scale benchmarks, end-to-end cancellation, virtualization, and broader browser fixtures remain planned.
6. [ ] Reassess whether an MCP adapter has a concrete support workflow after the standalone investigation experience is proven.
