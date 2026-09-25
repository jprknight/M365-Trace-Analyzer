param(
    [Parameter(Mandatory)]
    [string] $PublishDirectory,

    [string] $ExpectedVersion,

    [string] $Url = "http://localhost:8182",

    [int] $StartupTimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "..\src\M365Trace.Web\M365Trace.Web.csproj"
if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    [xml] $project = Get-Content -LiteralPath $projectPath
    $ExpectedVersion = [string] $project.Project.PropertyGroup.Version
}

if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    throw "The expected application version could not be determined."
}

$publishPath = (Resolve-Path -LiteralPath $PublishDirectory).Path
$executablePath = Join-Path $publishPath "M365Trace.Web.exe"
if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "Published executable was not found at '$executablePath'."
}

$stdoutPath = Join-Path $env:TEMP "m365-trace-smoke-$([Guid]::NewGuid().ToString('N')).stdout.log"
$stderrPath = Join-Path $env:TEMP "m365-trace-smoke-$([Guid]::NewGuid().ToString('N')).stderr.log"
$process = $null
$previousBrowserLaunchSetting = $env:M365_TRACE_DISABLE_BROWSER_LAUNCH

try {
    $env:M365_TRACE_DISABLE_BROWSER_LAUNCH = "1"
    $process = Start-Process `
        -FilePath $executablePath `
        -ArgumentList "--urls", $Url `
        -WorkingDirectory $publishPath `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru
    $env:M365_TRACE_DISABLE_BROWSER_LAUNCH = $previousBrowserLaunchSetting

    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    $page = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) {
            $stdout = Get-Content -LiteralPath $stdoutPath -Raw -ErrorAction SilentlyContinue
            $stderr = Get-Content -LiteralPath $stderrPath -Raw -ErrorAction SilentlyContinue
            throw "Published application exited before startup.`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
        }

        try {
            $page = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2
            break
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }

    if ($null -eq $page) {
        throw "Published application did not respond at '$Url' within $StartupTimeoutSeconds seconds."
    }

    if ($page.StatusCode -ne 200) {
        throw "Application root returned HTTP $($page.StatusCode)."
    }

    if ($page.Content -notmatch [Regex]::Escape("Version $ExpectedVersion")) {
        throw "Application root does not display version $ExpectedVersion."
    }

    if ($page.Content -notmatch "Open Trace") {
        throw "Application root does not contain the Open Trace control."
    }

    foreach ($asset in @("/app.css", "/_framework/blazor.web.js")) {
        $assetResponse = Invoke-WebRequest `
            -Uri "$($Url.TrimEnd('/'))$asset" `
            -UseBasicParsing `
            -TimeoutSec 10
        if ($assetResponse.StatusCode -ne 200 -or $assetResponse.RawContentLength -le 0) {
            throw "Static asset '$asset' was not served correctly."
        }
    }

    Write-Host "Published application smoke test passed at $Url."
}
finally {
    $env:M365_TRACE_DISABLE_BROWSER_LAUNCH = $previousBrowserLaunchSetting

    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id
        $process.WaitForExit()
    }

    Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
}
