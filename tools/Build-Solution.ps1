param(
    [string]$Configuration = "Debug",
    [System.Security.SecureString]$PfxPassword
)

$ErrorActionPreference = "Stop"

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

    Write-Host "signing.pfx not found. Generating new PFX at $pfxPath via Sign-Authenticode.ps1"
    & $signAuthScript -Generate -Password $PfxPassword -PfxPath $pfxPath
} else {
    Write-Host "signing.pfx already exists at $pfxPath; skipping generation."
}

# 2. Ensure signing.snk exists in solution root
if (-not (Test-Path $snkPath)) {
    Write-Host "signing.snk not found. Generating new SNK at $snkPath via Sign-StrongNames.ps1"
    & $signStrongScript -Generate -SnkPath $snkPath
} else {
    Write-Host "signing.snk already exists at $snkPath; skipping generation."
}

# 3. Update InternalsVisibleTo using Update-InternalsVisibleTo.ps1
Write-Host "Updating InternalsVisibleTo entries using Update-InternalsVisibleTo.ps1"
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
            Write-Host "Detected SolutionVersion from Solution.props: $solutionVersion"
        }
    } catch {
        Write-Warning "Failed to read SolutionVersion from Solution.props: $_"
    }
}

Write-Host "Building solution $solutionPath with configuration '$Configuration'"
& dotnet build $solutionPath -c $Configuration -clp:"ErrorsOnly;Summary"
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
Write-Host "Authenticode-signing SmartHopper assemblies under '$buildRoot' using signing.pfx at $pfxPath"
if ($PfxPassword) {
    & $signAuthScript -Sign $buildRoot -Password $PfxPassword -PfxPath $pfxPath
} else {
    & $signAuthScript -Sign $buildRoot -PfxPath $pfxPath
}
if ($LASTEXITCODE -ne 0) {
    Write-Error "Authenticode signing failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Build-Solution completed successfully."
