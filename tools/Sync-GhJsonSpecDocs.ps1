# Sync GhJSON specification markdown documents from the published ghjson-spec repository
# into the local embedded snapshot used by the smarthopper_ghjson_reference tool.
#
# Usage (from repo root):
#   pwsh -ExecutionPolicy Bypass -File .\tools\Sync-GhJsonSpecDocs.ps1
#   pwsh -ExecutionPolicy Bypass -File .\tools\Sync-GhJsonSpecDocs.ps1 -Check        # exit code 1 if drift
#   pwsh -ExecutionPolicy Bypass -File .\tools\Sync-GhJsonSpecDocs.ps1 -BaseUrl ...    # override source URL
#
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$BaseUrl = 'https://raw.githubusercontent.com/architects-toolkit/ghjson-spec/main/docs/',
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$DocsPath = 'src/SmartHopper.Core.Grasshopper/Resources/GhJsonSpec/v1.0',
    [string[]]$File = @('specification.md', 'ghpatch.md'),
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $BaseUrl.EndsWith('/')) { $BaseUrl += '/' }

$destRoot = Join-Path $Root $DocsPath
if (-not (Test-Path $destRoot)) {
    New-Item -ItemType Directory -Force -Path $destRoot | Out-Null
}

$changed = @()
$unchanged = @()
$drift = $false

foreach ($fileName in $File) {
    $url = $BaseUrl + $fileName
    $dest = Join-Path $destRoot $fileName

    Write-Host "Fetching $url"
    try {
        $remote = Invoke-WebRequest -Uri $url -UseBasicParsing -Headers @{ 'User-Agent' = 'smarthopper-sync' } -ErrorAction Stop
    }
    catch {
        throw "Failed to download $url : $($_.Exception.Message)"
    }

    # Normalize line endings so comparisons are stable regardless of source host.
    $remoteText = ($remote.Content -replace "`r`n", "`n").TrimEnd() + "`n"

    $localText = $null
    if (Test-Path $dest) {
        $localText = ([System.IO.File]::ReadAllText($dest)) -replace "`r`n", "`n"
        $localText = $localText.TrimEnd() + "`n"
    }

    if ($localText -eq $remoteText) {
        $unchanged += $fileName
        continue
    }

    $changed += $fileName
    $drift = $true

    if ($Check) {
        Write-Host "  DRIFT: $fileName" -ForegroundColor Yellow
    }
    else {
        # Preserve remote byte content (no BOM), but use LF to match spec repo convention.
        [System.IO.File]::WriteAllText($dest, $remoteText, (New-Object System.Text.UTF8Encoding($false)))
        Write-Host "  Updated: $dest" -ForegroundColor Green
    }
}

Write-Host ''
Write-Host ("GhJSON spec docs: {0} changed, {1} unchanged, {2} total." -f $changed.Count, $unchanged.Count, $File.Count)

if ($Check -and $drift) {
    Write-Host ''
    Write-Host 'Embedded GhJSON spec docs snapshot is OUT OF DATE.' -ForegroundColor Red
    exit 1
}

exit 0
