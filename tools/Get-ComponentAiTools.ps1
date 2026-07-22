# Extract declared AI tools from a SmartHopper component .cs file
# Reads the UsingAiTools override and returns the tool name(s)

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path
)

# Resolve path relative to script location if not absolute
if (-not [System.IO.Path]::IsPathRooted($Path)) {
    $Path = Join-Path (Split-Path -Parent $PSScriptRoot) $Path
}

if (-not (Test-Path $Path)) {
    Write-Error "File not found: $Path"
    exit 1
}

$content = Get-Content -Raw $Path
$allTools = [System.Collections.Generic.List[string]]::new()

# 1) Match: protected override IReadOnlyList<string> UsingAiTools => <value>;
if ($content -match 'protected\s+override\s+IReadOnlyList<string>\s+UsingAiTools\s*=>\s*(?<value>.+?);') {
    $value = $Matches['value']
    $pattern1 = '"(?<tool>[^"]+)"'
    $tools = [regex]::Matches($value, $pattern1) | ForEach-Object { $_.Groups['tool'].Value }
    if ($tools.Count -gt 0) {
        $allTools.AddRange([string[]]$tools)
    }
}

# 2) Match: CallAiToolAsync("toolname", ...)
$callAiToolPattern = 'CallAiToolAsync\s*\(\s*"(?<tool>[^"]+)"'
$callAiToolMatches = [regex]::Matches($content, $callAiToolPattern)
foreach ($match in $callAiToolMatches) {
    $null = $allTools.Add($match.Groups['tool'].Value)
}

# 3) Match: toolCall.Endpoint = "toolname";
$endpointPattern = 'Endpoint\s*=\s*"(?<tool>[^"]+)"'
$endpointMatches = [regex]::Matches($content, $endpointPattern)
foreach ($match in $endpointMatches) {
    $null = $allTools.Add($match.Groups['tool'].Value)
}

# 4) Match: new AIInteractionToolCall { ... Name = "toolname" ... }
$interactionToolCallPattern = 'new\s+AIInteractionToolCall\s*\{[^{}]*Name\s*=\s*"(?<tool>[^"]+)"'
$interactionMatches = [regex]::Matches($content, $interactionToolCallPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
foreach ($match in $interactionMatches) {
    $null = $allTools.Add($match.Groups['tool'].Value)
}

# Output unique tools
$uniqueTools = $allTools | Select-Object -Unique
if ($uniqueTools.Count -gt 0) {
    $uniqueTools | ForEach-Object { $_ }
}

exit 0
