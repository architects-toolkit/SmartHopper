<#
.SYNOPSIS
  Generates or decodes your strong-name key for local signing.
.PARAMETER Generate
  If set, creates a new SNK file locally.
.PARAMETER Base64
  If set, writes an SNK from Base64 text.
.PARAMETER Build
  If set, builds the solution with signing.
.PARAMETER Export
  If set, exports the SNK as Base64 text.
#>
param(
  [switch]$Generate,
  [string]$Base64,
  [switch]$Build,
  [switch]$Export
)

$snkPath = 'signing.snk'
if ($Generate) {
    Write-Host "Generating new SNK at $snkPath"
    # Locate sn.exe in PATH
    $snCmd = Get-Command sn.exe -ErrorAction SilentlyContinue
    if ($snCmd) {
        & $snCmd.Source -k $snkPath
    } else {
        # Fallback to known SDK locations
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
    Write-Host "Decoding SNK from Base64 into $snkPath"
    [IO.File]::WriteAllBytes($snkPath, [Convert]::FromBase64String($Base64))
} elseif ($Build) {
    Write-Host "Building solution with signing"
    dotnet build SmartHopper.sln -c Release `
      /p:SignAssembly=true `
    # Signing is applied automatically from Directory.Build.props
} elseif ($Export) {
    $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($snkPath))
    Write-Host "`nSNK Base64:`n$b64"
} else {
    Write-Host "No action specified. Use -Generate, -Base64 '<text>', -Build, or -Export."
    exit 1
}
