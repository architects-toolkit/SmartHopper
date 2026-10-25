<#
.SYNOPSIS
  Replaces SmartHopperPublicKey in Directory.Build.props with an anonymized placeholder.
.DESCRIPTION
  Use this script before sharing the repository to remove the real strong-name public key. It keeps
  the InternalsVisibleTo entries intact but replaces the SmartHopperPublicKey value with a harmless
  placeholder so internal APIs remain hidden from unsigned builds.
.PARAMETER CsprojPath
  Path to the project/props file to update. Defaults to Directory.Build.props.
.PARAMETER PlaceholderKey
  Optional placeholder key to use. If omitted, a default placeholder string is written.
.PARAMETER Help
  Displays this help message.
.EXAMPLE
  .\Anonymize-SmartHopperPublicKey.ps1
  Replaces the SmartHopperPublicKey with a default placeholder in Directory.Build.props.
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
    Write-Host "  -CsprojPath <path>    Path to project/props file (default: Directory.Build.props)"
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
    $CsprojPath = Join-Path $scriptDir "Directory.Build.props"
}

# Purpose: Ensure the target file exists before attempting modifications.
if (-not (Test-Path $CsprojPath)) {
    Write-Error "Target file not found at: $CsprojPath"
    exit 1
}

Write-Host "Anonymizing SmartHopperPublicKey in: $CsprojPath"

# Purpose: Load the file as XML to safely update the property, preserving its formatting.
try {
    $content = Get-Content $CsprojPath -Raw
    $rawBytes = [System.IO.File]::ReadAllBytes($CsprojPath)
    $hasUtf8Bom = $rawBytes.Length -ge 3 -and $rawBytes[0] -eq 0xEF -and $rawBytes[1] -eq 0xBB -and $rawBytes[2] -eq 0xBF
    $hasXmlDeclaration = $content.TrimStart([char]0xFEFF).TrimStart().StartsWith("<?xml")
    $newLine = if ($content.Contains("`r`n")) { "`r`n" } else { "`n" }
    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $xml.LoadXml($content.TrimStart([char]0xFEFF))
    $keyElement = $xml.SelectSingleNode("//SmartHopperPublicKey")
    if (-not $keyElement) {
        Write-Error "Could not find SmartHopperPublicKey element in $CsprojPath"
        exit 1
    }

    if ([string]::IsNullOrWhiteSpace($PlaceholderKey)) {
        $PlaceholderKey = "This value is automatically replaced by the build tooling before official builds."
    }

    $existingKey = $keyElement.InnerText
    if ($existingKey -eq $PlaceholderKey) {
        Write-Host "SmartHopperPublicKey is already the placeholder. No anonymization needed."
        exit 0
    }

    Write-Host "Replacing key (length $($existingKey.Length)) with placeholder (length $($PlaceholderKey.Length))."
    $keyElement.InnerText = $PlaceholderKey
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = [System.Text.UTF8Encoding]::new($hasUtf8Bom)
    $settings.Indent = $false
    $settings.OmitXmlDeclaration = -not $hasXmlDeclaration
    $settings.NewLineChars = $newLine
    $writer = [System.Xml.XmlWriter]::Create($CsprojPath, $settings)
    $xml.Save($writer)
    $writer.Close()
    Write-Host "Successfully anonymized SmartHopperPublicKey."
} catch {
    Write-Error "Failed to update .csproj: $_"
    exit 1
}

Write-Host "Done."
