# M365 Trace Analyzer

M365 Trace Analyzer is a local browser-based application for inspecting and analyzing Microsoft 365 HTTP traces.

The current implementation supports opening HTTP Archive (`.har`) and encrypted or unencrypted Fiddler Session Archive (`.saz`) files, viewing normalized sessions, filtering the session list, inspecting request and response headers, viewing bodies as raw text, formatted JSON, sandboxed HTML, images, or formatted XML, and running the migrated Office 365 Fiddler Extension analysis rules. Trace data is processed by the local ASP.NET Core application and is not uploaded to an external service.

## Run the standalone Windows application

The GitHub Release package is self-contained. You do not need to install the .NET SDK or .NET runtime.

1. Download the `M365-Trace-Analyzer-vX.Y.Z-win-x64.zip` asset from the latest GitHub Release.
2. Extract the complete ZIP file to a folder. Do not run the application from inside the ZIP.
3. Open the extracted folder and run `M365Trace.Web.exe`.
4. Keep the console window open while using the analyzer.
5. Open `http://localhost:8080` in a web browser.
6. Close the console window, or press `Ctrl+C` in it, when you are finished.

The executable starts a local web server that provides the browser interface. The trace is processed by that local process; it is not uploaded to a hosted analysis service.

Keep the complete extracted folder together because the executable depends on its accompanying files.

### Use another port

The application defaults to `http://localhost:8080`. If that port is already in use, open PowerShell in the extracted folder and run:

```powershell
.\M365Trace.Web.exe --urls "http://localhost:9090"
```

Then open `http://localhost:9090`.

Keep the host set to `localhost` so the analyzer is not exposed to other computers on the network.

### Troubleshooting

- If the browser cannot connect, confirm that `M365Trace.Web.exe` is still running and that its console window does not show an error.
- If Windows reports that port 8080 is already in use, start the executable with a different local port as shown above.
- If Windows SmartScreen appears, confirm that the package came from this repository's GitHub Release before choosing to run it.
- If the application window closes immediately, start it from PowerShell so the error remains visible:

```powershell
.\M365Trace.Web.exe
```

## Run from source

Running from source requires the .NET 10 SDK.

```powershell
dotnet run --project .\src\M365Trace.Web\M365Trace.Web.csproj
```

Keep the command running, then open `http://localhost:8080`.

To use another local port when running from source:

```powershell
dotnet run `
  --project .\src\M365Trace.Web\M365Trace.Web.csproj `
  -- `
  --urls "http://localhost:9090"
```

In Visual Studio Code, open **Run and Debug**, select **Run M365 Trace Analyzer**, and press `F5`. The checked-in launch configuration builds the web project, starts it on port 8080, and opens the browser automatically.

## Versioning and releases

The application version is defined in `src\M365Trace.Web\M365Trace.Web.csproj` using semantic versioning. The browser header displays the version embedded in the running assembly.

At startup, the application makes one anonymous request to the public GitHub Releases API for `jprknight/M365-Trace-Analyzer`. The result is cached for the lifetime of the local process. The UI reports:

- **Latest available version** when the running version matches or exceeds the latest published release
- **Version x.y.z available** with a link when a newer release exists
- **No published release yet** when the repository does not have a release
- **Update check unavailable** when GitHub cannot be reached

Trace import and analysis continue to work when offline or when the version check fails.

To publish a version:

1. Update `<Version>`, `<AssemblyVersion>`, and `<FileVersion>` in `M365Trace.Web.csproj`.
2. Build and test the release.
3. Create and push a matching tag such as `v0.1.0`.
4. Publish a non-draft, non-prerelease GitHub Release for that tag and attach the distributable package.

The latest stable GitHub Release is the update source of truth. Tags without a published release are not offered to users.

## Test

```powershell
dotnet test .\M365-Trace-Analyzer.sln
```

## Projects

- `M365Trace.Core` - normalized trace models
- `M365Trace.Import.Har` - bounded HAR parsing and validation
- `M365Trace.Import.Saz` - bounded SAZ/ZIP and raw HTTP session parsing
- `M365Trace.Rules` - host-independent analysis rule contracts
- `M365Trace.Web` - local Blazor browser interface
- `M365Trace.Import.Har.Tests` - importer tests and sanitized fixtures
- `M365Trace.Import.Saz.Tests` - generated SAZ archive and safety tests
- `M365Trace.Rules.Tests` - deterministic ruleset regression tests

## Supported trace files

- HAR 1.x JSON exported from browser developer tools
- Encrypted or unencrypted SAZ archives containing `raw/*_c.txt`, `*_s.txt`, and optional `*_m.xml` files

Password-protected SAZ archives support traditional ZipCrypto plus AES-128 and AES-256 encryption. Passwords are used only for the current import and are not stored. SAZ imports enforce compressed-file, expanded-size, entry-count, per-session-file, and decompressed-body limits.

## Migrated rules

The analyzer embeds the legacy session-classification data, localized ruleset strings, and a ruleset metadata manifest. Concrete `ITraceRule` implementations are discovered automatically at startup and loaded into a validated immutable catalog. Duplicate or malformed rule IDs prevent startup rather than failing during trace analysis.

Each imported session receives a cached `SessionFacts` view containing normalized URL, host, path, headers, content type, and searchable request and response text. Rules use those facts instead of repeatedly rebuilding the same values.

HTTP 200 handling is split into focused ordered rule classes under `Legacy\Http200`, covering hidden failures, Microsoft 365 protocols, content validation, and the final healthy fallback.

The current deterministic rule pipeline includes:

- Standard HTTP response classifications and unknown-status handling
- Specialized HTTP 0, 200, 302, 307, 400, 401, 403, 404, 456, 500, 502, 503, and 504 analysis
- Hidden HTTP 200 failures, including Client Access Rules, MAPI protocol/culture errors, malformed JSON, Autodiscover response validation, Free/Busy failures, and errors returned inside successful bodies
- Exchange Online and Exchange Server MAPI, EWS, Autodiscover, OWA, attachment, RPC, NSPI, REST People, suggestions, and Free/Busy classification
- Authentication classification for bearer, basic, SAML, and sessions without authentication headers
- Response-server header classification
- Apache Autodiscover, loopback, and NetLog broad checks
- Corrected warning and severe thresholds for long-running trace sessions

Rules execute in explicit phases and deterministic order. Specialized rules take precedence over generic fallbacks, and confidence values prevent less-specific classifications from replacing stronger results.

The browser displays the application version. See [Classification coverage](CLASSIFICATION-COVERAGE.md) for the included classification count, internal schema version, and technical implementation inventory.

Several clearly unreachable legacy predicates were implemented according to their apparent intent and covered by regression tests. This includes Exchange Online HTTP 401 Autodiscover host matching, attachment classification before generic OWA handling, specialized HTTP 0 handling, and accepting either known Outlook Microsoft 365 host for general classification.

HAR does not normally contain Fiddler process names, server/client IP addresses, TLS tunnel details, or detailed server timing. Rules that require those fields are not guessed. SAZ metadata coverage can be expanded as those values are added to the normalized trace model.

## Roadmap

See the [support-engineering roadmap](SUPPORT-ENGINEERING-ROADMAP.md) for the prioritized trace-analysis, reporting, metadata, performance, and investigation-workflow plan.
