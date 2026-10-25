<#
.SYNOPSIS
  Replaces SmartHopperPublicKey in SmartHopper.Infrastructure.csproj with an anonymized placeholder.
.DESCRIPTION
  Use this script before sharing the repository to remove the real strong-name public key. It keeps
  the InternalsVisibleTo entries intact but replaces the SmartHopperPublicKey value with a harmless
  placeholder so internal APIs remain hidden from unsigned builds.
.PARAMETER CsprojPath
  Path to the .csproj file to update. Defaults to SmartHopper.Infrastructure.csproj under src.
.PARAMETER PlaceholderKey
  Optional placeholder key to use. If omitted, a default placeholder string is written.
.PARAMETER Help
  Displays this help message.
.EXAMPLE
  .\Anonymize-SmartHopperPublicKey.ps1
  Replaces the SmartHopperPublicKey with a default placeholder in the default csproj.
.EXAMPLE
  .\Anonymize-SmartHopperPublicKey.ps1 -PlaceholderKey "DEADBEEF"
  Uses the provided placeholder string instead of the default placeholder.
#>
param(
    [string]$CsprojPath,
    [string]$PlaceholderKey,
    [switch]$Help
)

# Purpose: Shows usage information for this script.
function Show-Help {
    Write-Host "Usage: .\Anonymize-SmartHopperPublicKey.ps1 [options]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -CsprojPath <path>    Path to .csproj file (default: SmartHopper.Infrastructure.csproj)"
    Write-Host "  -PlaceholderKey <hex> Placeholder key to write (default: 'This value is automatically replaced by the build tooling before official builds.')"
    Write-Host "  -Help                 Displays this help message"
}

if ($Help) {
    Show-Help
    exit 0
}

# Purpose: Resolve default paths relative to the repository root. An explicit -CsprojPath
# updates only that file; otherwise every project under src declaring SmartHopperPublicKey
# is anonymized.
$scriptDir = Split-Path -Parent $PSScriptRoot
if ($CsprojPath) {
    if (-not (Test-Path $CsprojPath)) {
        Write-Error ".csproj not found at: $CsprojPath"
        exit 1
    }
    $CsprojPaths = @($CsprojPath)
} else {
    $srcDir = Join-Path $scriptDir "src"
    $CsprojPaths = @(Get-ChildItem -Path $srcDir -Filter "*.csproj" -Recurse -File |
        Where-Object { Select-String -Path $_.FullName -Pattern '<SmartHopperPublicKey>' -Quiet } |
        ForEach-Object { $_.FullName })
    if ($CsprojPaths.Count -eq 0) {
        Write-Error "No .csproj containing a SmartHopperPublicKey element found under: $srcDir"
        exit 1
    }
}

if ([string]::IsNullOrWhiteSpace($PlaceholderKey)) {
    $PlaceholderKey = "This value is automatically replaced by the build tooling before official builds."
}

$pattern = '(<SmartHopperPublicKey>)[^<]*(</SmartHopperPublicKey>)'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

foreach ($path in $CsprojPaths) {
    Write-Host "Anonymizing SmartHopperPublicKey in: $path"

    try {
        $content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
        $match = [System.Text.RegularExpressions.Regex]::Match($content, $pattern)
        if (-not $match.Success) {
            Write-Error "Could not find SmartHopperPublicKey element in $path"
            exit 1
        }

        $existingKey = $match.Groups[0].Value -replace '</?SmartHopperPublicKey>', ''
        if ($existingKey -eq $PlaceholderKey) {
            Write-Host "SmartHopperPublicKey is already the placeholder in $path. Skipping."
            continue
        }

        Write-Host "Replacing key (length $($existingKey.Length)) with placeholder (length $($PlaceholderKey.Length))."
        $content = $content -replace $pattern, "`${1}$PlaceholderKey`${2}"
        [System.IO.File]::WriteAllText($path, $content, $utf8NoBom)
        Write-Host "Successfully anonymized SmartHopperPublicKey in $path."
    } catch {
        Write-Error "Failed to update .csproj: $_"
        exit 1
    }
}

Write-Host "Done."
