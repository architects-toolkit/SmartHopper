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

# Purpose: Resolve default paths relative to the repository root.
$scriptDir = Split-Path -Parent $PSScriptRoot
if (-not $CsprojPath) {
    $CsprojPath = Join-Path $scriptDir "src\SmartHopper.Infrastructure\SmartHopper.Infrastructure.csproj"
}

# Purpose: Ensure the target .csproj exists before attempting modifications.
if (-not (Test-Path $CsprojPath)) {
    Write-Error ".csproj not found at: $CsprojPath"
    exit 1
}

Write-Host "Anonymizing SmartHopperPublicKey in: $CsprojPath"

# Purpose: Load the csproj as XML to safely update the property.
try {
    $content = Get-Content $CsprojPath -Raw
    $xml = [xml]$content
    $keyElement = $xml.SelectSingleNode("//SmartHopperPublicKey")
    if (-not $keyElement) {
        Write-Error "Could not find SmartHopperPublicKey element in $CsprojPath"
        exit 1
    }

    $existingKey = $keyElement.InnerText
    if ([string]::IsNullOrWhiteSpace($PlaceholderKey)) {
        $PlaceholderKey = "This value is automatically replaced by the build tooling before official builds."
    }

    Write-Host "Replacing key (length $($existingKey.Length)) with placeholder (length $($PlaceholderKey.Length))."
    $keyElement.InnerText = $PlaceholderKey
    $xml.Save($CsprojPath)
    Write-Host "Successfully anonymized SmartHopperPublicKey."
} catch {
    Write-Error "Failed to update .csproj: $_"
    exit 1
}

Write-Host "Done."
