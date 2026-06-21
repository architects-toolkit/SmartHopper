<#
.SYNOPSIS
    Updates the model-verification issue template dropdown with all released versions
    of SmartHopper.

.DESCRIPTION
    Gets all git tags, filters out the old dev tags, strips prerelease suffixes,
    filters by minimum major version, sorts them, and rewrites the options list
    in .github/ISSUE_TEMPLATE/model-verification.yml between the
    AUTO-GENERATED-VERSION-OPTIONS markers.

.PARAMETER TemplateFile
    Optional. Path to the issue template YAML file.
    Defaults to .github/ISSUE_TEMPLATE/model-verification.yml relative to the repo root.

.PARAMETER MinMajor
    Optional. The minimum major version number to include (e.g., 1).
    If specified, tags with a major version lower than this will be excluded.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)][string] $TemplateFile = "",
    [Parameter(Mandatory = $false)][int] $MinMajor = -1
)

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

if ([string]::IsNullOrWhiteSpace($TemplateFile)) {
    $TemplateFile = Join-Path $repoRoot '.github/ISSUE_TEMPLATE/model-verification.yml'
}

$startMarker = '# AUTO-GENERATED-VERSION-OPTIONS-START'
$endMarker   = '# AUTO-GENERATED-VERSION-OPTIONS-END'

# Get tags
$tags = git tag --list --sort=-v:refname
$filteredTags = $tags | Where-Object { $_ -notmatch '^0\.0\.0-dev' }

# Strip prerelease suffixes and get unique versions
$uniqueVersions = [System.Collections.Generic.HashSet[string]]::new()
foreach ($tag in $filteredTags) {
    # Remove any -alpha, -beta, -rc, etc.
    $version = $tag -replace '-.*$', ''
    
    if ([string]::IsNullOrWhiteSpace($version)) { continue }

    # Check minimum major version if specified
    if ($MinMajor -ge 0) {
        $majorStr = ($version -split '\.')[0]
        if ([int]$majorStr -lt $MinMajor) { continue }
    }

    [void]$uniqueVersions.Add($version)
}

$optionLines = [System.Collections.Generic.List[string]]::new()
foreach ($version in $uniqueVersions) {
    $optionLines.Add("        - $version")
}

if (-not (Test-Path $TemplateFile)) {
    Write-Error "Template file not found: $TemplateFile"
    exit 2
}

$templateLines = [System.IO.File]::ReadAllLines($TemplateFile, [System.Text.Encoding]::UTF8)

$startIdx = -1
$endIdx   = -1

for ($i = 0; $i -lt $templateLines.Count; $i++) {
    if ($templateLines[$i].TrimEnd() -match [regex]::Escape($startMarker)) {
        $startIdx = $i
    }
    if ($templateLines[$i].TrimEnd() -match [regex]::Escape($endMarker)) {
        $endIdx = $i
    }
}

if ($startIdx -lt 0 -or $endIdx -lt 0 -or $endIdx -le $startIdx) {
    Write-Error "Could not find AUTO-GENERATED-VERSION-OPTIONS markers in $TemplateFile"
    exit 2
}

$before  = $templateLines[0..$startIdx]
$after   = $templateLines[$endIdx..($templateLines.Count - 1)]
$newLines = @($before) + @($optionLines) + @($after)

$newContent = ($newLines -join "`n") + "`n"
$oldContent = [System.IO.File]::ReadAllText($TemplateFile, [System.Text.Encoding]::UTF8)

if ($newContent -eq $oldContent) {
    Write-Host "No changes needed in $TemplateFile"
    exit 0
}

[System.IO.File]::WriteAllText($TemplateFile, $newContent, $utf8NoBom)
Write-Host "Updated $TemplateFile with $($optionLines.Count) version option(s)."
exit 0
