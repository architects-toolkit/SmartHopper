<#
.SYNOPSIS
  Updates InternalsVisibleTo entries in SmartHopper.Infrastructure.csproj with the public key from signing.snk.
.DESCRIPTION
  Extracts the public key from the strong-name key file (signing.snk) and updates the
  SmartHopperPublicKey property in SmartHopper.Infrastructure.csproj.
  This ensures InternalsVisibleTo attributes are generated at build time with the correct key.
.PARAMETER SnkPath
  Path to the signing.snk file. Defaults to signing.snk in the solution root.
.PARAMETER CsprojPath
  Path to the .csproj file to update. Defaults to SmartHopper.Infrastructure.csproj.
.PARAMETER Help
  Displays this help message.
.EXAMPLE
  .\Update-InternalsVisibleTo.ps1
  Updates the .csproj using signing.snk from the solution root.
.EXAMPLE
  .\Update-InternalsVisibleTo.ps1 -SnkPath "C:\path\to\signing.snk"
  Updates the .csproj using a specific .snk file.
#>
param(
    [string]$SnkPath,
    [string]$CsprojPath,
    [switch]$Help
)

function Show-Help {
    Write-Host "Usage: .\Update-InternalsVisibleTo.ps1 [options]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -SnkPath <path>     Path to signing.snk file (default: solution root)"
    Write-Host "  -CsprojPath <path>  Path to .csproj file to update"
    Write-Host "  -Help               Displays this help message"
}

if ($Help) {
    Show-Help
    exit 0
}

# Determine paths
$scriptDir = Split-Path -Parent $PSScriptRoot
if (-not $SnkPath) {
    $SnkPath = Join-Path $scriptDir "signing.snk"
}
if (-not $CsprojPath) {
    $CsprojPath = Join-Path $scriptDir "src\SmartHopper.Infrastructure\SmartHopper.Infrastructure.csproj"
}

# Validate SNK exists
if (-not (Test-Path $SnkPath)) {
    Write-Error "signing.snk not found at: $SnkPath"
    exit 1
}

# Validate csproj exists
if (-not (Test-Path $CsprojPath)) {
    Write-Error ".csproj not found at: $CsprojPath"
    exit 1
}

Write-Host "Extracting public key from: $SnkPath"

# Find sn.exe to extract public key
function Find-SnExe {
    $snCmd = Get-Command sn.exe -ErrorAction SilentlyContinue
    if ($snCmd) {
        return $snCmd.Source
    }
    
    # Search in common Windows SDK and .NET Framework locations
    $sdkPaths = @(
        "${Env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\x64\sn.exe",
        "${Env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\sn.exe",
        "${Env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.2 Tools\x64\sn.exe",
        "${Env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.2 Tools\sn.exe",
        "${Env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\x64\sn.exe",
        "${Env:ProgramFiles(x86)}\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe",
        "${Env:ProgramFiles(x86)}\Windows Kits\10\bin\x64\sn.exe",
        "${Env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.22621.0\x64\sn.exe",
        "${Env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.19041.0\x64\sn.exe",
        "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\x64\sn.exe",
        "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\sn.exe"
    )
    
    foreach ($path in $sdkPaths) {
        if (Test-Path $path) {
            return $path
        }
    }
    
    # Try searching in Program Files
    $searchPaths = @(
        "C:\Program Files (x86)\Microsoft SDKs",
        "C:\Program Files (x86)\Windows Kits"
    )
    
    foreach ($searchPath in $searchPaths) {
        if (Test-Path $searchPath) {
            $found = Get-ChildItem -Path $searchPath -Filter "sn.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($found) {
                return $found.FullName
            }
        }
    }
    
    return $null
}

$snExe = Find-SnExe
if (-not $snExe) {
    Write-Error "sn.exe not found. Please install the Windows SDK or run in a Developer PowerShell."
    exit 1
}

Write-Host "Using sn.exe at: $snExe"

# Extract public key to temp file, then get the hex string
$tempPubKey = [System.IO.Path]::GetTempFileName()
try {
    # Extract public key from SNK
    & $snExe -p $SnkPath $tempPubKey 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to extract public key from SNK"
        exit 1
    }
    
    # Get the public key as hex string using sn -tp
    $output = & $snExe -tp $tempPubKey 2>&1
    
    # Parse the public key hex from the output
    # The output contains lines like "Public key (hash algorithm: sha1):" followed by hex
    $publicKeyHex = ""
    $capture = $false
    foreach ($line in $output) {
        if ($line -match "^Public key") {
            $capture = $true
            continue
        }
        if ($capture) {
            # Stop at empty line or "Public key token" line
            if ($line -match "^Public key token" -or [string]::IsNullOrWhiteSpace($line)) {
                break
            }
            # Accumulate hex digits (remove spaces)
            $publicKeyHex += ($line -replace '\s', '')
        }
    }
    
    if ([string]::IsNullOrEmpty($publicKeyHex)) {
        Write-Error "Failed to parse public key from sn.exe output"
        Write-Host "sn.exe output was:"
        $output | ForEach-Object { Write-Host $_ }
        exit 1
    }
    
    Write-Host "Extracted public key: $($publicKeyHex.Substring(0, 40))..."
    
} finally {
    if (Test-Path $tempPubKey) {
        Remove-Item $tempPubKey -Force
    }
}

# Update the .csproj file
Write-Host "Updating: $CsprojPath"

$content = Get-Content $CsprojPath -Raw

# Replace the SmartHopperPublicKey value using XML parsing to avoid regex issues
try {
    $xml = [xml]$content
    $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    
    # Find SmartHopperPublicKey element
    $keyElement = $xml.SelectSingleNode("//SmartHopperPublicKey")
    if ($keyElement) {
        $keyElement.InnerText = $publicKeyHex
        $xml.Save($CsprojPath)
        Write-Host "Successfully updated SmartHopperPublicKey in $CsprojPath"
    } else {
        Write-Error "Could not find SmartHopperPublicKey element in $CsprojPath"
        exit 1
    }
} catch {
    Write-Error "Failed to update .csproj: $_"
    exit 1
}

Write-Host "Done."
