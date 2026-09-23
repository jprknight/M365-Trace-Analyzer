param(
    [string] $SolutionPath = "M365-Trace-Analyzer.sln"
)

$ErrorActionPreference = "Stop"

$reportLines = & dotnet list $SolutionPath package `
    --vulnerable `
    --include-transitive `
    --format json

if ($LASTEXITCODE -ne 0) {
    throw "NuGet vulnerability analysis failed."
}

$report = $reportLines -join [Environment]::NewLine
$null = $report | ConvertFrom-Json

if ($report -match '"vulnerabilities"\s*:') {
    Write-Host $report
    throw "One or more vulnerable NuGet packages were detected."
}

Write-Host "No known vulnerable NuGet packages were detected."
