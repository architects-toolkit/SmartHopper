# Purpose: Enforces anonymization of SmartHopperPublicKey before every commit.

$repoRoot = Split-Path -Parent $PSScriptRoot
$anonymizeScript = Join-Path $repoRoot "tools\Anonymize-SmartHopperPublicKey.ps1"
$csprojPath = Join-Path $repoRoot "src\SmartHopper.Infrastructure\SmartHopper.Infrastructure.csproj"
$expectedPlaceholder = "This value is automatically replaced by the build tooling before official builds."

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

Write-Host "SmartHopperPublicKey anonymized and verified. Proceeding with commit."
