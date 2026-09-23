param(
    [Parameter(Mandatory)]
    [string] $Tag,

    [string] $ProjectPath = "src\M365Trace.Web\M365Trace.Web.csproj"
)

$ErrorActionPreference = "Stop"

$normalizedTag = $Tag.Trim()
if ($normalizedTag.StartsWith("refs/tags/", [StringComparison]::OrdinalIgnoreCase)) {
    $normalizedTag = $normalizedTag["refs/tags/".Length..($normalizedTag.Length - 1)] -join ""
}

$normalizedTag = $normalizedTag.TrimStart("v", "V")
[xml] $project = Get-Content -LiteralPath $ProjectPath
$projectVersion = [string] $project.Project.PropertyGroup.Version

if ([string]::IsNullOrWhiteSpace($projectVersion)) {
    throw "The application project does not declare a Version value."
}

if ($normalizedTag -ne $projectVersion) {
    throw "Release tag version '$normalizedTag' does not match project version '$projectVersion'."
}

Write-Host "Release tag v$normalizedTag matches project version $projectVersion."
