<#
.SYNOPSIS
  Manage PFX certificate and Authenticode-sign SmartHopper provider assemblies.
.DESCRIPTION
  Generates a self-signed PFX, decodes/imports Base64 PFX, exports PFX as Base64, or
  recursively Authenticode-signs all SmartHopper.Providers.*.dll under a given path.
.PARAMETER Generate
  Creates a self-signed PFX certificate (signing.pfx) for Authenticode signing. Requires -Password.
.PARAMETER Base64
  Base64-encoded PFX data; decodes into signing.pfx. Requires -Password for export and signing.
.PARAMETER File
  Path to a text file containing Base64-encoded PFX data; supersedes -Base64 for signing.
.PARAMETER Password
  Password for PFX certificate import/export and signing operations.
.PARAMETER Export
  Exports the signing.pfx file as Base64 text to stdout. Requires -Password.
.PARAMETER Sign
  Authenticode-signs all SmartHopper.Providers.*.dll under the given path, using the decoded PFX.
.PARAMETER Help
  Displays this help message.
.PARAMETER PfxPath
  Override default signing PFX path (default 'signing.pfx').
#>
param(
    [switch]$Generate,
    [string]$Base64,
    [string]$File,
    [string]$Password,
    [switch]$Export,
    [switch]$Help,
    [string]$Sign,
    [string]$PfxPath
)

# default PFX path; override via -PfxPath
$pfxPath = if ($PfxPath) { $PfxPath } else { Join-Path -Path $PSScriptRoot -ChildPath 'signing.pfx' }

function Show-Help {
    Write-Host "Usage: .\Sign-Authenticode.ps1 [options]"
    Write-Host ""
    Write-Host "Options:"  
    Write-Host "  -Generate               Creates a self-signed PFX certificate (signing.pfx). Requires -Password."  
    Write-Host "  -Base64 <data>          Decodes Base64-encoded PFX into signing.pfx. Requires -Password."  
    Write-Host "  -File <path>            Path to a file containing Base64 PFX data; implies -Base64."  
    Write-Host "  -Password <pwd>         Password for PFX certificate import/export and signing."  
    Write-Host "  -Export                 Exports signing.pfx as Base64 text to stdout. Requires -Password."  
    Write-Host "  -Sign <path>            Authenticode-signs all SmartHopper.Providers.*.dll under <path>. Requires Base64 or Generate."  
    Write-Host "  -Help                   Displays this help message."  
    Write-Host "  -PfxPath <path>         Override default signing PFX path (default 'signing.pfx')."  
}

if ($Help -or (-not $Generate -and -not $Base64 -and -not $File -and -not $Export -and -not $Sign)) {
    Show-Help
    exit 0
}

if ($Generate) {
    if (-not $Password) {
        Write-Error "-Password is required when generating a PFX certificate."
        exit 1
    }
    Write-Host "Generating self-signed PFX certificate at $pfxPath"
    $securePwd = ConvertTo-SecureString -String $Password -AsPlainText -Force
    $cert = New-SelfSignedCertificate -Subject "CN=SmartHopperDev" -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy Exportable -KeySpec Signature -Type CodeSigningCert
    Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $pfxPath -Password $securePwd
    Write-Host "PFX certificate created at $pfxPath"
} elseif ($Base64) {
    if (-not $Password) {
        Write-Error "-Password is required when importing a Base64 PFX."
        exit 1
    }
    Write-Host "Decoding Base64 PFX into $pfxPath"
    [IO.File]::WriteAllBytes($pfxPath, [Convert]::FromBase64String($Base64))
} elseif ($Export) {
    if (-not (Test-Path $pfxPath)) {
        Write-Error "$pfxPath not found."
        exit 1
    }
    if (-not $Password) {
        Write-Error "-Password is required when exporting PFX."
        exit 1
    }
    Write-Host "Exporting PFX as Base64:"
    $bytes = [IO.File]::ReadAllBytes($pfxPath)
    $b64 = [Convert]::ToBase64String($bytes)
    Write-Host $b64
} elseif ($Sign) {
    # Read Base64 from file if requested
    if ($File) {
        if (-not (Test-Path $File)) {
            Write-Error "Base64 file '$File' not found."
            exit 1
        }
        Write-Host "Reading Base64 PFX data from '$File'"
        $Base64 = Get-Content $File -Raw
    }
    # If no Base64 or File and no explicit -PfxPath provided, locate existing PFX in script directory
    if (-not $Base64 -and -not $File -and -not $PfxPath) {
        $scriptDir = Split-Path $MyInvocation.MyCommand.Path
        $found = Get-ChildItem -Path $scriptDir -Filter '*.pfx' | Select-Object -First 1
        if ($found) {
            $pfxPath = $found.FullName
            Write-Host "Using existing PFX certificate file: $pfxPath"
        }
    }
    # Decode Base64 into PFX if provided
    if ($Base64) {
        Write-Host "Decoding Base64 PFX into $pfxPath"
        [IO.File]::WriteAllBytes($pfxPath, [Convert]::FromBase64String($Base64))
    }
    # Ensure PFX file exists
    if (-not (Test-Path $pfxPath)) {
        Write-Error "PFX file '$pfxPath' not found. Please use -Base64, -File, or place a .pfx in the script directory."
        exit 1
    }
    if (-not $Password) {
        Write-Error "-Password is required for signing operations."
        exit 1
    }
    Write-Host "Signing provider assemblies under path '$Sign' with Authenticode certificate"
    $signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue

    # Determine items to sign: explicit DLL or provider assemblies directory
    if ((Test-Path $Sign -PathType Leaf) -and ([IO.Path]::GetExtension($Sign) -ieq ".dll")) {
        Write-Host "Signing explicit DLL: $Sign"
        $items = @(Get-Item $Sign)
    } elseif (Test-Path $Sign -PathType Container) {
        Write-Host "Signing assemblies under directory: $Sign"
        $items = Get-ChildItem -Path $Sign -Recurse -Filter "*.dll" |
            Where-Object { $_.Name -like "SmartHopper.Providers.*.dll" -or $_.Name -eq "SmartHopper.Config.dll" }
    } else {
        Write-Error "Path '$Sign' is not a .dll file or directory"
        exit 1
    }

    foreach ($dll in $items) {
        Write-Host "Signing $($dll.FullName) with PFX file..."
        & $signtool.Source sign /fd SHA256 /a /f "$pfxPath" /p "$Password" $dll.FullName
        if ($LASTEXITCODE -ne 0) {
            Write-Host "File-based signing failed (exit code $LASTEXITCODE), falling back to store-based signing..."
            $imported = Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation Cert:\CurrentUser\My -Password (ConvertTo-SecureString -String $Password -AsPlainText -Force)
            if (-not $imported) {
                Write-Error "Failed to import PFX certificate. Please verify the PFX password and that the runner has permission to import certificates."
                exit 1
            }
            $thumb = $imported.Thumbprint
            Write-Host "Signing $($dll.FullName) with certificate thumbprint $thumb..."
            & $signtool.Source sign /fd SHA256 /sha1 $thumb $dll.FullName
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Fallback signing failed for $($dll.FullName)."
                exit 1
            }
        }
    }
}
