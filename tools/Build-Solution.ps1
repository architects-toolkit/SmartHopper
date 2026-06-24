<#
.SYNOPSIS
    Build the SmartHopper solution, update InternalsVisibleTo, and Authenticode-sign the output assemblies.
.DESCRIPTION
    This script orchestrates the full SmartHopper build pipeline:

    - Generates or reuses a strong-name key (signing.snk).
    - Generates or reuses a self-signed Authenticode PFX (signing.pfx).
    - Updates InternalsVisibleTo entries with the public key.
    - Builds the solution with the requested configuration.
    - Authenticode-signs the built SmartHopper assemblies.

    Use -Testing for fully automated, non-interactive builds in secure testing environments.
    When -Testing is set:

    - The PFX certificate is generated with password "test" if signing.pfx does not exist.
    - The same password "test" is used for Authenticode signing.
    - No password prompt is shown, and the entire compilation and signing pipeline runs unattended.

    Use -OutErrors and/or -OutWarnings to restrict dotnet build console output to only compilation
    errors and/or warnings. These switches are useful for CI scenarios that need to inspect
    compilation results concisely. They may be used together with -Testing. When neither switch is
    specified, the default output filter (ErrorsOnly;Summary) is used.
.PARAMETER Configuration
    Build configuration: Debug or Release (default: Debug).
.PARAMETER PfxPassword
    SecureString password for the signing.pfx certificate. Ignored when -Testing is used.
.PARAMETER Testing
    Automated testing mode: creates the signing certificate with password "test" if missing and uses
    that password for signing. No interactive prompts are shown.
.PARAMETER OutErrors
    Show only compilation errors from dotnet build.
.PARAMETER OutWarnings
    Show only compilation warnings from dotnet build.
#>
param(
    [string]$Configuration = "Debug",
    [System.Security.SecureString]$PfxPassword,
    [switch]$Testing,
    [switch]$OutErrors,
    [switch]$OutWarnings
)

$ErrorActionPreference = "Stop"

# In testing mode, force a predictable password and never prompt.
if ($Testing) {
    $testPasswordPlain = "test"
    $PfxPassword = ConvertTo-SecureString -String $testPasswordPlain -AsPlainText -Force
}

function Get-PlainPassword {
    return [System.Net.NetworkCredential]::new("", $PfxPassword).Password
}

$solutionRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $solutionRoot "SmartHopper.sln"
$snkPath = Join-Path $solutionRoot "signing.snk"
$pfxPath = Join-Path $solutionRoot "signing.pfx"
$solutionPropsPath = Join-Path $solutionRoot "Solution.props"

$signStrongScript = Join-Path $PSScriptRoot "Sign-StrongNames.ps1"
$signAuthScript = Join-Path $PSScriptRoot "Sign-Authenticode.ps1"
$updateInternalsScript = Join-Path $PSScriptRoot "Update-InternalsVisibleTo.ps1"

if (-not (Test-Path $signStrongScript)) {
    Write-Error "Sign-StrongNames.ps1 not found at $signStrongScript"
    exit 1
}
if (-not (Test-Path $signAuthScript)) {
    Write-Error "Sign-Authenticode.ps1 not found at $signAuthScript"
    exit 1
}
if (-not (Test-Path $updateInternalsScript)) {
    Write-Error "Update-InternalsVisibleTo.ps1 not found at $updateInternalsScript"
    exit 1
}

# 1. Ensure signing.pfx exists in solution root
if (-not (Test-Path $pfxPath)) {
    if (-not $PfxPassword) {
        $PfxPassword = Read-Host "Enter password for signing.pfx (will be used for Authenticode signing)" -AsSecureString
    }

    Write-Host "signing.pfx not found. Generating new PFX at $pfxPath via Sign-Authenticode.ps1" -ForegroundColor Cyan
    $generatePassword = if ($Testing) { $testPasswordPlain } else { Get-PlainPassword }
    & $signAuthScript -Generate -Password $generatePassword -PfxPath $pfxPath
} else {
    Write-Host "signing.pfx already exists at $pfxPath; skipping generation." -ForegroundColor DarkGray
}

# 2. Ensure signing.snk exists in solution root
if (-not (Test-Path $snkPath)) {
    Write-Host "signing.snk not found. Generating new SNK at $snkPath via Sign-StrongNames.ps1" -ForegroundColor Cyan
    & $signStrongScript -Generate -SnkPath $snkPath
} else {
    Write-Host "signing.snk already exists at $snkPath; skipping generation." -ForegroundColor DarkGray
}

# 3. Update InternalsVisibleTo using Update-InternalsVisibleTo.ps1
Write-Host "Updating InternalsVisibleTo entries using Update-InternalsVisibleTo.ps1" -ForegroundColor Cyan
& $updateInternalsScript -SnkPath $snkPath

# 4. Build the solution
if (-not (Test-Path $solutionPath)) {
    Write-Error "Solution file not found at $solutionPath"
    exit 1
}

$solutionVersion = $null
if (Test-Path $solutionPropsPath) {
    try {
        $xml = [xml](Get-Content $solutionPropsPath -Raw)
        $solutionVersion = $xml.Project.PropertyGroup.SolutionVersion
        if ($solutionVersion) {
            Write-Host "Detected SolutionVersion from Solution.props: $solutionVersion" -ForegroundColor Cyan
        }
    } catch {
        Write-Warning "Failed to read SolutionVersion from Solution.props: $_"
    }
}

Write-Host "Building solution $solutionPath with configuration '$Configuration'" -ForegroundColor Cyan
$loggerParams = @()
if ($OutErrors) { $loggerParams += "ErrorsOnly" }
if ($OutWarnings) { $loggerParams += "WarningsOnly" }
if ($loggerParams.Count -eq 0) { $loggerParams = @("ErrorsOnly", "Summary") }
$clp = $loggerParams -join ";"
& dotnet build $solutionPath -c $Configuration -clp:$clp
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

if (-not $solutionVersion) {
    Write-Error "SolutionVersion could not be determined from Solution.props; cannot resolve build output folder."
    exit 1
}

$buildRoot = Join-Path $solutionRoot ("bin/" + $solutionVersion + "/" + $Configuration)
if (-not (Test-Path $buildRoot)) {
    Write-Error "Build output folder '$buildRoot' not found. Ensure the solution builds into bin/<version>/<configuration>."
    exit 1
}

# 5. Authenticode-sign assemblies in build folder
# Password prompting is handled by Sign-Authenticode.ps1 if not provided
Write-Host "Authenticode-signing SmartHopper assemblies under '$buildRoot' using signing.pfx at $pfxPath" -ForegroundColor Cyan
if ($PfxPassword) {
    & $signAuthScript -Sign $buildRoot -Password (Get-PlainPassword) -PfxPath $pfxPath
} else {
    & $signAuthScript -Sign $buildRoot -PfxPath $pfxPath
}
if ($LASTEXITCODE -ne 0) {
    Write-Error "Authenticode signing failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Build-Solution completed successfully." -ForegroundColor Green
