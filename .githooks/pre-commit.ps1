# Purpose: Enforces anonymization of SmartHopperPublicKey and updates version date before every commit.

$repoRoot = Split-Path -Parent $PSScriptRoot
$anonymizeScript = Join-Path $repoRoot "tools\Anonymize-SmartHopperPublicKey.ps1"
$versionScript = Join-Path $repoRoot "tools\Change-SolutionVersion.ps1"
$csprojPath = Join-Path $repoRoot "src\SmartHopper.Infrastructure\SmartHopper.Infrastructure.csproj"
$solutionPropsPath = Join-Path $repoRoot "Solution.props"
$readmePath = Join-Path $repoRoot "README.md"
$changelogPath = Join-Path $repoRoot "CHANGELOG.md"
$expectedPlaceholder = "This value is automatically replaced by the build tooling before official builds."

# ===== Step 1: Update version date =====
Write-Host "Step 1: Updating version date..." -ForegroundColor Cyan
if (Test-Path $versionScript) {
    & $versionScript -UpdateDateOnly
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Version update script failed (exit $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
    # Stage modified files
    git add $solutionPropsPath 2>$null
    git add $readmePath 2>$null
    git add $changelogPath 2>$null
    Write-Host "Version date updated and files staged." -ForegroundColor Green
}
else {
    Write-Warning "Version script not found at $versionScript, skipping version update."
}

# ===== Step 2: Anonymize SmartHopperPublicKey =====
Write-Host "`nStep 2: Anonymizing SmartHopperPublicKey..." -ForegroundColor Cyan

if (-not (Test-Path $anonymizeScript)) {
    Write-Error "Anonymize script not found at $anonymizeScript"
    exit 1
}

if (-not (Test-Path $csprojPath)) {
    Write-Error "Target csproj not found at $csprojPath"
    exit 1
}

# Run anonymization to guarantee the placeholder is present.
& $anonymizeScript -CsprojPath $csprojPath
if ($LASTEXITCODE -ne 0) {
    Write-Error "Anonymization script failed (exit $LASTEXITCODE)."
    exit $LASTEXITCODE
}

# Stage the modified file so it's included in the current commit
git add $csprojPath

# Verify the placeholder was applied to block commits with real keys.
try {
    $xml = [xml](Get-Content $csprojPath -Raw)
    $keyElement = $xml.SelectSingleNode("//SmartHopperPublicKey")
    if (-not $keyElement) {
        Write-Error "SmartHopperPublicKey element not found in $csprojPath"
        exit 1
    }

    if ($keyElement.InnerText -ne $expectedPlaceholder) {
        Write-Error "SmartHopperPublicKey is not anonymized. Expected placeholder text."
        exit 1
    }
} catch {
    Write-Error "Failed to verify SmartHopperPublicKey placeholder: $_"
    exit 1
}

Write-Host "`nAll pre-commit checks passed. Proceeding with commit." -ForegroundColor Green
