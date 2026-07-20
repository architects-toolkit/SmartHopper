<#
.SYNOPSIS
  Manage strong-name key (.snk) for SmartHopper.
.DESCRIPTION
  Generates, decodes, or exports a .snk strong-name key.
.PARAMETER SnkPath
  Optional path to the signing.snk file; defaults to ..\signing.snk (solution root), falling back to signing.snk next to this script.
.PARAMETER Generate
  Generates a new strong-name key file (signing.snk) using sn.exe.
.PARAMETER Base64
  Base64-encoded SNK data; decodes into signing.snk.
.PARAMETER Export
  Exports the signing.snk file as Base64 text.
.PARAMETER Help
  Displays this help message.
#>
param(
    [string]$SnkPath,
    [switch]$Generate,
    [string]$Base64,
    [switch]$Export,
    [switch]$Help
)

$solutionRoot = Split-Path -Parent $PSScriptRoot
$defaultRootSnk = Join-Path -Path $solutionRoot -ChildPath 'signing.snk'
$defaultLocalSnk = Join-Path -Path $PSScriptRoot -ChildPath 'signing.snk'

if ([string]::IsNullOrWhiteSpace($SnkPath)) {
    $snkPath = $defaultRootSnk
} else {
    $snkPath = $SnkPath
}

function Show-Help {
    Write-Host "Usage: .\Sign-StrongNames.ps1 [options]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Generate         Generates a new strong-name key (signing.snk) via sn.exe."
    Write-Host "  -Base64 <text>    Decodes Base64 text into signing.snk."
    Write-Host "  -Export           Exports signing.snk as Base64 text."
    Write-Host "  -Help             Displays this help message."
}

if ($Help -or (-not $Generate -and -not $Base64 -and -not $Export)) {
    Show-Help
    exit 0
}

function Find-SnExe {
    $snCmd = Get-Command sn.exe -ErrorAction SilentlyContinue
    if ($snCmd) { return $snCmd.Source }

    $sdkPaths = @(
        "$Env:ProgramFiles(x86)\Windows Kits\10\bin\x64\sn.exe",
        "$Env:ProgramFiles(x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\x64\sn.exe",
        "$Env:ProgramFiles(x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\sn.exe"
    )
    $sdkPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
}

function Test-SnkPublicKey {
    param([string]$Path)
    $snExe = Find-SnExe
    if (-not $snExe) { return $false }
    $tempPub = [System.IO.Path]::GetTempFileName()
    try {
        & $snExe -p $Path $tempPub 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { return $false }
        & $snExe -tp $tempPub 2>&1 | Out-Null
        return ($LASTEXITCODE -eq 0)
    } finally {
        if (Test-Path $tempPub) { Remove-Item $tempPub -Force -ErrorAction SilentlyContinue }
    }
}

function New-SnkWithDotNet {
    param([string]$Path)
    # Use legacy CAPI RSA provider (PROV_RSA_FULL, type 1) with a signature key.
    # This produces a key pair in the classic format that sn.exe -tp can parse,
    # avoiding CNG/RSA2 1024-bit incompatibilities reported after VS updates.
    $cspParams = [System.Security.Cryptography.CspParameters]::new()
    $cspParams.ProviderType = 1
    $cspParams.KeyNumber = [System.Security.Cryptography.KeyNumber]::Signature
    $rsa = [System.Security.Cryptography.RSACryptoServiceProvider]::new(1024, $cspParams)
    try {
        [System.IO.File]::WriteAllBytes($Path, $rsa.ExportCspBlob($true))
    } finally {
        $rsa.Dispose()
    }
}

if ($Generate) {
    Write-Host "Generating new strong-name key at $snkPath" -ForegroundColor Cyan

    $snExe = Find-SnExe
    if ($snExe) {
        & $snExe -k $snkPath 2>&1 | Out-Null
    }

    if (-not (Test-Path $snkPath) -or -not (Test-SnkPublicKey $snkPath)) {
        if ($snExe -and $LASTEXITCODE -ne 0) {
            Write-Warning "sn.exe failed or produced an invalid key; falling back to .NET CAPI key generation."
        } elseif (-not $snExe) {
            Write-Warning "sn.exe not found; using .NET CAPI key generation."
        } else {
            Write-Warning "Generated key is not valid for sn.exe -tp; regenerating with .NET CAPI."
        }
        New-SnkWithDotNet $snkPath
    }

    if (-not (Test-SnkPublicKey $snkPath)) {
        Write-Error "Failed to generate a valid strong-name key at $snkPath."
        exit 1
    }

    Write-Host "Successfully generated valid strong-name key at $snkPath" -ForegroundColor Green
} elseif ($Base64) {
    Write-Host "Decoding Base64 SNK into $snkPath" -ForegroundColor Cyan
    [IO.File]::WriteAllBytes($snkPath, [Convert]::FromBase64String($Base64))
} elseif ($Export) {
    if (-not (Test-Path $snkPath)) {
        if ([string]::IsNullOrWhiteSpace($SnkPath) -and (Test-Path $defaultLocalSnk)) {
            $snkPath = $defaultLocalSnk
        } else {
            Write-Error "$snkPath not found."
            exit 1
        }
    }
    $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($snkPath))
    Write-Host $b64
}
