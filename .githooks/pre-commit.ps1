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
    # Capture pre-script state of all files that might be modified
    $solutionPropsBefore = if (Test-Path $solutionPropsPath) { Get-Content $solutionPropsPath -Raw -Encoding utf8 } else { $null }
    $readmeBefore = if (Test-Path $readmePath) { Get-Content $readmePath -Raw -Encoding utf8 } else { $null }
    $changelogBefore = if (Test-Path $changelogPath) { Get-Content $changelogPath -Raw -Encoding utf8 } else { $null }

    & $versionScript -UpdateDateOnly
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Version update script failed (exit $LASTEXITCODE)."
        exit $LASTEXITCODE
    }

    # Stage only files that actually changed
    $anyStaged = $false

    # Check and stage Solution.props if changed
    $solutionPropsAfter = if (Test-Path $solutionPropsPath) { Get-Content $solutionPropsPath -Raw -Encoding utf8 } else { $null }
    if ($solutionPropsBefore -ne $solutionPropsAfter) {
        git add $solutionPropsPath 2>$null
        Write-Host "Staged changes to Solution.props." -ForegroundColor Green
        $anyStaged = $true
    }

    # Check and stage README.md if changed
    $readmeAfter = if (Test-Path $readmePath) { Get-Content $readmePath -Raw -Encoding utf8 } else { $null }
    if ($readmeBefore -ne $readmeAfter) {
        git add $readmePath 2>$null
        Write-Host "Staged changes to README.md." -ForegroundColor Green
        $anyStaged = $true
    }

    # Check and stage CHANGELOG.md if changed (using selective line staging)
    $changelogAfter = if (Test-Path $changelogPath) { Get-Content $changelogPath -Raw -Encoding utf8 } else { $null }
    if ($changelogBefore -ne $changelogAfter) {
        Write-Host "Staging only changed lines in CHANGELOG.md..." -ForegroundColor Cyan

        # Get the diff and apply only the actual changes (not the entire file)
        $diffOutput = git diff $changelogPath
        if ($diffOutput) {
            # Apply the diff to staging area using git apply --cached
            # This stages only the specific lines that changed
            $diffOutput | git apply --cached - 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Staged only changed lines in CHANGELOG.md." -ForegroundColor Green
                $anyStaged = $true
            }
            else {
                Write-Warning "Failed to stage selective changes, falling back to full file staging."
                git add $changelogPath 2>$null
                $anyStaged = $true
            }
        }
    }

    if (-not $anyStaged) {
        Write-Host "No version-related changes to stage." -ForegroundColor Yellow
    }
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
