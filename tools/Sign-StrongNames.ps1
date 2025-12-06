<#
.SYNOPSIS
  Manage strong-name key (.snk) for SmartHopper.
.DESCRIPTION
  Generates, decodes, or exports a .snk strong-name key.
.PARAMETER SnkPath
  Optional path to the signing.snk file; defaults to signing.snk next to this script.
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

if ([string]::IsNullOrWhiteSpace($SnkPath)) {
    $snkPath = Join-Path -Path $PSScriptRoot -ChildPath 'signing.snk'
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

if ($Generate) {
    Write-Host "Generating new strong-name key at $snkPath"
    $snCmd = Get-Command sn.exe -ErrorAction SilentlyContinue
    if ($snCmd) {
        & $snCmd.Source -k $snkPath
    } else {
        $sdkPaths = @(
            "$Env:ProgramFiles(x86)\Windows Kits\10\bin\x64\sn.exe",
            "$Env:ProgramFiles(x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\sn.exe"
        )
        $exe = $sdkPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
        if ($exe) {
            & $exe -k $snkPath
        } else {
            Write-Error "sn.exe not found. Please install the Windows SDK or run in a Developer PowerShell."
            exit 1
        }
    }
} elseif ($Base64) {
    Write-Host "Decoding Base64 SNK into $snkPath"
    [IO.File]::WriteAllBytes($snkPath, [Convert]::FromBase64String($Base64))
} elseif ($Export) {
    if (-not (Test-Path $snkPath)) {
        Write-Error "$snkPath not found."
        exit 1
    }
    $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($snkPath))
    Write-Host $b64
}
