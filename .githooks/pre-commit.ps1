# Purpose: Enforces anonymization of SmartHopperPublicKey and updates version date before every commit.

$repoRoot = Split-Path -Parent $PSScriptRoot
$anonymizeScript = Join-Path $repoRoot "tools\Anonymize-SmartHopperPublicKey.ps1"
$versionScript = Join-Path $repoRoot "tools\Change-SolutionVersion.ps1"
$csprojPath = Join-Path $repoRoot "src\SmartHopper.Infrastructure\SmartHopper.Infrastructure.csproj"
$solutionPropsPath = Join-Path $repoRoot "Solution.props"
$readmePath = Join-Path $repoRoot "README.md"
$changelogPath = Join-Path $repoRoot "CHANGELOG.md"
$expectedPlaceholder = "This value is automatically replaced by the build tooling before official builds."

# ===== Step 1: Regenerate labels.yml and labeler.yml =====
Write-Host "Step 1: Regenerating labels.yml and labeler.yml..." -ForegroundColor Cyan
$labelScript = Join-Path $repoRoot "tools\Update-GitHubLabels.ps1"
$labelsPath = Join-Path $repoRoot ".github\labels.yml"
$labelerPath = Join-Path $repoRoot ".github\labeler.yml"
if (Test-Path $labelScript) {
    $labelsBefore = if (Test-Path $labelsPath) { Get-Content $labelsPath -Raw -Encoding utf8 } else { $null }
    $labelerBefore = if (Test-Path $labelerPath) { Get-Content $labelerPath -Raw -Encoding utf8 } else { $null }
    & $labelScript -Apply
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Label update script failed (exit $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
    $labelsAfter = if (Test-Path $labelsPath) { Get-Content $labelsPath -Raw -Encoding utf8 } else { $null }
    $labelerAfter = if (Test-Path $labelerPath) { Get-Content $labelerPath -Raw -Encoding utf8 } else { $null }
    $anyStaged = $false
    if ($labelsBefore -ne $labelsAfter) {
        git add $labelsPath 2>$null
        Write-Host "Staged updated labels.yml." -ForegroundColor Green
        $anyStaged = $true
    }
    if ($labelerBefore -ne $labelerAfter) {
        git add $labelerPath 2>$null
        Write-Host "Staged updated labeler.yml." -ForegroundColor Green
        $anyStaged = $true
    }
    if (-not $anyStaged) {
        Write-Host "No label file changes to stage." -ForegroundColor Yellow
    }
}
else {
    Write-Warning "Label script not found at $labelScript, skipping label regeneration."
}

# ===== Step 2: Update license headers =====
Write-Host "`nStep 2: Updating license headers..." -ForegroundColor Cyan
$licenseScript = Join-Path $repoRoot "tools\Update-LicenseHeaders.ps1"
if (Test-Path $licenseScript) {
    & $licenseScript
    if ($LASTEXITCODE -ne 0) {
        Write-Error "License header update script failed (exit $LASTEXITCODE)."
        exit $LASTEXITCODE
    }

    # Stage any modified .cs or .csproj files under src/ that were already staged
    $modifiedSrcFiles = git diff --cached --name-only | Where-Object { $_ -match '^src\/.*\.(cs|csproj)$' }
    if ($modifiedSrcFiles) {
        foreach ($file in $modifiedSrcFiles) {
            git add $file 2>$null
            Write-Host "Staged updated license header: $file" -ForegroundColor Green
        }
    }
    else {
        Write-Host "No license header changes to stage." -ForegroundColor Yellow
    }
}
else {
    Write-Warning "License header script not found at $licenseScript, skipping license header update."
}

# ===== Step 3: Update version date =====
Write-Host "`nStep 3: Updating version date..." -ForegroundColor Cyan

# Determine whether to skip version update based on staged files
$stagedFiles = git diff --cached --name-only
$hasSrcChanges = $false
foreach ($file in $stagedFiles) {
    if ($file -match '^src\/') {
        $hasSrcChanges = $true
        break
    }
}

if (-not $hasSrcChanges) {
    Write-Host "Skipping version update: no staged files under src/." -ForegroundColor Yellow
}
elseif (Test-Path $versionScript) {
    # Capture pre-script state of all files that might be modified
    $solutionPropsBefore = if (Test-Path $solutionPropsPath) { Get-Content $solutionPropsPath -Raw -Encoding utf8 } else { $null }
    $readmeBefore = if (Test-Path $readmePath) { Get-Content $readmePath -Raw -Encoding utf8 } else { $null }
    $changelogBefore = if (Test-Path $changelogPath) { Get-Content $changelogPath -Raw -Encoding utf8 } else { $null }

    & $versionScript -DateOnly
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

# ===== Step 4: Anonymize SmartHopperPublicKey =====
Write-Host "`nStep 4: Anonymizing SmartHopperPublicKey..." -ForegroundColor Cyan

# Check if SmartHopper.Infrastructure.csproj is staged for commit
$stagedFiles = git diff --cached --name-only
$csprojRelativePath = "src/SmartHopper.Infrastructure/SmartHopper.Infrastructure.csproj"
$csprojIsStaged = $stagedFiles -contains $csprojRelativePath

if (-not $csprojIsStaged) {
    Write-Host "SmartHopper.Infrastructure.csproj is not staged - skipping anonymization." -ForegroundColor Yellow
    Write-Host "`nAll pre-commit checks passed. Proceeding with commit." -ForegroundColor Green
    exit 0
}

if (-not (Test-Path $anonymizeScript)) {
    Write-Error "Anonymize script not found at $anonymizeScript"
    exit 1
}

if (-not (Test-Path $csprojPath)) {
    Write-Error "Target csproj not found at $csprojPath"
    exit 1
}

# Run anonymization to guarantee the placeholder is present.
# Capture state before anonymization
$csprojBefore = if (Test-Path $csprojPath) { Get-Content $csprojPath -Raw -Encoding utf8 } else { $null }

& $anonymizeScript -CsprojPath $csprojPath
if ($LASTEXITCODE -ne 0) {
    Write-Error "Anonymization script failed (exit $LASTEXITCODE)."
    exit $LASTEXITCODE
}

# Stage only changed lines in Infrastructure.csproj (similar to CHANGELOG.md)
$csprojAfter = if (Test-Path $csprojPath) { Get-Content $csprojPath -Raw -Encoding utf8 } else { $null }
if ($csprojBefore -ne $csprojAfter) {
    Write-Host "Staging only changed lines in SmartHopper.Infrastructure.csproj..." -ForegroundColor Cyan

    # Get the diff and apply only the actual changes (not the entire file)
    $diffOutput = git diff $csprojPath
    if ($diffOutput) {
        # Apply the diff to staging area using git apply --cached
        # This stages only the specific lines that changed
        $diffOutput | git apply --cached - 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Staged only changed lines in SmartHopper.Infrastructure.csproj." -ForegroundColor Green
        }
        else {
            Write-Warning "Failed to stage selective changes, falling back to full file staging."
            git add $csprojPath 2>$null
        }
    }
}

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
