<#
.SYNOPSIS
    Promotes an AI model to Verified = true in the corresponding *ProviderModels.cs file.

.DESCRIPTION
    Used by the model-verification workflow once two users have certified a model.
    Locates the `new AIModelCapabilities { ... Model = "<Model>" ... }` block inside
    src/SmartHopper.Providers.<Provider>/<Provider>ProviderModels.cs and ensures the
    `Verified` flag is set to `true` (replacing `Verified = false`, or inserting the
    line right after `Model = "..."` when missing).

    Exits with code 0 when the file was modified, 1 if no change was needed (already
    verified), and 2 on error (model/provider not found).

.PARAMETER Provider
    Provider folder name, e.g. OpenAI, Anthropic, MistralAI, DeepSeek, OpenRouter, Gemini.

.PARAMETER Model
    Exact model identifier, e.g. "mistral-medium-latest".

.EXAMPLE
    pwsh -File tools/Update-ModelVerified.ps1 -Provider MistralAI -Model mistral-medium-latest
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $Provider,
    [Parameter(Mandatory = $true)][string] $Model
)

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$providerDir = Join-Path $repoRoot "src/SmartHopper.Providers.$Provider"
$file = Join-Path $providerDir "${Provider}ProviderModels.cs"

if (-not (Test-Path $file)) {
    Write-Error "Provider models file not found: $file"
    exit 2
}

$lines = Get-Content -LiteralPath $file
$modelPattern = '^\s*Model\s*=\s*"' + [regex]::Escape($Model) + '"\s*,\s*$'

# Find the model declaration line
$modelLineIndex = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match $modelPattern) {
        $modelLineIndex = $i
        break
    }
}

if ($modelLineIndex -lt 0) {
    Write-Error "Model '$Model' not found in $file"
    exit 2
}

# Walk back to the opening brace `{` of this object initializer
$blockStart = -1
for ($i = $modelLineIndex; $i -ge 0; $i--) {
    if ($lines[$i] -match '\bnew\s+AIModelCapabilities\b') {
        $blockStart = $i
        break
    }
}
if ($blockStart -lt 0) { $blockStart = $modelLineIndex }

# Walk forward to find the matching closing brace `}` using a brace counter
$depth = 0
$started = $false
$blockEnd = -1
for ($i = $blockStart; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    foreach ($ch in $line.ToCharArray()) {
        if ($ch -eq '{') { $depth++; $started = $true }
        elseif ($ch -eq '}') { $depth-- }
    }
    if ($started -and $depth -le 0) {
        $blockEnd = $i
        break
    }
}
if ($blockEnd -lt 0) {
    Write-Error "Could not find end of AIModelCapabilities block for model '$Model'."
    exit 2
}

# Inspect the block for Verified flag
$verifiedRegex = '^(?<indent>\s*)Verified\s*=\s*(?<val>true|false)\s*,\s*$'
$verifiedIndex = -1
$verifiedValue = $null
for ($i = $blockStart; $i -le $blockEnd; $i++) {
    if ($lines[$i] -match $verifiedRegex) {
        $verifiedIndex = $i
        $verifiedValue = $Matches.val
        break
    }
}

if ($verifiedIndex -ge 0 -and $verifiedValue -eq 'true') {
    Write-Host "Model '$Provider/$Model' is already Verified = true."
    exit 1
}

if ($verifiedIndex -ge 0) {
    $lines[$verifiedIndex] = $lines[$verifiedIndex] -replace 'Verified\s*=\s*false', 'Verified = true'
}
else {
    # Insert `Verified = true,` right after the Model line, using the same indentation
    $indent = ($lines[$modelLineIndex] -replace '^(?<i>\s*).*$', '${i}')
    $newLine = "${indent}Verified = true,"
    $head = $lines[0..$modelLineIndex]
    $tail = if ($modelLineIndex + 1 -le $lines.Count - 1) { $lines[($modelLineIndex + 1)..($lines.Count - 1)] } else { @() }
    $lines = @($head + $newLine + $tail)
}

# Preserve original line endings (CRLF for .cs files in this repo)
$content = ($lines -join "`r`n")
if (-not $content.EndsWith("`r`n")) { $content += "`r`n" }
[System.IO.File]::WriteAllText($file, $content, $utf8NoBom)

Write-Host "Promoted '$Provider/$Model' to Verified = true in $file"
exit 0
