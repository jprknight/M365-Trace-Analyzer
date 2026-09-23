param(
    [Parameter(Mandatory)]
    [string] $ResultsDirectory,

    [double] $MinimumLineCoveragePercent = 40
)

$ErrorActionPreference = "Stop"

$coverageFiles = Get-ChildItem `
    -LiteralPath $ResultsDirectory `
    -Filter "coverage.cobertura.xml" `
    -Recurse `
    -File |
    Where-Object {
        $_.FullName -notmatch "[\\/](In|Out)[\\/]"
    }

if ($coverageFiles.Count -eq 0) {
    throw "No Cobertura coverage reports were found in '$ResultsDirectory'."
}

[long] $coveredLines = 0
[long] $validLines = 0

foreach ($coverageFile in $coverageFiles) {
    [xml] $coverage = Get-Content -LiteralPath $coverageFile.FullName
    $coveredLines += [long] $coverage.coverage.GetAttribute("lines-covered")
    $validLines += [long] $coverage.coverage.GetAttribute("lines-valid")
}

if ($validLines -le 0) {
    throw "Coverage reports did not contain any valid source lines."
}

$coveragePercent = 100 * $coveredLines / $validLines
Write-Host (
    "Aggregate line coverage: {0:N2}% ({1:N0}/{2:N0} lines). Required: {3:N2}%." -f
    $coveragePercent,
    $coveredLines,
    $validLines,
    $MinimumLineCoveragePercent)

if ($coveragePercent -lt $MinimumLineCoveragePercent) {
    throw (
        "Aggregate line coverage {0:N2}% is below the required {1:N2}%." -f
        $coveragePercent,
        $MinimumLineCoveragePercent)
}
