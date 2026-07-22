<#
.SYNOPSIS
    Queries OpenRouter as the single source of truth for model metadata,
    then updates the corresponding *ProviderModels.cs file.

.DESCRIPTION
    Calls OpenRouter's /api/v1/models endpoint (rich metadata including
    architecture.input_modalities, architecture.output_modalities,
    supported_parameters, context_length, and expiration_date).

    For every model returned by OpenRouter that belongs to the requested
    provider:
      - If it already exists in the source file → update Capabilities,
        ContextLimit, and Deprecated based on expiration_date.
      - If it is missing from the source file → auto-insert a new
        AIModelCapabilities block with mapped flags.

    Models present in the source file but absent from OpenRouter are marked
    Deprecated = true.

    When expiration_date is set and closer than one year from now,
    the model is also marked Deprecated = true.

.PARAMETER Provider
    Provider name matching the folder under src/SmartHopper.Providers.<Provider>,
    e.g. OpenAI, MistralAI, Anthropic, OpenRouter, DeepSeek, Gemini.

.PARAMETER OpenRouterApiKey
    OpenRouter API key. The same key is used for every provider because
    OpenRouter is the primary source of truth.

.PARAMETER ProviderApiKey
    Optional. The provider's own API key. When supplied, the provider's
    official /models endpoint is queried as a secondary source so that
    models exposed by the provider but not yet listed on OpenRouter are
    still added to the source file (with conservative default capabilities).
    A model is only marked Deprecated when it is missing from BOTH OpenRouter
    and (when queried) the provider's own API.

    When the provider API response includes alias information, aliases are
    merged into the Aliases list of the corresponding model entry (additive:
    existing hand-curated aliases are preserved and new ones are appended).

    Alias support by provider API:
      MistralAI  - "aliases" array returned on each model object.
      Anthropic  - no alias mapping in list response (alias IDs appear as
                   separate model entries; no reverse link is exposed).
      OpenAI     - no alias field in the model object.
      DeepSeek   - no alias field in the model object.

.PARAMETER PromptKeys
    Switch. When present, the script interactively prompts for the
    OpenRouter API key and (optionally) the provider API key via
    Read-Host. This is useful when running the script locally and you
    prefer not to pass keys on the command line.

.PARAMETER TargetFile
    Optional. Absolute or repo-relative path to the *ProviderModels.cs file.
    Defaults to src/SmartHopper.Providers.<Provider>/<Provider>ProviderModels.cs.

.PARAMETER UpdateFile
    When present, the source file is rewritten with new models inserted,
    existing models updated, and disappeared models marked as deprecated.

.OUTPUTS
    A JSON string written to stdout with the following shape:
    {
      "provider": "OpenAI",
      "apiUrl": "https://openrouter.ai/api/v1/models",
      "apiModels": [ "gpt-4o", "gpt-4o-mini" ],
      "openrouterModels": [ "gpt-4o", "gpt-4o-mini" ],
      "providerApiModels": [ "gpt-4o", "gpt-4o-mini" ],
      "sourceModels": [ "gpt-4", "gpt-4o" ],
      "newModels": [ "gpt-4o-mini" ],
      "deprecatedModels": [ "gpt-4" ],
      "unchangedModels": [ "gpt-4o" ],
      "fileUpdated": true
    }

.EXAMPLE
    .\tools\Update-ProviderModels.ps1 -Provider OpenAI -OpenRouterApiKey $env.OPENROUTER_API_KEY

    .\tools\Update-ProviderModels.ps1 -Provider Anthropic -OpenRouterApiKey $env.OPENROUTER_API_KEY -UpdateFile
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)][string] $Provider,
    [Parameter(Mandatory = $false)][string] $OpenRouterApiKey = "",
    [Parameter(Mandatory = $false)][string] $ProviderApiKey = "",
    [Parameter(Mandatory = $false)][string] $TargetFile = "",
    [Parameter(Mandatory = $false)][switch] $UpdateFile,
    [Parameter(Mandatory = $false)][switch] $FailOnValidationErrors,
    [Parameter(Mandatory = $false)][switch] $ValidateOnly,
    [Parameter(Mandatory = $false)][switch] $Help,
    [Parameter(Mandatory = $false)][switch] $PromptKeys
)

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

# ---------------------------------------------------------------------------
# Help
# ---------------------------------------------------------------------------
if ($Help) {
    $helpText = @"
Update-ProviderModels.ps1
=========================
Queries OpenRouter (and optionally the provider's own API) for model metadata,
then updates or validates the corresponding *ProviderModels.cs file.

SYNTAX
------
    .\Update-ProviderModels.ps1 -Provider <String> [-OpenRouterApiKey <String>]
        [-ProviderApiKey <String>] [-TargetFile <String>] [-UpdateFile]
        [-FailOnValidationErrors] [-ValidateOnly] [-PromptKeys]

    .\Update-ProviderModels.ps1 -Help

PARAMETERS
----------
    -Provider <String>
        Required (unless -Help is used).
        Provider name matching the folder under src/SmartHopper.Providers.<Provider>.
        Supported values: OpenAI, MistralAI, Anthropic, OpenRouter, DeepSeek, Gemini.

    -OpenRouterApiKey <String>
        Optional. OpenRouter API key.
        OpenRouter is queried as the primary source of truth for all providers.
        If omitted, the script falls back to checking the environment variable
        OPENROUTER_API_KEY.

    -ProviderApiKey <String>
        Optional. The provider's own API key.
        When supplied, the provider's official /models endpoint is also queried
        so that models exposed by the provider but not yet listed on OpenRouter
        are still added (with conservative default capabilities).
        Alias information is merged when available (MistralAI supports this).

    -TargetFile <String>
        Optional. Absolute or repo-relative path to the *ProviderModels.cs file.
        Defaults to:
          src/SmartHopper.Providers.<Provider>/<Provider>ProviderModels.cs

    -UpdateFile
        Switch. When present, the source file is rewritten with:
          - New models inserted
          - Existing models updated (Capabilities, ContextLimit, Deprecated)
          - Missing models marked as Deprecated = true
        Without this switch, the script runs in report-only mode.

    -FailOnValidationErrors
        Switch. Causes the script to exit with a non-zero code when validation
        errors are found (e.g. deprecated models still marked Default = true).

    -ValidateOnly
        Switch. Skips fetching live data and only performs static validation
        of the existing *ProviderModels.cs file.

    -PromptKeys
        Switch. Interactively prompts for the OpenRouter API key and
        (optionally) the provider API key via secure Read-Host input.
        Useful when running the script locally to avoid exposing keys
        in shell history.

    -Help
        Switch. Displays this help message and exits.

BEHAVIOUR
---------
  OpenRouter source of truth
    Every provider except OpenRouter itself is filtered by a provider-specific
    prefix (e.g. "openai/" for OpenAI). OpenRouter models are kept verbatim.

  Deprecation rules
    A model is marked Deprecated = true when:
      - It is absent from OpenRouter (and from the provider API if queried).
      - Its expiration_date on OpenRouter is closer than one year from now.

  Validation rules
    - A model that is Deprecated must NOT be marked Default = true.
    - A Default model must have a Rank value.
    - Aliases must resolve to a known model entry.
    - Every model id must be unique.
    - Only one Default model per provider.

EXAMPLES
--------
  # Report-only run for OpenAI (no file changes)
  .\Update-ProviderModels.ps1 -Provider OpenAI -OpenRouterApiKey `$env:OPENROUTER_API_KEY

  # Update the Anthropic model file
  .\Update-ProviderModels.ps1 -Provider Anthropic -OpenRouterApiKey `$env:OPENROUTER_API_KEY -UpdateFile

  # Enrich with MistralAI's own API to catch unreleased aliases
  .\Update-ProviderModels.ps1 -Provider MistralAI -OpenRouterApiKey `$env:OPENROUTER_API_KEY `
      -ProviderApiKey `$env:MISTRAL_API_KEY -UpdateFile

  # Validate existing file without network calls
  .\Update-ProviderModels.ps1 -Provider OpenAI -ValidateOnly -FailOnValidationErrors

  # Run interactively (prompts for keys so they don't appear in shell history)
  .\Update-ProviderModels.ps1 -Provider OpenAI -PromptKeys -UpdateFile

OUTPUT
------
    A JSON summary is written to stdout with the following shape:
    {
      "provider": "OpenAI",
      "apiUrl": "https://openrouter.ai/api/v1/models",
      "apiModels": [ "gpt-4o", "gpt-4o-mini" ],
      "openrouterModels": [ "gpt-4o", "gpt-4o-mini" ],
      "providerApiModels": [ "gpt-4o", "gpt-4o-mini" ],
      "sourceModels": [ "gpt-4", "gpt-4o" ],
      "newModels": [ "gpt-4o-mini" ],
      "deprecatedModels": [ "gpt-4" ],
      "unchangedModels": [ "gpt-4o" ],
      "fileUpdated": true
    }
"@
    Write-Host $helpText
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Provider)) {
    $Provider = Read-Host -Prompt "Enter provider name (e.g. OpenAI, Anthropic, MistralAI, OpenRouter, DeepSeek, Gemini)"
    if ([string]::IsNullOrWhiteSpace($Provider)) {
        Write-Error "Parameter -Provider is required. Run with -Help for usage information."
        exit 1
    }
}

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Interactive key prompting
# ---------------------------------------------------------------------------
if ($PromptKeys) {
    $secureOpenRouter = Read-Host -Prompt "Enter OpenRouter API Key" -AsSecureString
    $OpenRouterApiKey = [System.Net.NetworkCredential]::new('', $secureOpenRouter).Password

    $secureProvider = Read-Host -Prompt "Enter Provider API Key (optional, press Enter to skip)" -AsSecureString
    $providerPlain = [System.Net.NetworkCredential]::new('', $secureProvider).Password
    if (-not [string]::IsNullOrWhiteSpace($providerPlain)) {
        $ProviderApiKey = $providerPlain
    }
}

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------
$OpenRouterUrl    = 'https://openrouter.ai/api/v1/models'
$OneYearFromNow   = (Get-Date).AddYears(1)

# OpenRouter prefix used to filter and strip provider-specific models.
# Note: 'OpenRouter' is special-cased below: every OpenRouter model is kept
# verbatim (no prefix stripping) because that *is* the OpenRouter catalogue.
$ProviderPrefixes = @{
    'OpenAI'     = 'openai/'
    'Anthropic'  = 'anthropic/'
    'MistralAI'  = 'mistralai/'
    'DeepSeek'   = 'deepseek/'
    'Gemini'     = 'google/'
    'OpenRouter' = ''
}

# Per-provider regex matching the "version suffix" appended to a base model id.
# Used to group several provider-API ids that refer to the same logical model:
#   - dated suffix (immutable release):    -YYYYMMDD or -YYYY-MM-DD
#   - rolling alias:                       -latest
# The dated id is treated as canonical; bare and -latest ids become aliases.
# Set to $null for providers whose ids do not encode aliases this way.
$ProviderAliasSuffix = @{
    'OpenAI'     = '-(?:\d{4}-\d{2}-\d{2}|latest)$'
    'Anthropic'  = '-(?:\d{8}|latest)$'
    'MistralAI'  = $null   # uses .name field grouping (handled separately)
    'DeepSeek'   = $null
    'Gemini'     = $null
    'OpenRouter' = $null
}

$CompositeDefaultCapabilities = [ordered]@{
    Text2Text          = @('TextInput', 'TextOutput')
    ToolChat          = @('TextInput', 'TextOutput', 'FunctionCalling')
    ReasoningChat     = @('TextInput', 'TextOutput', 'Reasoning')
    ToolReasoningChat = @('TextInput', 'TextOutput', 'Reasoning', 'FunctionCalling')
    Text2Json         = @('TextInput', 'JsonOutput')
    Text2Image        = @('TextInput', 'ImageOutput')
    Text2Speech       = @('TextInput', 'AudioOutput')
    Speech2Text       = @('AudioInput', 'TextOutput')
    Image2Text        = @('ImageInput', 'TextOutput')
    Image2Image       = @('ImageInput', 'ImageOutput')
}

# Provider-native /models endpoints. Used only when -ProviderApiKey is supplied.
# Each entry returns the URL and a script block that builds auth headers.
# Note: OpenRouter is excluded because it uses the same endpoint as the OpenRouter source of truth.
# For OpenRouter, deprecation is handled by comparing existing models against the current OpenRouter catalogue.
$ProviderApis = @{
    'OpenAI'     = @{ Url = 'https://api.openai.com/v1/models';     Headers = { param($k) @{ Authorization = "Bearer $k" } } }
    'MistralAI'  = @{ Url = 'https://api.mistral.ai/v1/models';     Headers = { param($k) @{ Authorization = "Bearer $k" } } }
    'DeepSeek'   = @{ Url = 'https://api.deepseek.com/v1/models';   Headers = { param($k) @{ Authorization = "Bearer $k" } } }
    'Anthropic'  = @{ Url = 'https://api.anthropic.com/v1/models';  Headers = { param($k) @{ 'x-api-key' = $k; 'anthropic-version' = '2023-06-01' } } }
    'Gemini'     = @{ Url = 'https://generativelanguage.googleapis.com/v1beta/models'; Headers = { param($k) @{ 'x-goog-api-key' = $k } } }
}

# ---------------------------------------------------------------------------
# Helper: Resolve target file
# ---------------------------------------------------------------------------
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
if ([string]::IsNullOrWhiteSpace($TargetFile)) {
    $TargetFile = Join-Path $repoRoot "src/SmartHopper.Providers.$Provider/${Provider}ProviderModels.cs"
}
$TargetFile = Resolve-Path $TargetFile -ErrorAction Stop

# ---------------------------------------------------------------------------
# Helper: Parse a single AIModelCapabilities C# block into a PSObject
# ---------------------------------------------------------------------------
function ConvertFrom-ModelBlock($blockText) {
    $result = [ordered]@{
        RawBlock            = $blockText
        Provider            = $null
        Model               = $null
        Capabilities        = $null
        Default             = $null
        Verified            = $null
        Deprecated          = $null
        SupportsStreaming   = $null
        SupportsPromptCaching = $null
        Rank                = $null
        ContextLimit        = $null
        Created             = $null
        Pricing             = $null
        Aliases             = $null
        DiscouragedForTools = $null
        CacheKeyStrategy    = $null
    }

    $rxProps = @{
        Provider            = 'Provider\s*=\s*"([^"]*)"'
        Model               = 'Model\s*=\s*"([^"]*)"'
        Capabilities        = 'Capabilities\s*=\s*([^,\n]+)'
        Default             = 'Default\s*=\s*([^,\n]+)'
        Verified            = 'Verified\s*=\s*(true|false)'
        Deprecated          = 'Deprecated\s*=\s*(true|false)'
        SupportsStreaming   = 'SupportsStreaming\s*=\s*(true|false)'
        SupportsPromptCaching = 'SupportsPromptCaching\s*=\s*(true|false)'
        Rank                = 'Rank\s*=\s*(\d+)'
        ContextLimit        = 'ContextLimit\s*=\s*(\d+)'
        CacheKeyStrategy    = 'CacheKeyStrategy\s*=\s*"([^"]*)"'
    }

    foreach ($prop in $rxProps.GetEnumerator()) {
        $m = [regex]::Match($blockText, $prop.Value)
        if ($m.Success) {
            $result[$prop.Key] = $m.Groups[1].Value.Trim()
        }
    }

    # Created = new DateTime(YYYY, M, D[, H, M, S])
    $createdRx = [regex]::Match($blockText, 'Created\s*=\s*new\s+DateTime\s*\(\s*([^)]+?)\s*\)')
    if ($createdRx.Success) {
        $parts = $createdRx.Groups[1].Value -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
        if ($parts.Count -ge 3) {
            try {
                $y = [int]$parts[0]; $mo = [int]$parts[1]; $d = [int]$parts[2]
                $result.Created = [DateTime]::new($y, $mo, $d)
            } catch { }
        }
    }

    # Pricing = new AIModelPricing { ... }
    $pricingRx = [regex]::Match($blockText, 'Pricing\s*=\s*new\s+AIModelPricing\s*\{\s*([^}]*)\s*\}')
    if ($pricingRx.Success) {
        $priceBody = $pricingRx.Groups[1].Value
        $pricing = [ordered]@{}
        foreach ($pm in [regex]::Matches($priceBody, '(\w+)\s*=\s*([0-9]*\.?[0-9]+(?:[eE][-+]?\d+)?)m?')) {
            $pricing[$pm.Groups[1].Value] = $pm.Groups[2].Value
        }
        if ($pricing.Count -gt 0) { $result.Pricing = [pscustomobject]$pricing }
    }

    # Aliases
    $aliasesRx = [regex]::Match($blockText, 'Aliases\s*=\s*new\s+List<string>\s*\{\s*([^}]*)\s*\}')
    if ($aliasesRx.Success) {
        $result.Aliases = $aliasesRx.Groups[1].Value -split ',' | ForEach-Object { $_.Trim().Trim('"') } | Where-Object { $_ }
    }

    # DiscouragedForTools
    $discRx = [regex]::Match($blockText, 'DiscouragedForTools\s*=\s*new\s+List<string>\s*\{\s*([^}]*)\s*\}')
    if ($discRx.Success) {
        $result.DiscouragedForTools = $discRx.Groups[1].Value -split ',' | ForEach-Object { $_.Trim().Trim('"') } | Where-Object { $_ }
    }

    return [pscustomobject]$result
}

# ---------------------------------------------------------------------------
# Helper: Generate a C# AIModelCapabilities block from a merged model object
# ---------------------------------------------------------------------------
function Format-ModelBlock($model, $providerVar) {
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('                new AIModelCapabilities')
    $lines.Add('                {')
    $lines.Add("                    Provider = $providerVar,")
    $lines.Add("                    Model = `"$($model.Model)`",")

    if (-not [string]::IsNullOrWhiteSpace($model.Capabilities)) {
        $suffix = if ($model.Capabilities -eq 'AICapability.None') { ' // TODO: retrieve capabilities' } else { '' }
        $lines.Add("                    Capabilities = $($model.Capabilities),$suffix")
    }

    if (-not [string]::IsNullOrWhiteSpace($model.Default) -and $model.Default -ne 'AICapability.None') {
        $lines.Add("                    Default = $($model.Default),")
    }

    if ($model.SupportsStreaming -eq 'true')  { $lines.Add('                    SupportsStreaming = true,') }
    if ($model.SupportsStreaming -eq 'false') { $lines.Add('                    SupportsStreaming = false,') }

    if ($model.SupportsPromptCaching -eq 'true')  { $lines.Add('                    SupportsPromptCaching = true,') }
    if ($model.SupportsPromptCaching -eq 'false') { $lines.Add('                    SupportsPromptCaching = false,') }

    if ($model.Verified -eq 'true')  { $lines.Add('                    Verified = true,') }
    if ($model.Verified -eq 'false') { $lines.Add('                    Verified = false,') }

    if ($model.Deprecated -eq 'true') {
        $lines.Add('                    Deprecated = true,')
    }

    if (-not [string]::IsNullOrWhiteSpace($model.Rank)) {
        $lines.Add("                    Rank = $($model.Rank),")
    }

    if (-not [string]::IsNullOrWhiteSpace($model.ContextLimit)) {
        $lines.Add("                    ContextLimit = $($model.ContextLimit),")
    }

    if ($model.Created) {
        $dt = $null
        if ($model.Created -is [DateTime]) { $dt = $model.Created }
        else { try { $dt = [DateTime]::Parse([string]$model.Created) } catch { } }
        if ($dt -and $dt -ne [DateTime]::MinValue) {
            $lines.Add("                    Created = new DateTime($($dt.Year), $($dt.Month), $($dt.Day)),")
        }
    }

    if ($model.Pricing) {
        $priceProps = [System.Collections.Generic.List[string]]::new()
        foreach ($p in @('Prompt','Completion','Request','Image','ImageOutput','ImageToken','Audio','AudioOutput','InputAudioCache','InputCacheRead','InputCacheWrite','InternalReasoning','WebSearch','Discount')) {
            $v = $model.Pricing.$p
            if ($null -ne $v -and -not [string]::IsNullOrWhiteSpace([string]$v)) {
                $num = [string]$v
                # Strip trailing 'm' if present; normalize
                $num = $num.TrimEnd('m','M')
                [void]$priceProps.Add("                        $p = ${num}m,")
            }
        }
        if ($priceProps.Count -gt 0) {
            $lines.Add('                    Pricing = new AIModelPricing')
            $lines.Add('                    {')
            foreach ($pp in $priceProps) { $lines.Add($pp) }
            $lines.Add('                    },')
        }
    }

    if ($model.Aliases -and $model.Aliases.Count -gt 0) {
        $aliasParts = $model.Aliases | ForEach-Object { "`"$_`"" }
        $lines.Add("                    Aliases = new List<string> { $($aliasParts -join ', ') },")
    }

    if ($model.DiscouragedForTools -and $model.DiscouragedForTools.Count -gt 0) {
        $toolParts = $model.DiscouragedForTools | ForEach-Object { "`"$_`"" }
        $lines.Add("                    DiscouragedForTools = new List<string> { $($toolParts -join ', ') },")
    }

    if (-not [string]::IsNullOrWhiteSpace($model.CacheKeyStrategy)) {
        $lines.Add("                    CacheKeyStrategy = `"$($model.CacheKeyStrategy)`",")
    }

    $lines.Add('                },')
    return ($lines -join "`r`n")
}

# ---------------------------------------------------------------------------
# Helper: Map OpenRouter modalities + supported_parameters to AICapability flags
# ---------------------------------------------------------------------------
function ConvertTo-CapabilityFlags($openRouterModel) {
    $caps = [System.Collections.Generic.List[string]]::new()

    $arch = $openRouterModel.architecture
    $inputModalities  = if ($arch) { $arch.input_modalities } else { $null }
    $outputModalities = if ($arch) { $arch.output_modalities } else { $null }
    $supportedParams  = $openRouterModel.supported_parameters

    if ($inputModalities -contains 'text')   { $caps.Add('AICapability.TextInput') }
    if ($inputModalities -contains 'image')  { $caps.Add('AICapability.ImageInput') }
    if ($inputModalities -contains 'audio')  { $caps.Add('AICapability.AudioInput') }
    if ($inputModalities -contains 'video')  { $caps.Add('AICapability.VideoInput') }

    if ($outputModalities -contains 'text')  { $caps.Add('AICapability.TextOutput') }
    if ($outputModalities -contains 'image') { $caps.Add('AICapability.ImageOutput') }
    if ($outputModalities -contains 'audio') { $caps.Add('AICapability.AudioOutput') }
    if ($outputModalities -contains 'video') { $caps.Add('AICapability.VideoOutput') }

    if ($supportedParams -contains 'tools' -or
        $supportedParams -contains 'tool_choice' -or
        $supportedParams -contains 'parallel_tool_calls') {
        $caps.Add('AICapability.FunctionCalling')
    }

    if ($supportedParams -contains 'response_format' -or
        $supportedParams -contains 'structured_outputs') {
        $caps.Add('AICapability.JsonOutput')
    }

    if ($supportedParams -contains 'reasoning' -or
        $supportedParams -contains 'reasoning_effort' -or
        $supportedParams -contains 'include_reasoning') {
        $caps.Add('AICapability.Reasoning')
    }

    # Embed output is indicated by embedding-specific endpoints or model naming patterns
    # OpenRouter: embedding models expose 'embeddings' in output_modalities per the API spec
    if ($outputModalities -contains 'embeddings' -or
        $supportedParams -contains 'embeddings' -or
        $openRouterModel.id -match 'embed' -or
        $openRouterModel.description -match 'embed') {
        $caps.Add('AICapability.EmbedOutput')
    }

    if ($caps.Count -eq 0) {
        return 'AICapability.None'
    }
    return ($caps -join ' | ')
}

function Test-CapabilityExpressionContains($expression, $capabilityName) {
    return -not [string]::IsNullOrWhiteSpace($expression) -and $expression -match "(^|[^A-Za-z0-9_])AICapability\.$([regex]::Escape($capabilityName))([^A-Za-z0-9_]|$)"
}

function Test-CapabilityExpressionHasAll($expression, $capabilityNames) {
    foreach ($capabilityName in $capabilityNames) {
        if (-not (Test-CapabilityExpressionContains $expression $capabilityName)) {
            return $false
        }
    }
    return $true
}

function Test-RealtimeModelName($modelName) {
    return -not [string]::IsNullOrWhiteSpace($modelName) -and $modelName -match '(?i)realtime'
}

function Test-ProviderModelValidation($models) {
    $validationErrors = [System.Collections.Generic.List[string]]::new()
    $validationWarnings = [System.Collections.Generic.List[string]]::new()
    $missingDefaultCapabilities = [System.Collections.Generic.List[string]]::new()
    $pendingCapabilityModels = [System.Collections.Generic.List[string]]::new()
    $realtimeModels = [System.Collections.Generic.List[string]]::new()

    $nonDeprecatedModelsForValidation = @($models | Where-Object { $_.Deprecated -ne 'true' })

    # Helper: Check if a model is discouraged for all tools
    function Test-ModelDiscouragedForAllTools($model) {
        if ($model.DiscouragedForTools -and $model.DiscouragedForTools.Count -gt 0) {
            return $model.DiscouragedForTools -contains '*'
        }
        return $false
    }

    foreach ($composite in $CompositeDefaultCapabilities.GetEnumerator()) {
        $capableModels = @($nonDeprecatedModelsForValidation | Where-Object {
            Test-CapabilityExpressionHasAll $_.Capabilities $composite.Value -and
            -not (Test-ModelDiscouragedForAllTools $_)
        })

        if ($capableModels.Count -eq 0) { continue }

        $defaultModels = @($nonDeprecatedModelsForValidation | Where-Object {
            Test-CapabilityExpressionContains $_.Default $composite.Key -and
            -not (Test-ModelDiscouragedForAllTools $_)
        })

        if ($defaultModels.Count -eq 0) {
            [void]$missingDefaultCapabilities.Add($composite.Key)
            # Missing defaults are warnings, not errors. Some providers may only
            # serve a subset of capability categories (e.g. image-generation only).
            [void]$validationWarnings.Add("Missing default for AICapability.$($composite.Key) while $($capableModels.Count) non-deprecated model(s) support $($composite.Value -join ', ').")
        }
    }

    foreach ($m in $models) {
        if ($m.Deprecated -ne 'true' -and (
            [string]::IsNullOrWhiteSpace($m.Capabilities) -or
            $m.Capabilities -eq 'AICapability.None' -or
            $m.RawBlock -match '//\s*TODO:\s*retrieve capabilities')) {
            [void]$pendingCapabilityModels.Add($m.Model)
            [void]$validationErrors.Add("Model '$($m.Model)' is non-deprecated but has pending capability definition.")
        }

        if (Test-RealtimeModelName $m.Model) {
            [void]$realtimeModels.Add($m.Model)
            [void]$validationErrors.Add("Realtime model '$($m.Model)' is present in the provider model list.")
        }
    }

    return [ordered]@{
        success                    = ($validationErrors.Count -eq 0)
        errors                     = @($validationErrors)
        warnings                   = @($validationWarnings)
        missingDefaultCapabilities = @($missingDefaultCapabilities)
        pendingCapabilityModels    = @($pendingCapabilityModels)
        realtimeModels             = @($realtimeModels)
    }
}

# ---------------------------------------------------------------------------
# 1. Read and parse the existing C# file
# ---------------------------------------------------------------------------
# Read as UTF-8 text (handles BOM correctly), matching Update-LicenseHeaders.ps1
$sourceContent = [System.IO.File]::ReadAllText($TargetFile, [System.Text.Encoding]::UTF8)
# Normalize: strip BOM if present so regexes don't see \uFEFF
$sourceContent = $sourceContent -replace '^[\uFEFF]', ''

# Extract the provider variable name used inside RetrieveModels()
$providerVarRx = [regex]::Match($sourceContent, 'var\s+(\w+)\s*=\s*this\.\w+\.Name\.ToLowerInvariant\(\)\s*;')
$providerVar = if ($providerVarRx.Success) { $providerVarRx.Groups[1].Value } else { 'provider' }

# Find the RetrieveModels() model list boundaries using brace counting
$startMarkerRx = [regex]::new('var\s+models\s*=\s*new\s+List<AIModelCapabilities>\s*\n\s*\{\s*\n')
$startMatch = $startMarkerRx.Match($sourceContent)
if (-not $startMatch.Success) {
    Write-Error "Could not find model list start in $TargetFile"
    exit 5
}

$startIndex = $startMatch.Index + $startMatch.Length
$searchContent = $sourceContent.Substring($startIndex)

$depth = 1   # already inside the List<...> { initializer
$inString = $false
$stringChar = $null
$endOffset = -1

for ($i = 0; $i -lt $searchContent.Length; $i++) {
    $ch = $searchContent[$i]

    if ($inString) {
        if ($ch -eq $stringChar -and ($i -eq 0 -or $searchContent[$i - 1] -ne '\')) {
            $inString = $false
        }
        continue
    }

    if ($ch -eq '"' -or $ch -eq "'") {
        $inString = $true
        $stringChar = $ch
        continue
    }

    if ($ch -eq '{') { $depth++ }
    elseif ($ch -eq '}') {
        $depth--
        if ($depth -eq 0) {
            # check for trailing semicolon
            $j = $i + 1
            while ($j -lt $searchContent.Length -and [char]::IsWhiteSpace($searchContent[$j])) { $j++ }
            if ($j -lt $searchContent.Length -and $searchContent[$j] -eq ';') {
                $endOffset = $j
                break
            }
        }
    }
}

if ($endOffset -lt 0) {
    Write-Error "Could not find model list end in $TargetFile"
    exit 6
}

$beforeList = $sourceContent.Substring(0, $startIndex)
$listContent = $searchContent.Substring(0, $endOffset + 1)
$afterList  = $searchContent.Substring($endOffset + 1)

# Extract existing model blocks
$blockRx = [regex]::new('new\s+AIModelCapabilities\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\},?', [System.Text.RegularExpressions.RegexOptions]::Singleline)
$blockMatches = $blockRx.Matches($listContent)

$existingModels = @{ }
foreach ($bm in $blockMatches) {
    $parsed = ConvertFrom-ModelBlock -blockText $bm.Value
    if ($parsed.Model) {
        $existingModels[$parsed.Model] = $parsed
    }
}

Write-Host "[$Provider] Parsed $($existingModels.Count) existing model block(s)."

if ($ValidateOnly) {
    $validation = Test-ProviderModelValidation @($existingModels.Values)
    $report = [ordered]@{
        provider           = $Provider
        apiUrl             = $null
        providerApiQueried = $false
        providerApiUrl     = $null
        apiModels          = @()
        openrouterModels   = @()
        providerApiModels  = $null
        sourceModels       = @($existingModels.Keys | Sort-Object)
        newModels          = @()
        deprecatedModels   = @()
        unchangedModels    = @()
        fileUpdated        = $false
        validation          = $validation
    }

    Write-Output ($report | ConvertTo-Json -Depth 10)

    if ($validation.warnings.Count -gt 0) {
        foreach ($validationWarning in $validation.warnings) {
            Write-Host "::warning title=$Provider provider model validation::$validationWarning"
        }
    }

    if (-not $validation.success) {
        # Surface each validation issue as a GitHub Actions error annotation so
        # the message is visible in the run log. We deliberately use Write-Host
        # (not Write-Error) here: Write-Error under $ErrorActionPreference =
        # 'Stop' throws a terminating exception which propagates out of the
        # script as an opaque [System.Management.Automation.RuntimeException],
        # making downstream catch blocks (e.g. the fetch-models action wrapper)
        # surface a generic "script failed" message instead of the actual
        # validation details.
        foreach ($validationError in $validation.errors) {
            Write-Host "::error title=$Provider provider model validation::$validationError"
        }
    }

    if ($FailOnValidationErrors -and -not $validation.success) {
        exit 9
    }

    exit 0
}

# ---------------------------------------------------------------------------
# 2. Query OpenRouter
# ---------------------------------------------------------------------------
$headers = @{ Authorization = "Bearer $OpenRouterApiKey" }

try {
    $response = Invoke-RestMethod -Uri $OpenRouterUrl -Headers $headers -Method GET -TimeoutSec 60
}
catch {
    Write-Error "[$Provider] OpenRouter request failed: $($_.Exception.Message)"
    exit 7
}

if (-not $ProviderPrefixes.ContainsKey($Provider)) {
    Write-Error "Unknown provider '$Provider'. No OpenRouter prefix mapping."
    exit 8
}
$prefix = $ProviderPrefixes[$Provider]
$isOpenRouterProvider = ($Provider -eq 'OpenRouter')

# Helper: derive the model name stored in the source file from an OpenRouter id.
# For non-OpenRouter providers, also strips OpenRouter pricing-tier suffixes
# (e.g. ":free", ":extended") which are OpenRouter-specific tags.
function Get-ModelName($fullId) {
    if ($isOpenRouterProvider) { return $fullId }
    $name = $fullId.Substring($prefix.Length)
    # Strip pricing-tier suffix: "gemma-4-26b-a4b-it:free" -> "gemma-4-26b-a4b-it"
    $colonIdx = $name.IndexOf(':')
    if ($colonIdx -ge 0) { $name = $name.Substring(0, $colonIdx) }
    return $name
}

$openRouterModels = [System.Collections.Generic.List[psobject]]::new()
foreach ($item in $response.data) {
    $fullId = $item.id
    if ([string]::IsNullOrWhiteSpace($fullId)) { continue }

    if ($fullId.StartsWith('ft:')) { continue }
    if (Test-RealtimeModelName $fullId) { continue }

    if ($isOpenRouterProvider) {
        # OpenRouter provider: keep every model verbatim (full "vendor/model" id).
        $openRouterModels.Add($item)
    }
    elseif ($fullId.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        # Strip provider prefix: "openai/gpt-4o" -> "gpt-4o".
        if (-not [string]::IsNullOrWhiteSpace($fullId.Substring($prefix.Length))) {
            $openRouterModels.Add($item)
        }
    }
}

Write-Host "[$Provider] OpenRouter returned $($openRouterModels.Count) model(s) for prefix '$prefix'."

# For non-OpenRouter providers, deduplicate models that differ only by
# OpenRouter pricing-tier suffix (e.g. "model:free" vs "model").
# Keep the entry without a suffix when both exist (it carries richer pricing metadata).
if (-not $isOpenRouterProvider) {
    $dedup = [ordered]@{}
    foreach ($orm in $openRouterModels) {
        $name = Get-ModelName $orm.id
        $rawPart = $orm.id.Substring($prefix.Length)
        $hasSuffix = $rawPart.Contains(':')
        if ($dedup.Contains($name)) {
            # Replace only if current entry has no tier suffix (canonical)
            if (-not $hasSuffix) { $dedup[$name] = $orm }
        }
        else {
            $dedup[$name] = $orm
        }
    }
    $openRouterModels = [System.Collections.Generic.List[psobject]]@($dedup.Values)
    Write-Host "[$Provider] After tier-suffix deduplication: $($openRouterModels.Count) model(s)."
}

# Helper: Normalize model name for cross-reference matching.
# Anthropic uses hyphens in their API (claude-opus-4-7) while OpenRouter uses dots (claude-opus-4.7).
function Get-NormalizedModelName($modelName) {
    return $modelName -replace '\.', '-'
}

# Resolve the OpenRouter entry for a model id, trying (in order):
#   1. exact id
#   2. dot/hyphen normalized id
#   3. each provided alias (exact + normalized)
# OpenRouter typically ships the bare form (e.g. "gpt-4o-mini",
# "claude-haiku-4.5") while our canonical is the dated form
# ("gpt-4o-mini-2024-07-18", "claude-haiku-4-5-20251001"). Without this
# fallback the canonical loses its Created/OutputPrice metadata and falls
# to the bottom of the sort.
function Resolve-OpenRouterEntry($modelId, $aliases) {
    if ([string]::IsNullOrWhiteSpace($modelId)) { return $null }
    $candidates = [System.Collections.Generic.List[string]]::new()
    [void]$candidates.Add($modelId)
    [void]$candidates.Add((Get-NormalizedModelName $modelId))
    if ($aliases) {
        foreach ($a in $aliases) {
            if ([string]::IsNullOrWhiteSpace($a)) { continue }
            [void]$candidates.Add($a)
            [void]$candidates.Add((Get-NormalizedModelName $a))
        }
    }

    # Collect all distinct OpenRouter matches across the candidate set, then
    # pick the most recent (tiebreak: highest output price). This matters when
    # several aliases each map to a different OpenRouter entry — the canonical
    # should reflect the newest release, not whichever alias was checked first.
    $hits = [System.Collections.Generic.List[object]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($key in $candidates) {
        if (-not $openRouterLookup.ContainsKey($key)) { continue }
        $orm = $openRouterLookup[$key]
        $ormId = if ($orm.id) { [string]$orm.id } else { $key }
        if ($seen.Add($ormId)) { [void]$hits.Add($orm) }
    }

    if ($hits.Count -eq 0) { return $null }
    if ($hits.Count -eq 1) { return $hits[0] }

    return $hits | Sort-Object -Property `
        @{ Expression = { if ($_.created) { [long]$_.created } else { 0 } }; Descending = $true },
        @{ Expression = {
            $p = 0.0
            if ($_.pricing.completion)    { $p += [double]$_.pricing.completion }
            if ($_.pricing.image_output)  { $p += [double]$_.pricing.image_output }
            elseif ($_.pricing.image)     { $p += [double]$_.pricing.image }
            if ($_.pricing.audio_output)  { $p += [double]$_.pricing.audio_output }
            elseif ($_.pricing.audio)     { $p += [double]$_.pricing.audio }
            $p
        }; Descending = $true } |
        Select-Object -First 1
}

# Build quick lookup by stored model name (original + normalized for cross-reference matching)
$openRouterLookup = @{ }
foreach ($orm in $openRouterModels) {
    $name = Get-ModelName $orm.id
    $openRouterLookup[$name] = $orm
    # Also add normalized key so provider-API hyphenated ids match OpenRouter dotted ids
    $normalized = Get-NormalizedModelName $name
    if ($normalized -ne $name) {
        $openRouterLookup[$normalized] = $orm
    }
}

# ---------------------------------------------------------------------------
# 3. Merge OpenRouter data with existing source models
# ---------------------------------------------------------------------------
$mergedModels = [ordered]@{ }

# -- Seed with existing models (preserve properties we don't derive from OpenRouter)
foreach ($kvp in $existingModels.GetEnumerator()) {
    $mergedModels[$kvp.Key] = $kvp.Value
}

# ---------------------------------------------------------------------------
# 2b. Optional primary source: provider's own /models API
#
# When -ProviderApiKey is supplied, the provider's own API becomes the
# authoritative list of live models. OpenRouter is then only used to enrich
# brand-new models (capabilities, context limit, deprecation hint) that are
# not yet present in the source file. Models already in the source file are
# preserved as-is.
# ---------------------------------------------------------------------------
$providerApiModelNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
# Full provider API objects keyed by model id, for alias extraction.
$providerApiLookup = @{}
$providerApiQueried = $false

if (-not [string]::IsNullOrWhiteSpace($ProviderApiKey) -and $ProviderApis.ContainsKey($Provider)) {
    $api = $ProviderApis[$Provider]
    try {
        $providerHeaders = & $api.Headers $ProviderApiKey
        $providerResponse = Invoke-RestMethod -Uri $api.Url -Headers $providerHeaders -Method GET -TimeoutSec 60
        $providerApiQueried = $true

        # Most providers return { data: [{ id: ... }] }.
        # Gemini returns { models: [{ name: "models/<id>" }] } instead.
        if ($Provider -eq 'Gemini') {
            $providerData = if ($providerResponse.models) { $providerResponse.models } else { @() }
            foreach ($pm in $providerData) {
                $rawName = $pm.name
                if ([string]::IsNullOrWhiteSpace($rawName)) { continue }
                # Strip the "models/" prefix that Google's API returns.
                if ($rawName.StartsWith('models/', [System.StringComparison]::OrdinalIgnoreCase)) {
                    $rawName = $rawName.Substring('models/'.Length)
                }
                [void]$providerApiModelNames.Add($rawName)
                $providerApiLookup[$rawName] = $pm
            }
        }
        else {
            $providerData = if ($providerResponse.data) { $providerResponse.data } else { @() }
            foreach ($pm in $providerData) {
                if (-not [string]::IsNullOrWhiteSpace($pm.id) -and -not $pm.id.StartsWith('ft:')) {
                    [void]$providerApiModelNames.Add($pm.id)
                    $providerApiLookup[$pm.id] = $pm
                }
            }
        }
    }
    catch {
        Write-Warning "[$Provider] Provider API request failed: $($_.Exception.Message). Falling back to OpenRouter only."
    }
}

# ---------------------------------------------------------------------------
# Build provider-API alias + deprecation maps.
#
# Grouping strategy per provider:
#   MistralAI  - Group by the "name" field (authoritative canonical). Every
#                API entry whose .name == N belongs to the same logical model;
#                the canonical id is N itself. The per-entry "aliases" array
#                is merged in as a supplement (it is sometimes incomplete).
#                "deprecation" != null on any group member marks the canonical
#                as deprecated.
#   OpenAI/Anthropic - No "aliases" field in the API; instead, dated suffixes
#                in the id encode the alias relationship. Group by stripping
#                the suffix (-YYYY-MM-DD / -YYYYMMDD / -latest) to get a base
#                key. The dated id is canonical (immutable release); the bare
#                id and -latest variant become aliases. Driven by
#                $ProviderAliasSuffix table above.
#   Other       - Use the per-entry "aliases" array when present (DeepSeek
#                currently doesn't expose aliases, so the map is empty).
# ---------------------------------------------------------------------------
$apiAliasesByCanonical   = @{}
$apiCanonicalByAlias     = [System.Collections.Generic.Dictionary[string,string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$apiDeprecatedCanonicals = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

if ($providerApiQueried) {
    if ($Provider -eq 'MistralAI') {
        $groups = @{}
        foreach ($pmId in $providerApiModelNames) {
            $pm = $providerApiLookup[$pmId]
            $grpKey = if (-not [string]::IsNullOrWhiteSpace($pm.name)) { $pm.name } else { $pmId }
            if (-not $groups.ContainsKey($grpKey)) {
                $groups[$grpKey] = [System.Collections.Generic.List[string]]::new()
            }
            [void]$groups[$grpKey].Add($pmId)
        }

        foreach ($kvp in $groups.GetEnumerator()) {
            $canonical = $kvp.Key
            $aliasSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            $aliasList = [System.Collections.Generic.List[string]]::new()

            foreach ($id in $kvp.Value) {
                if (-not [string]::Equals($id, $canonical, 'OrdinalIgnoreCase') -and $aliasSet.Add($id)) {
                    [void]$aliasList.Add($id)
                }
                $pm = $providerApiLookup[$id]
                if ($pm.aliases) {
                    foreach ($a in $pm.aliases) {
                        if ([string]::IsNullOrWhiteSpace($a)) { continue }
                        if ([string]::Equals($a, $canonical, 'OrdinalIgnoreCase')) { continue }
                        if ($aliasSet.Add($a)) { [void]$aliasList.Add($a) }
                    }
                }
                if ($null -ne $pm.deprecation) {
                    [void]$apiDeprecatedCanonicals.Add($canonical)
                }
            }

            $apiAliasesByCanonical[$canonical] = $aliasList.ToArray()
        }
    }
    elseif ($ProviderAliasSuffix.ContainsKey($Provider) -and $ProviderAliasSuffix[$Provider]) {
        # Suffix-based grouping (OpenAI, Anthropic):
        #   baseKey   = id with -YYYY-MM-DD / -YYYYMMDD / -latest stripped
        #   canonical = dated variant (highest date wins on ties); else -latest;
        #               else the bare id (group has no dated release at all)
        $suffixRx = [regex]::new($ProviderAliasSuffix[$Provider])
        $dateRx   = [regex]::new('-(\d{8}|\d{4}-\d{2}-\d{2})$')
        $latestRx = [regex]::new('-latest$')

        $groups = @{}
        foreach ($pmId in $providerApiModelNames) {
            $baseKey = Get-LogicalModelKey ($suffixRx.Replace($pmId, ''))
            if (-not $groups.ContainsKey($baseKey)) {
                $groups[$baseKey] = [System.Collections.Generic.List[string]]::new()
            }
            [void]$groups[$baseKey].Add($pmId)
        }

        foreach ($kvp in $groups.GetEnumerator()) {
            $baseKey = $kvp.Key
            $ids     = @($kvp.Value)

            # Skip groups that contain only the bare baseKey itself: nothing
            # to alias (no dated/-latest variant present). Singleton groups
            # whose sole id is dated are NOT skipped, because the bare baseKey
            # is still an implicit server-side rolling alias we must record.
            if ($ids.Count -eq 1 -and [string]::Equals($ids[0], $baseKey, 'OrdinalIgnoreCase')) {
                continue
            }

            # Only the bare baseKey and -latest variant are rolling aliases.
            # Different dated releases are distinct immutable models and must
            # remain standalone (NOT folded into one group's alias list).
            #
            # Canonical = most recent dated id in the group; the rolling
            # alias slots (bare baseKey, -latest) attach to it.
            $dated = @($ids | Where-Object { $dateRx.IsMatch($_) })
            if ($dated.Count -gt 0) {
                # Compare on the captured date string. Both YYYYMMDD and
                # YYYY-MM-DD sort correctly lexicographically.
                $canonical = $dated | Sort-Object -Property @{
                    Expression = { $dateRx.Match($_).Groups[1].Value }
                } -Descending | Select-Object -First 1
            }
            else {
                $latest = @($ids | Where-Object { $latestRx.IsMatch($_) })
                if ($latest.Count -gt 0) { $canonical = $latest[0] }
                else                     { $canonical = $baseKey }
            }

            # Aliases: only the bare baseKey and the -latest variant, when
            # present in the group (or implicit for the bare baseKey).
            $aliasList = [System.Collections.Generic.List[string]]::new()
            foreach ($id in $ids) {
                if ([string]::Equals($id, $canonical, 'OrdinalIgnoreCase')) { continue }
                $isBare   = [string]::Equals($id, $baseKey, 'OrdinalIgnoreCase')
                $isLatest = $latestRx.IsMatch($id)
                if ($isBare -or $isLatest) { [void]$aliasList.Add($id) }
            }

            # Always include bare baseKey and baseKey-latest as aliases on the canonical
            # dated id, regardless of whether the API listed them as separate entries.
            foreach ($implied in @($baseKey, "$baseKey-latest")) {
                if (-not [string]::Equals($implied, $canonical, 'OrdinalIgnoreCase') -and
                    -not ($aliasList | Where-Object { [string]::Equals($_, $implied, 'OrdinalIgnoreCase') })) {
                    [void]$aliasList.Add($implied)
                }
            }

            if ($aliasList.Count -gt 0) {
                $apiAliasesByCanonical[$canonical] = $aliasList.ToArray()
            }
        }
    }
    else {
        foreach ($pmId in $providerApiModelNames) {
            $pm = $providerApiLookup[$pmId]
            if ($pm.aliases -and $pm.aliases.Count -gt 0) {
                $apiAliasesByCanonical[$pmId] = @($pm.aliases | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            }
        }
    }

    foreach ($srcKey in @($existingModels.Keys)) {
        $logicalSourceKey = Get-LogicalModelKey $srcKey
        $canonicalVersionPeer = $providerApiModelNames | Where-Object {
            -not [string]::Equals($_, $srcKey, 'OrdinalIgnoreCase') -and
            [string]::Equals((Get-LogicalModelKey $_), $logicalSourceKey, 'OrdinalIgnoreCase')
        } | Select-Object -First 1

        if (-not $canonicalVersionPeer) { continue }

        $apiCanonicalByAlias[$srcKey] = $canonicalVersionPeer
        if (-not $apiAliasesByCanonical.ContainsKey($canonicalVersionPeer)) {
            $apiAliasesByCanonical[$canonicalVersionPeer] = @()
        }

        $versionAliases = [System.Collections.Generic.List[string]]::new()
        foreach ($a in @($apiAliasesByCanonical[$canonicalVersionPeer])) { [void]$versionAliases.Add($a) }
        if (-not ($versionAliases | Where-Object { [string]::Equals($_, $srcKey, 'OrdinalIgnoreCase') })) {
            [void]$versionAliases.Add($srcKey)
        }
        $apiAliasesByCanonical[$canonicalVersionPeer] = $versionAliases.ToArray()
    }

    # Reverse map: alias -> canonical.
    foreach ($kvp in $apiAliasesByCanonical.GetEnumerator()) {
        foreach ($a in $kvp.Value) {
            if ([string]::Equals($a, $kvp.Key, 'OrdinalIgnoreCase')) { continue }
            if (-not $apiCanonicalByAlias.ContainsKey($a)) {
                $apiCanonicalByAlias[$a] = $kvp.Key
            }
        }
    }

    # Supplement: source-file -latest models that the provider API no longer lists
    # as standalone entries but belong to a dated canonical family.
    # E.g. "claude-3-5-haiku-latest" is in the source but not in the API response;
    # we still want to fold it into "claude-3-5-haiku-20241022".
    if ($ProviderAliasSuffix.ContainsKey($Provider) -and $ProviderAliasSuffix[$Provider]) {
        $suffixRxSupplement = [regex]::new($ProviderAliasSuffix[$Provider])
        $latestRxSupplement = [regex]::new('-latest$')
        $dateRxSupplement   = [regex]::new('-(\d{8}|\d{4}-\d{2}-\d{2})$')

        foreach ($srcKey in @($existingModels.Keys)) {
            if (-not $latestRxSupplement.IsMatch($srcKey)) { continue }
            if ($apiCanonicalByAlias.ContainsKey($srcKey)) { continue }

            $baseKey = $suffixRxSupplement.Replace($srcKey, '')
            $logicalBaseKey = Get-LogicalModelKey $baseKey

            # Find all dated members of the same family in the provider API
            $datedSiblings = @($providerApiModelNames | Where-Object {
                $dateRxSupplement.IsMatch($_) -and
                [string]::Equals((Get-LogicalModelKey ($suffixRxSupplement.Replace($_, ''))), $logicalBaseKey, 'OrdinalIgnoreCase')
            })

            if ($datedSiblings.Count -eq 0) { continue }

            # Pick the most recent dated sibling as canonical
            $datedCanonical = $datedSiblings | Sort-Object -Property @{
                Expression = { $dateRxSupplement.Match($_).Groups[1].Value }
            } -Descending | Select-Object -First 1

            # Skip if the -latest id is itself the canonical (shouldn't happen, but guard)
            if ([string]::Equals($srcKey, $datedCanonical, 'OrdinalIgnoreCase')) { continue }

            $apiCanonicalByAlias[$srcKey] = $datedCanonical

            # Register the bare baseKey too if not already mapped
            if (-not $apiCanonicalByAlias.ContainsKey($baseKey) -and
                -not [string]::Equals($baseKey, $datedCanonical, 'OrdinalIgnoreCase')) {
                $apiCanonicalByAlias[$baseKey] = $datedCanonical
            }

            # Ensure the dated canonical has the -latest (and bare) in its alias list
            if (-not $apiAliasesByCanonical.ContainsKey($datedCanonical)) {
                $apiAliasesByCanonical[$datedCanonical] = @()
            }
            $existingAliases = [System.Collections.Generic.List[string]]::new()
            foreach ($a in @($apiAliasesByCanonical[$datedCanonical])) { [void]$existingAliases.Add($a) }
            if (-not ($existingAliases | Where-Object { [string]::Equals($_, $srcKey, 'OrdinalIgnoreCase') })) {
                [void]$existingAliases.Add($srcKey)
            }
            if (-not ($existingAliases | Where-Object { [string]::Equals($_, $baseKey, 'OrdinalIgnoreCase') }) -and
                -not [string]::Equals($baseKey, $datedCanonical, 'OrdinalIgnoreCase')) {
                [void]$existingAliases.Add($baseKey)
            }
            $apiAliasesByCanonical[$datedCanonical] = $existingAliases.ToArray()
        }
    }
}

# ---------------------------------------------------------------------------
# Helper: Merge API-derived pricing into an existing pricing object per-field.
# Preserves any hand-curated fields that the API did not publish; overwrites
# only the fields the API did publish. Returns a new pscustomobject.
# ---------------------------------------------------------------------------
function Merge-Pricing($existing, $incoming) {
    if (-not $existing) { return $incoming }
    if (-not $incoming) { return $existing }

    $merged = [ordered]@{}
    $fields = @('Prompt','Completion','Request','Image','ImageOutput','ImageToken','Audio','AudioOutput','InputAudioCache','InputCacheRead','InputCacheWrite','InternalReasoning','WebSearch','Discount')
    foreach ($f in $fields) {
        $iv = $incoming.$f
        $ev = $existing.$f
        if ($null -ne $iv -and -not [string]::IsNullOrWhiteSpace([string]$iv)) {
            $merged[$f] = $iv
        }
        elseif ($null -ne $ev -and -not [string]::IsNullOrWhiteSpace([string]$ev)) {
            $merged[$f] = $ev
        }
    }
    if ($merged.Count -eq 0) { return $null }
    return [pscustomobject]$merged
}

# ---------------------------------------------------------------------------
# Helper: Extract enrichment data from an OpenRouter model entry
# ---------------------------------------------------------------------------
function Get-OpenRouterEnrichment($orm) {
    if (-not $orm) { return $null }

    $deprecated = $false
    if ($orm.expiration_date) {
        try {
            $exp = [DateTime]::Parse($orm.expiration_date.ToString())
            if ($exp -lt $OneYearFromNow) { $deprecated = $true }
        } catch { }
    }

    $ctx = $orm.context_length
    if (-not $ctx -and $orm.top_provider) { $ctx = $orm.top_provider.context_length }

    # Created: Unix epoch seconds
    $created = $null
    if ($orm.created) {
        try { $created = [DateTimeOffset]::FromUnixTimeSeconds([long]$orm.created).UtcDateTime } catch { }
    }

    # Pricing: map OpenRouter snake_case fields to our PascalCase properties.
    $pricing = $null
    if ($orm.pricing) {
        $map = @{
            prompt              = 'Prompt'
            completion          = 'Completion'
            request             = 'Request'
            image               = 'Image'
            image_output        = 'ImageOutput'
            image_token         = 'ImageToken'
            audio               = 'Audio'
            audio_output        = 'AudioOutput'
            input_audio_cache   = 'InputAudioCache'
            input_cache_read    = 'InputCacheRead'
            input_cache_write   = 'InputCacheWrite'
            internal_reasoning  = 'InternalReasoning'
            web_search          = 'WebSearch'
            discount            = 'Discount'
        }
        $priceObj = [ordered]@{}
        foreach ($kvp in $map.GetEnumerator()) {
            $raw = $orm.pricing.$($kvp.Key)
            if ($null -eq $raw) { continue }
            $s = [string]$raw
            if ([string]::IsNullOrWhiteSpace($s)) { continue }
            # Skip zero to keep emitted blocks small (field is optional).
            try { if ([decimal]$s -eq [decimal]0) { continue } } catch { continue }
            $priceObj[$kvp.Value] = $s
        }
        if ($priceObj.Count -gt 0) { $pricing = [pscustomobject]$priceObj }
    }

    return [pscustomobject]@{
        Capabilities = ConvertTo-CapabilityFlags -openRouterModel $orm
        ContextLimit = if ($null -ne $ctx) { $ctx.ToString() } else { $null }
        Deprecated   = $deprecated
        Created      = $created
        Pricing      = $pricing
    }
}

# ---------------------------------------------------------------------------
# Collapse existing source entries keyed by an alias into their canonical id
# (built in $apiCanonicalByAlias above). Ensures each logical model has a
# single entry after the seed.
# ---------------------------------------------------------------------------
if ($providerApiQueried) {
    if ($apiCanonicalByAlias.Count -gt 0) {
        $rekeyed = [ordered]@{}
        foreach ($kvp in $mergedModels.GetEnumerator()) {
            $key = $kvp.Key
            $val = $kvp.Value
            $canonical = if ($apiCanonicalByAlias.ContainsKey($key)) { $apiCanonicalByAlias[$key] } else { $key }

            if ($rekeyed.Contains($canonical)) {
                $existing = $rekeyed[$canonical]
                $aliases = [System.Collections.Generic.List[string]]::new()
                if ($existing.Aliases) { foreach ($a in @($existing.Aliases)) { [void]$aliases.Add($a) } }

                if ([string]::Equals($key, $canonical, 'OrdinalIgnoreCase')) {
                    # Canonical entry wins: replace data, absorb aliased entry's
                    # old key + aliases into the canonical's Aliases list.
                    if ($val.Aliases) {
                        foreach ($a in @($val.Aliases)) { if (-not $aliases.Contains($a)) { [void]$aliases.Add($a) } }
                    }
                    if (-not [string]::Equals($existing.Model, $canonical, 'OrdinalIgnoreCase') -and -not $aliases.Contains($existing.Model)) {
                        [void]$aliases.Add($existing.Model)
                    }
                    $val.Model = $canonical
                    $val.Aliases = if ($aliases.Count -gt 0) { $aliases.ToArray() } else { $null }
                    $rekeyed[$canonical] = $val
                }
                else {
                    # $val is aliased-in: keep existing canonical data, just
                    # record this key (+ its aliases) under the canonical's Aliases.
                    if (-not $aliases.Contains($key)) { [void]$aliases.Add($key) }
                    if ($val.Aliases) {
                        foreach ($a in @($val.Aliases)) {
                            if (-not $aliases.Contains($a) -and -not [string]::Equals($a, $canonical, 'OrdinalIgnoreCase')) {
                                [void]$aliases.Add($a)
                            }
                        }
                    }
                    $existing.Aliases = $aliases.ToArray()
                }
            }
            else {
                if (-not [string]::Equals($key, $canonical, 'OrdinalIgnoreCase')) {
                    # Rekey: preserve old key as alias, update Model field.
                    $aliases = [System.Collections.Generic.List[string]]::new()
                    if ($val.Aliases) {
                        foreach ($a in @($val.Aliases)) {
                            if (-not [string]::Equals($a, $canonical, 'OrdinalIgnoreCase')) { [void]$aliases.Add($a) }
                        }
                    }
                    if (-not $aliases.Contains($key)) { [void]$aliases.Add($key) }
                    $val.Aliases = $aliases.ToArray()
                    $val.Model = $canonical
                }
                $rekeyed[$canonical] = $val
            }
        }
        $mergedModels = $rekeyed
    }
}

# ---------------------------------------------------------------------------
# 3. Merge logic
# ---------------------------------------------------------------------------
if ($providerApiQueried) {
    # ---- Provider API is the source of truth ----
    # Existing source models are preserved verbatim; only their Deprecated flag
    # is toggled based on whether the provider API still lists them.
    # Brand-new provider-API models are seeded with defaults and (when matched)
    # enriched from OpenRouter.
    foreach ($pmId in $providerApiModelNames) {
        # Skip API ids that are aliases of another canonical (avoids duplicate entries).
        if ($apiCanonicalByAlias.ContainsKey($pmId)) { continue }

        $apiAliases = if ($apiAliasesByCanonical.ContainsKey($pmId)) { @($apiAliasesByCanonical[$pmId]) } else { @() }
        $isApiDeprecated = $apiDeprecatedCanonicals.Contains($pmId)

        if ($mergedModels.Contains($pmId)) {
            # Refresh Created/Pricing from OpenRouter when available (always
        # re-derived, since these are published metadata, not hand-curated).
        $ormRefresh = Resolve-OpenRouterEntry $pmId $apiAliases
        $enrichRefresh = Get-OpenRouterEnrichment -orm $ormRefresh
        if ($enrichRefresh) {
            if ($enrichRefresh.Created) { $mergedModels[$pmId] | Add-Member -NotePropertyName 'Created' -NotePropertyValue $enrichRefresh.Created -Force }
            if ($enrichRefresh.Pricing) {
                $existingPricing = $mergedModels[$pmId].Pricing
                $mergedPricing = Merge-Pricing $existingPricing $enrichRefresh.Pricing
                $mergedModels[$pmId] | Add-Member -NotePropertyName 'Pricing' -NotePropertyValue $mergedPricing -Force
            }
        }

        # Preserve all hand-curated data but update aliases from provider API
            # (additive merge: keep existing aliases, add any new ones from the API)
            # and propagate provider deprecation flag.
            $existing = $mergedModels[$pmId]
            $currentAliases = [System.Collections.Generic.List[string]]::new()
            if ($existing.Aliases) {
                foreach ($a in @($existing.Aliases)) { [void]$currentAliases.Add($a) }
            }
            foreach ($a in $apiAliases) {
                if (-not $currentAliases.Contains($a)) { [void]$currentAliases.Add($a) }
            }
            # Drop stale aliases that are themselves provider-API canonicals
            # (i.e. listed as a top-level model and NOT registered as our alias).
            # This corrects historical entries where a different dated release
            # was previously folded in as an alias.
            $cleaned = [System.Collections.Generic.List[string]]::new()
            foreach ($a in $currentAliases) {
                $isOtherCanonical = ($providerApiModelNames -contains $a) -and (
                    -not $apiCanonicalByAlias.ContainsKey($a) -or
                    -not [string]::Equals($apiCanonicalByAlias[$a], $pmId, 'OrdinalIgnoreCase')
                )
                if (-not $isOtherCanonical) { [void]$cleaned.Add($a) }
            }
            $existing.Aliases = if ($cleaned.Count -gt 0) { $cleaned.ToArray() } else { $null }
            if ($isApiDeprecated) { $existing.Deprecated = 'true' }
            continue
        }

        $ormLookup = Resolve-OpenRouterEntry $pmId $apiAliases
        $enrichment = Get-OpenRouterEnrichment -orm $ormLookup

        $caps = if ($enrichment -and -not [string]::IsNullOrWhiteSpace($enrichment.Capabilities) -and $enrichment.Capabilities -ne 'AICapability.None') {
            $enrichment.Capabilities
        } else {
            'AICapability.None'
        }

        $deprecated = if ($isApiDeprecated -or ($enrichment -and $enrichment.Deprecated)) { 'true' } else { $null }
        $ctx        = if ($enrichment) { $enrichment.ContextLimit } else { $null }

        $mergedModels[$pmId] = [pscustomobject][ordered]@{
            Provider              = $null   # filled by Format-ModelBlock
            Model                 = $pmId
            Capabilities          = $caps
            Verified              = 'false'
            Deprecated            = $deprecated
            SupportsStreaming     = 'true'
            SupportsPromptCaching = $null
            Rank                  = '50'
            ContextLimit          = $ctx
            Created               = if ($enrichment) { $enrichment.Created } else { $null }
            Pricing               = if ($enrichment) { $enrichment.Pricing } else { $null }
            Aliases               = if ($apiAliases.Count -gt 0) { $apiAliases } else { $null }
            DiscouragedForTools   = $null
            CacheKeyStrategy      = $null
        }
    }
}
else {
    # ---- OpenRouter-only mode (legacy behaviour) ----
    # OpenRouter is the source of truth: capabilities and context limits are
    # refreshed for every model and brand-new entries are added.
    foreach ($orm in $openRouterModels) {
        $modelName  = Get-ModelName $orm.id
        $enrichment = Get-OpenRouterEnrichment -orm $orm

        if ($mergedModels.Contains($modelName)) {
            $existing = $mergedModels[$modelName]
            # Only overwrite Capabilities when OpenRouter returned a real value.
            # Preserve hand-curated flags when the API reports None.
            if (-not [string]::IsNullOrWhiteSpace($enrichment.Capabilities) -and $enrichment.Capabilities -ne 'AICapability.None') {
                $existing.Capabilities = $enrichment.Capabilities
            }
            if ($null -ne $enrichment.ContextLimit) { $existing.ContextLimit = $enrichment.ContextLimit }
            if ($enrichment.Deprecated -or $existing.Deprecated -eq 'true') { $existing.Deprecated = 'true' }
            if ($enrichment.Created) { $existing | Add-Member -NotePropertyName 'Created' -NotePropertyValue $enrichment.Created -Force }
            if ($enrichment.Pricing) {
                $mergedPricing = Merge-Pricing $existing.Pricing $enrichment.Pricing
                $existing | Add-Member -NotePropertyName 'Pricing' -NotePropertyValue $mergedPricing -Force
            }
        }
        else {
            $mergedModels[$modelName] = [pscustomobject][ordered]@{
                Provider              = $null
                Model                 = $modelName
                Capabilities          = $enrichment.Capabilities
                Verified              = 'false'
                Deprecated            = if ($enrichment.Deprecated) { 'true' } else { $null }
                SupportsStreaming     = $null
                SupportsPromptCaching = $null
                Rank                  = '50'
                ContextLimit          = $enrichment.ContextLimit
                Created               = $enrichment.Created
                Pricing               = $enrichment.Pricing
                Aliases               = $null
                DiscouragedForTools   = $null
                CacheKeyStrategy      = $null
            }
        }
    }
}

# ---------------------------------------------------------------------------
# Normalize Aliases: remove self-references and duplicates (case-insensitive).
# ---------------------------------------------------------------------------
foreach ($m in $mergedModels.Values) {
    if (-not $m.Aliases) { continue }
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $clean = [System.Collections.Generic.List[string]]::new()
    foreach ($a in @($m.Aliases)) {
        if ([string]::IsNullOrWhiteSpace($a)) { continue }
        if ([string]::Equals($a, $m.Model, 'OrdinalIgnoreCase')) { continue }
        if ($seen.Add($a)) { [void]$clean.Add($a) }
    }
    $m.Aliases = if ($clean.Count -gt 0) { $clean.ToArray() } else { $null }
}

# ---------------------------------------------------------------------------
# Sibling capability inheritance.
#
# Different dated releases that share a baseKey (e.g. gpt-4o-mini-transcribe-
# 2025-03-20 vs gpt-4o-mini-transcribe-2025-12-15) are distinct models, but
# OpenRouter only documents the bare alias. Older/peripheral dated entries
# therefore land with Capabilities = AICapability.None. Inherit caps and
# context from a sibling that has real data.
# ---------------------------------------------------------------------------
if ($ProviderAliasSuffix.ContainsKey($Provider) -and $ProviderAliasSuffix[$Provider]) {
    $suffixRxInherit = [regex]::new($ProviderAliasSuffix[$Provider])

    $byBase = @{}
    foreach ($m in $mergedModels.Values) {
        $bk = $suffixRxInherit.Replace($m.Model, '')
        if (-not $byBase.ContainsKey($bk)) {
            $byBase[$bk] = [System.Collections.Generic.List[object]]::new()
        }
        [void]$byBase[$bk].Add($m)
    }

    foreach ($kvp in $byBase.GetEnumerator()) {
        $siblings = $kvp.Value
        if ($siblings.Count -le 1) { continue }

        # Donor: any sibling with real (non-None/non-empty) capabilities.
        $donor = $siblings | Where-Object {
            -not [string]::IsNullOrWhiteSpace($_.Capabilities) -and
            $_.Capabilities -ne 'AICapability.None'
        } | Select-Object -First 1
        if (-not $donor) { continue }

        foreach ($m in $siblings) {
            if ([System.Object]::ReferenceEquals($m, $donor)) { continue }
            if ([string]::IsNullOrWhiteSpace($m.Capabilities) -or $m.Capabilities -eq 'AICapability.None') {
                $m.Capabilities = $donor.Capabilities
            }
            if ($null -eq $m.ContextLimit -and $null -ne $donor.ContextLimit) {
                $m.ContextLimit = $donor.ContextLimit
            }
            if ([string]::IsNullOrWhiteSpace($m.SupportsStreaming) -and -not [string]::IsNullOrWhiteSpace($donor.SupportsStreaming)) {
                $m.SupportsStreaming = $donor.SupportsStreaming
            }
        }
    }
}

# ---------------------------------------------------------------------------
# Mark models that no longer appear in the authoritative source as deprecated.
# Authoritative = provider API when queried, otherwise OpenRouter.
#
# For OpenRouter provider specifically:
# - OpenRouter API is both source of truth AND authoritative source.
# - Models present in OpenRouterProviderModels.cs but missing from current OpenRouter catalogue are marked Deprecated = true
# - This ensures OpenRouter models are deprecated when removed from OpenRouter's available models list
# ---------------------------------------------------------------------------
if ($providerApiQueried) {
    $apiModelNames = $providerApiModelNames
}
else {
    $apiModelNames = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]($openRouterModels | ForEach-Object { Get-ModelName $_.id }),
        [System.StringComparer]::OrdinalIgnoreCase)
}

foreach ($kvp in $mergedModels.GetEnumerator()) {
    if (-not $apiModelNames.Contains($kvp.Key)) {
        $kvp.Value.Deprecated = 'true'
    }
}

foreach ($key in @($mergedModels.Keys)) {
    if (Test-RealtimeModelName $key) {
        [void]$mergedModels.Remove($key)
    }
}

# ---------------------------------------------------------------------------
# Reassign generic alias (bare baseKey) to latest non-deprecated family member
# ---------------------------------------------------------------------------
if ($ProviderAliasSuffix.ContainsKey($Provider) -and $ProviderAliasSuffix[$Provider]) {
    $suffixRxAlias = [regex]::new($ProviderAliasSuffix[$Provider])
    $byBaseAlias = @{}
    foreach ($m in $mergedModels.Values) {
        $bk = $suffixRxAlias.Replace($m.Model, '')
        if (-not $byBaseAlias.ContainsKey($bk)) {
            $byBaseAlias[$bk] = [System.Collections.Generic.List[object]]::new()
        }
        [void]$byBaseAlias[$bk].Add($m)
    }

    foreach ($kvp in $byBaseAlias.GetEnumerator()) {
        $siblings = @($kvp.Value)
        $baseKey = $kvp.Key

        # Skip if the only member is the bare baseKey itself (no dated variant present)
        if ($siblings.Count -eq 1 -and [string]::Equals($siblings[0].Model, $baseKey, 'OrdinalIgnoreCase')) { continue }

        # Latest non-deprecated: sort by model-ID date string first (most reliable),
        # then by OpenRouter epoch as tiebreaker (Created not yet computed at this stage).
        $dateRxSort  = [regex]::new('-(\d{8}|\d{4}-\d{2}-\d{2})$')
        $getDateKey  = { param($m)
            $dm = $dateRxSort.Match($m.Model)
            if ($dm.Success) { $dm.Groups[1].Value } else { '' }
        }
        $getCreatedEpoch = { param($m)
            $orm = Resolve-OpenRouterEntry $m.Model $m.Aliases
            if ($orm -and $orm.created) { [long]$orm.created } else { [long]0 }
        }

        $candidates = $siblings |
            Where-Object { $_.Deprecated -ne 'true' } |
            Sort-Object -Property @(
                @{ Expression = { & $getDateKey $_ };  Descending = $true }
                @{ Expression = { & $getCreatedEpoch $_ }; Descending = $true }
            )

        # Winner = most recent non-deprecated; fallback = most recent deprecated
        $winner = if ($candidates) {
            $candidates | Select-Object -First 1
        } else {
            $siblings | Sort-Object -Property @(
                @{ Expression = { & $getDateKey $_ };  Descending = $true }
                @{ Expression = { & $getCreatedEpoch $_ }; Descending = $true }
            ) | Select-Object -First 1
        }

        # Implied rolling aliases for this family
        $impliedAliases = @($baseKey, "$baseKey-latest")

        foreach ($m in $siblings) {
            $current = [System.Collections.Generic.List[string]]::new()
            if ($m.Aliases) { foreach ($a in @($m.Aliases)) { [void]$current.Add($a) } }

            foreach ($implied in $impliedAliases) {
                # Never add an alias that is identical to the model name itself
                if ([string]::Equals($implied, $m.Model, 'OrdinalIgnoreCase')) { continue }

                $hasImplied = $current | Where-Object { [string]::Equals($_, $implied, 'OrdinalIgnoreCase') }
                if ([System.Object]::ReferenceEquals($m, $winner)) {
                    if (-not $hasImplied) { [void]$current.Add($implied) }
                } else {
                    if ($hasImplied) {
                        $current = [System.Collections.Generic.List[string]](
                            $current | Where-Object { -not [string]::Equals($_, $implied, 'OrdinalIgnoreCase') }
                        )
                    }
                }
            }

            $m.Aliases = if ($current.Count -gt 0) { $current.ToArray() } else { $null }
        }
    }
}

# ---------------------------------------------------------------------------
# Compute sorting keys (Created, OutputPrice) for every merged model
# ---------------------------------------------------------------------------
foreach ($m in $mergedModels.Values) {
    $orm = Resolve-OpenRouterEntry $m.Model $m.Aliases
    if ($orm) {
        # Created: OpenRouter returns Unix epoch seconds
        if ($orm.created) {
            $createdDt = [DateTimeOffset]::FromUnixTimeSeconds([long]$orm.created).DateTime
            $m | Add-Member -NotePropertyName 'Created' -NotePropertyValue $createdDt -Force
        }
        else {
            $m | Add-Member -NotePropertyName 'Created' -NotePropertyValue ([DateTime]::MinValue) -Force
        }

        # Output pricing: sum of applicable output prices (completion, image, audio_output)
        $prices = [System.Collections.Generic.List[decimal]]::new()
        if ($orm.pricing.completion) { $prices.Add([decimal]$orm.pricing.completion) }

        $imgPrice = if ($null -ne $orm.pricing.image_output) { $orm.pricing.image_output }
                    elseif ($null -ne $orm.pricing.image) { $orm.pricing.image }
                    else { $null }
        if ($null -ne $imgPrice) { $prices.Add([decimal]$imgPrice) }

        $audioPrice = if ($null -ne $orm.pricing.audio_output) { $orm.pricing.audio_output }
                      elseif ($null -ne $orm.pricing.audio) { $orm.pricing.audio }
                      else { $null }
        if ($null -ne $audioPrice) { $prices.Add([decimal]$audioPrice) }

        $sumPrice = if ($prices.Count -gt 0) { ($prices | Measure-Object -Sum).Sum } else { [decimal]::MaxValue }
        $m | Add-Member -NotePropertyName 'OutputPrice' -NotePropertyValue $sumPrice -Force
    }
    else {
        # No OpenRouter data → push to bottom of sort
        $m | Add-Member -NotePropertyName 'Created' -NotePropertyValue ([DateTime]::MinValue) -Force
        $m | Add-Member -NotePropertyName 'OutputPrice' -NotePropertyValue ([decimal]::MaxValue) -Force
    }
}

# ---------------------------------------------------------------------------
# 4. Diff for reporting
# ---------------------------------------------------------------------------
$allModelNames = $mergedModels.Keys | Sort-Object
$apiModelNamesList = $apiModelNames | Sort-Object
$sourceModelNamesList = $existingModels.Keys | Sort-Object

# A model is considered already known when its canonical id OR any of its
# aliases was present in the source file before this run. Comparing against
# the raw provider-API id list would mis-flag alias ids (e.g. the rolling
# "gpt-5-pro" alias of "gpt-5-pro-2025-10-06") as brand-new models, because
# aliases are folded into a canonical entry's Aliases list rather than added
# as standalone entries. Diffing on canonical entries keeps the report in
# sync with what is actually written to the source file.
$sourceModelSet = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]$sourceModelNamesList,
    [System.StringComparer]::OrdinalIgnoreCase)

function Test-KnownInSource($model) {
    if ($sourceModelSet.Contains([string]$model.Model)) { return $true }
    if ($model.Aliases) {
        foreach ($alias in @($model.Aliases)) {
            if (-not [string]::IsNullOrWhiteSpace($alias) -and $sourceModelSet.Contains([string]$alias)) {
                return $true
            }
        }
    }
    return $false
}

$newModels        = @($mergedModels.Values | Where-Object { -not (Test-KnownInSource $_) } | ForEach-Object { $_.Model } | Sort-Object)
$deprecatedModels = $allModelNames     | Where-Object { $mergedModels[$_].Deprecated -eq 'true' -and ($_ -in $sourceModelNamesList) }
$unchangedModels  = @($mergedModels.Values | Where-Object { (Test-KnownInSource $_) -and $_.Deprecated -ne 'true' } | ForEach-Object { $_.Model } | Sort-Object)

$validation = Test-ProviderModelValidation @($mergedModels.Values)

# ---------------------------------------------------------------------------
# 5. Optional file update
# ---------------------------------------------------------------------------
$fileUpdated = $false
if ($UpdateFile) {
    # Compute term index (Q1=most recent 3 months, Q2=3-6 months, ..., Q8=18-24 months).
    $now = Get-Date
    $termStart = for ($i = 0; $i -le 8; $i++) { $now.AddMonths(-3 * $i) }

    function Get-TermIndex($created) {
        if (-not $created -or $created -eq [DateTime]::MinValue) { return 99 }
        for ($i = 1; $i -le 8; $i++) {
            if ($created -ge $termStart[$i]) { return $i }
        }
        return 99
    }

    foreach ($m in $mergedModels.Values) {
        $m | Add-Member -NotePropertyName 'TermIndex' -NotePropertyValue (Get-TermIndex $m.Created) -Force
    }

    # Sort by: deprecated first, then term (most recent first), then verified first, then output price (cheapest first), then created date (newest first), then name
    $sorted = $mergedModels.Values | Sort-Object -Property @(
        @{ Expression = { if ($_.Deprecated -eq 'true') { 1 } else { 0 } }; Ascending = $true }
        @{ Expression = { $_.TermIndex }; Ascending = $true }
        @{ Expression = { if ($_.Verified -eq 'true') { 0 } else { 1 } }; Ascending = $true }
        @{ Expression = { $_.OutputPrice }; Ascending = $true }
        @{ Expression = { $_.Created }; Descending = $true }
        @{ Expression = { $_.Model }; Ascending = $true }
    )

    # Assign ranks: non-deprecated models get descending ranks starting at 10000, in steps of 5
    $nonDeprecated = $sorted | Where-Object { $_.Deprecated -ne 'true' }
    for ($i = 0; $i -lt $nonDeprecated.Count; $i++) {
        $nonDeprecated[$i].Rank = (10000 - ($i * 5)).ToString()
    }

    # Deprecated models get low ranks starting at 0, in steps of 5
    $deprecated = $sorted | Where-Object { $_.Deprecated -eq 'true' }
    for ($i = 0; $i -lt $deprecated.Count; $i++) {
        $deprecated[$i].Rank = (0 - ($i * 5)).ToString()
    }

    function Get-SectionComment($model) {
        if ($model.Deprecated -eq 'true') { return '// Deprecated models' }

        $ci  = [System.Globalization.CultureInfo]::GetCultureInfo('en-US')
        $fmt = 'MMMM yyyy'
        switch ($model.TermIndex) {
            1       { return "// Released between $($now.AddMonths(-3).ToString($fmt, $ci)) and $($now.ToString($fmt, $ci))" }
            2       { return "// Released between $($now.AddMonths(-6).ToString($fmt, $ci)) and $($now.AddMonths(-3).ToString($fmt, $ci))" }
            3       { return "// Released between $($now.AddMonths(-9).ToString($fmt, $ci)) and $($now.AddMonths(-6).ToString($fmt, $ci))" }
            4       { return "// Released between $($now.AddMonths(-12).ToString($fmt, $ci)) and $($now.AddMonths(-9).ToString($fmt, $ci))" }
            5       { return "// Released between $($now.AddMonths(-15).ToString($fmt, $ci)) and $($now.AddMonths(-12).ToString($fmt, $ci))" }
            6       { return "// Released between $($now.AddMonths(-18).ToString($fmt, $ci)) and $($now.AddMonths(-15).ToString($fmt, $ci))" }
            7       { return "// Released between $($now.AddMonths(-21).ToString($fmt, $ci)) and $($now.AddMonths(-18).ToString($fmt, $ci))" }
            8       { return "// Released between $($now.AddMonths(-24).ToString($fmt, $ci)) and $($now.AddMonths(-21).ToString($fmt, $ci))" }
            default { return "// Released before $($now.AddMonths(-24).ToString($fmt, $ci)) or unknown release date" }
        }
    }

    $newBlocks = [System.Collections.Generic.List[string]]::new()
    $currentSection = $null
    foreach ($m in $sorted) {
        $section = Get-SectionComment $m
        if ($section -ne $currentSection) {
            if ($newBlocks.Count -gt 0) {
                $newBlocks.Add('')
            }
            $newBlocks.Add("                $section")
            $currentSection = $section
        }
        $newBlocks.Add((Format-ModelBlock -model $m -providerVar $providerVar))
    }

    # Remove the trailing comma from the last block so the list initializer ends
    # with "};" instead of "},\r\n};" – avoids brace-scanner mismatches on re-runs.
    if ($newBlocks.Count -gt 0) {
        $last = $newBlocks[$newBlocks.Count - 1]
        $newBlocks[$newBlocks.Count - 1] = $last -replace ',\s*$', ''
    }

    $newListContent = ($newBlocks -join "`r`n`r`n")
    $newFileContent = $beforeList + $newListContent + "`r`n            };" + $afterList

    [System.IO.File]::WriteAllText($TargetFile, $newFileContent, $utf8NoBom)
    $fileUpdated = $true
    Write-Host "[$Provider] Wrote $($sorted.Count) model(s) to $TargetFile."
}

# ---------------------------------------------------------------------------
# 6. JSON report
# ---------------------------------------------------------------------------
$openRouterModelNamesList  = @($openRouterModels | ForEach-Object { Get-ModelName $_.id } | Sort-Object)
$providerApiModelNamesList = @($providerApiModelNames | Sort-Object)

$report = [ordered]@{
    provider           = $Provider
    apiUrl             = $OpenRouterUrl
    providerApiQueried = $providerApiQueried
    providerApiUrl     = if ($providerApiQueried) { $ProviderApis[$Provider].Url } else { $null }
    apiModels          = @($apiModelNamesList)
    openrouterModels   = $openRouterModelNamesList
    providerApiModels  = if ($providerApiQueried) { $providerApiModelNamesList } else { $null }
    sourceModels       = @($sourceModelNamesList)
    newModels          = @($newModels)
    deprecatedModels   = @($deprecatedModels)
    unchangedModels    = @($unchangedModels)
    fileUpdated        = $fileUpdated
    validation          = $validation
}

Write-Output ($report | ConvertTo-Json -Depth 10)

if ($validation.warnings.Count -gt 0) {
    foreach ($validationWarning in $validation.warnings) {
        Write-Host "::warning title=$Provider provider model validation::$validationWarning"
    }
}

if (-not $validation.success) {
    # Surface each validation issue as a GitHub Actions error annotation so the
    # message is visible in the run log. See the matching block in the
    # -ValidateOnly path above for the rationale (Write-Error + EAP=Stop would
    # throw and obscure the cause).
    foreach ($validationError in $validation.errors) {
        Write-Host "::error title=$Provider provider model validation::$validationError"
    }
}

if ($FailOnValidationErrors -and -not $validation.success) {
    exit 9
}

exit 0
