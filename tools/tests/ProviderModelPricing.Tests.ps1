<#
.SYNOPSIS
    Deterministic tests for tools/ProviderModelPricing.psm1.

.DESCRIPTION
    Plain-PowerShell assertions (no Pester dependency, no network). Covers the
    entries-first (smarthopper-models/2) pricing parse/format/merge contract
    plus one end-to-end Update-ProviderModels.ps1 run against a temporary
    *ProviderModels.cs file with a mocked Invoke-RestMethod.

    Usage:  pwsh -NoProfile -File tools/tests/ProviderModelPricing.Tests.ps1
    Exit code: 0 = all passed, 1 = at least one failure.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '..' 'ProviderModelPricing.psm1') -Force -DisableNameChecking

$script:Pass = 0
$script:Fail = 0
$script:Failures = [System.Collections.Generic.List[string]]::new()

function Test-Case {
    param([string] $Name, [scriptblock] $Body)
    try {
        & $Body
        $script:Pass++
        Write-Host "PASS  $Name"
    }
    catch {
        $script:Fail++
        [void]$script:Failures.Add("${Name}: $($_.Exception.Message)")
        Write-Host "FAIL  $Name — $($_.Exception.Message)"
    }
}

function Assert-True {
    param($Condition, [string] $Message)
    if (-not $Condition) { throw "assert failed: $Message" }
}

function Assert-Equal {
    param($Expected, $Actual, [string] $Message)
    if ($Expected -ne $Actual) { throw "assert failed: $Message (expected [$Expected], got [$Actual])" }
}

function Assert-Throws {
    param([scriptblock] $Body, [string] $Pattern, [string] $Message)
    $threw = $false
    $errText = ''
    try { & $Body } catch { $threw = $true; $errText = $_.Exception.Message }
    Assert-True $threw "$Message (expected a throw, none occurred)"
    if ($Pattern) {
        Assert-True ($errText -match $Pattern) "$Message (error '$errText' did not match /$Pattern/)"
    }
}

function Find-Entry {
    param($Pricing, [string] $Kind, [string] $Unit)
    return @($Pricing.Entries | Where-Object { $_.Kind -eq $Kind -and $_.Unit -eq $Unit }) | Select-Object -First 1
}

# ---------------------------------------------------------------------------
# Read: standard + batch pricing stay independent; exact property boundary.
# ---------------------------------------------------------------------------
Test-Case 'read: Pricing and BatchPricing parse independently' {
    $block = @'
    new AIModelCapabilities
    {
        Model = "m1",
        Pricing = new AIModelPricing
        {
            Entries =
            {
                new AIPriceEntry("text_input", AIPriceUnit.Token, 0.001m),
                new AIPriceEntry("tool:web_search", AIPriceUnit.Invocation, 0.01m),
            },
        },
        BatchPricing = new AIModelPricing
        {
            Entries =
            {
                new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0005m),
            },
        },
    },
'@
    $p = Read-ModelPricingBlock -BlockText $block -PropertyName 'Pricing'
    Assert-Equal 2 @($p.Entries).Count 'pricing entry count'
    Assert-Equal '0.001' (Find-Entry $p 'text_input' 'Token').Amount 'text_input amount'
    Assert-Equal 'USD' $p.Currency 'currency defaults to USD'
    Assert-True ($null -eq $p.Discount) 'discount absent'

    $b = Read-ModelPricingBlock -BlockText $block -PropertyName 'BatchPricing'
    Assert-Equal 1 @($b.Entries).Count 'batch entry count'
    Assert-Equal '0.0005' (Find-Entry $b 'text_input' 'Token').Amount 'batch text_input amount'
}

Test-Case 'read: BatchPricing-only block is not mistaken for Pricing' {
    $block = @'
    new AIModelCapabilities
    {
        Model = "m2",
        BatchPricing = new AIModelPricing
        {
            Entries =
            {
                new AIPriceEntry("text_output", AIPriceUnit.Token, 0.002m),
            },
        },
    },
'@
    $p = Read-ModelPricingBlock -BlockText $block -PropertyName 'Pricing'
    Assert-True ($null -eq $p) 'Pricing must not match inside BatchPricing'
    $b = Read-ModelPricingBlock -BlockText $block -PropertyName 'BatchPricing'
    Assert-Equal '0.002' (Find-Entry $b 'text_output' 'Token').Amount 'batch amount'
}

Test-Case 'read: custom kind, per-minute, per-item and ComputeSecond units' {
    $block = @'
        Pricing = new AIModelPricing
        {
            Entries =
            {
                new AIPriceEntry("tool:custom_search", AIPriceUnit.Invocation, 0.02m),
                new AIPriceEntry("audio_input", AIPriceUnit.Minute, 0.006m),
                new AIPriceEntry("image_output", AIPriceUnit.Item, 0.04m),
                new AIPriceEntry("custom_compute", AIPriceUnit.ComputeSecond, 0.0002m),
            },
        },
'@
    $p = Read-ModelPricingBlock -BlockText $block -PropertyName 'Pricing'
    Assert-Equal 4 @($p.Entries).Count 'entry count'
    Assert-Equal '0.006' (Find-Entry $p 'audio_input' 'Minute').Amount 'minute amount'
    Assert-Equal '0.04'  (Find-Entry $p 'image_output' 'Item').Amount 'item amount'
    Assert-Equal '0.0002' (Find-Entry $p 'custom_compute' 'ComputeSecond').Amount 'compute amount'
    Assert-Equal '0.02' (Find-Entry $p 'tool:custom_search' 'Invocation').Amount 'tool amount'
}

Test-Case 'read: currency and discount retained' {
    $block = @'
        Pricing = new AIModelPricing
        {
            Currency = "EUR",
            Discount = 0.5m,
            Entries =
            {
                new AIPriceEntry("text_input", AIPriceUnit.Token, 0.001m),
            },
        },
'@
    $p = Read-ModelPricingBlock -BlockText $block -PropertyName 'Pricing'
    Assert-Equal 'EUR' $p.Currency 'currency'
    Assert-Equal '0.5' $p.Discount 'discount'
}

Test-Case 'read: explicit zero and 15+ digit precision survive' {
    $block = @'
        Pricing = new AIModelPricing
        {
            Entries =
            {
                new AIPriceEntry("text_input", AIPriceUnit.Token, 0m),
                new AIPriceEntry("text_output", AIPriceUnit.Token, 0.123456789012345678m),
            },
        },
'@
    $p = Read-ModelPricingBlock -BlockText $block -PropertyName 'Pricing'
    Assert-Equal '0' (Find-Entry $p 'text_input' 'Token').Amount 'explicit zero kept'
    Assert-Equal '0.123456789012345678' (Find-Entry $p 'text_output' 'Token').Amount 'precision kept'

    $formatted = Format-ModelPricingBlock -Pricing $p -PropertyName 'Pricing'
    $p2 = Read-ModelPricingBlock -BlockText $formatted -PropertyName 'Pricing'
    Assert-Equal '0' (Find-Entry $p2 'text_input' 'Token').Amount 'zero round-trip'
    Assert-Equal '0.123456789012345678' (Find-Entry $p2 'text_output' 'Token').Amount 'precision round-trip'
}

Test-Case 'read: scientific notation normalizes to plain decimal' {
    $block = @'
        Pricing = new AIModelPricing
        {
            Entries =
            {
                new AIPriceEntry("text_input", AIPriceUnit.Token, 1.5e-7m),
            },
        },
'@
    $p = Read-ModelPricingBlock -BlockText $block -PropertyName 'Pricing'
    Assert-Equal '0.00000015' (Find-Entry $p 'text_input' 'Token').Amount 'expanded exponent'
}

Test-Case 'read: explicit zero exponents still expand' {
    $block = @'
        Pricing = new AIModelPricing
        {
            Entries =
            {
                new AIPriceEntry("text_input", AIPriceUnit.Token, 1e0m),
                new AIPriceEntry("text_output", AIPriceUnit.Token, 1e+0m),
                new AIPriceEntry("cache_read", AIPriceUnit.Token, .5e0m),
            },
        },
'@
    $p = Read-ModelPricingBlock -BlockText $block -PropertyName 'Pricing'
    Assert-Equal '1' (Find-Entry $p 'text_input' 'Token').Amount '1e0 expands'
    Assert-Equal '1' (Find-Entry $p 'text_output' 'Token').Amount '1e+0 expands'
    Assert-Equal '0.5' (Find-Entry $p 'cache_read' 'Token').Amount '.5e0 expands'
}

Test-Case 'read: leading zeros normalize to canonical wire form' {
    $block = @'
        Pricing = new AIModelPricing
        {
            Discount = 00.5m,
            Entries =
            {
                new AIPriceEntry("text_input", AIPriceUnit.Token, 0001.50m),
            },
        },
'@
    $p = Read-ModelPricingBlock -BlockText $block -PropertyName 'Pricing'
    Assert-Equal '1.50' (Find-Entry $p 'text_input' 'Token').Amount 'leading zeros stripped'
    Assert-Equal '0.5' ([string]$p.Discount) 'discount canonicalized'
}

Test-Case 'read: C# decimal representability boundary is exact' {
    $mk = { param($lit) @"
        Pricing = new AIModelPricing
        {
            Entries =
            {
                new AIPriceEntry("text_input", AIPriceUnit.Token, ${lit}m),
            },
        },
"@ }
    $p = Read-ModelPricingBlock -BlockText (& $mk '1e-28') -PropertyName 'Pricing'
    Assert-Equal '0.0000000000000000000000000001' (Find-Entry $p 'text_input' 'Token').Amount '1e-28 accepted'
    Assert-Throws { Read-ModelPricingBlock -BlockText (& $mk '1e-29') -PropertyName 'Pricing' } 'cannot be represented' '1e-29 rejected'
    $p2 = Read-ModelPricingBlock -BlockText (& $mk '7.9228162514264337593543950335') -PropertyName 'Pricing'
    Assert-Equal '7.9228162514264337593543950335' (Find-Entry $p2 'text_input' 'Token').Amount 'decimal.MaxValue accepted'
    Assert-Throws { Read-ModelPricingBlock -BlockText (& $mk '7.9228162514264337593543950336') -PropertyName 'Pricing' } 'cannot be represented' 'above decimal.MaxValue rejected'
}

Test-Case 'read: absent pricing returns null; explicit empty pricing yields entries=[]' {
    $none = Read-ModelPricingBlock -BlockText 'Model = "m3",' -PropertyName 'Pricing'
    Assert-True ($null -eq $none) 'absent pricing is null'

    $empty = Read-ModelPricingBlock -BlockText 'Pricing = new AIModelPricing { Entries = { }, },' -PropertyName 'Pricing'
    Assert-True ($null -ne $empty) 'explicit empty pricing is preserved'
    Assert-Equal 0 @($empty.Entries).Count 'empty entries list'
    $fmt = Format-ModelPricingBlock -Pricing $empty -PropertyName 'Pricing'
    Assert-True ($fmt -match 'Entries = \{ \}') 'empty pricing formats explicitly'
}

Test-Case 'read: non-literal pricing expression parses as absent' {
    # `Pricing = modelPricing` / `Pricing = GetDefaultPricing()` carry computed
    # values, not literals — the seed parser only interprets literal
    # `new AIModelPricing` blocks, so these read as "no pricing".
    $block = 'Model = "m4", Pricing = GetDefaultPricing(), BatchPricing = modelPricing,'
    Assert-True ($null -eq (Read-ModelPricingBlock -BlockText $block -PropertyName 'Pricing')) 'computed pricing is not a literal'
}

# ---------------------------------------------------------------------------
# Read: failures — flat v1 fields, malformed rates, duplicates, bad units.
# ---------------------------------------------------------------------------
Test-Case 'read: flat v1 pricing member is rejected' {
    $block = 'Pricing = new AIModelPricing { TextInput = 0.001m, TextOutput = 0.002m },'
    Assert-Throws { Read-ModelPricingBlock -BlockText $block -PropertyName 'Pricing' | Out-Null } 'unsupported.*TextInput' 'flat field rejected'
}

Test-Case 'read: malformed rate literal is rejected' {
    $block = @'
        Pricing = new AIModelPricing
        {
            Entries =
            {
                new AIPriceEntry("text_input", AIPriceUnit.Token, abc),
            },
        },
'@
    Assert-Throws { Read-ModelPricingBlock -BlockText $block -PropertyName 'Pricing' | Out-Null } 'malformed' 'non-numeric rate rejected'
}

Test-Case 'read: duplicate kind/unit pair is rejected' {
    $block = @'
        Pricing = new AIModelPricing
        {
            Entries =
            {
                new AIPriceEntry("text_input", AIPriceUnit.Token, 0.001m),
                new AIPriceEntry("text_input", AIPriceUnit.Token, 0.002m),
            },
        },
'@
    Assert-Throws { Read-ModelPricingBlock -BlockText $block -PropertyName 'Pricing' | Out-Null } 'duplicate' 'duplicate kind/unit rejected'
}

Test-Case 'read: unsupported unit is rejected' {
    $block = 'Pricing = new AIModelPricing { Entries = { new AIPriceEntry("audio_input", AIPriceUnit.Furlong, 0.1m) } },'
    Assert-Throws { Read-ModelPricingBlock -BlockText $block -PropertyName 'Pricing' | Out-Null } 'Furlong' 'unknown unit rejected'
}

Test-Case 'read: invalid kind and out-of-range discount are rejected' {
    Assert-Throws {
        Read-ModelPricingBlock -BlockText 'Pricing = new AIModelPricing { Entries = { new AIPriceEntry("Bad_Kind", AIPriceUnit.Token, 0.1m) } },' -PropertyName 'Pricing' | Out-Null
    } 'kind' 'uppercase kind rejected'
    Assert-Throws {
        Read-ModelPricingBlock -BlockText 'Pricing = new AIModelPricing { Discount = 1m, Entries = { new AIPriceEntry("text_input", AIPriceUnit.Token, 0.1m) } },' -PropertyName 'Pricing' | Out-Null
    } 'Discount' 'discount >= 1 rejected'
}

# ---------------------------------------------------------------------------
# Format: indentation, literal shapes, currency/discount retention.
# ---------------------------------------------------------------------------
Test-Case 'format: emits v2 block at 20-space member indentation' {
    $p = [pscustomobject]@{
        Currency = 'USD'
        Discount = $null
        Entries  = @(
            [pscustomobject]@{ Kind = 'text_input';  Unit = 'Token';      Amount = '0.0000001' }
            [pscustomobject]@{ Kind = 'tool:web_search'; Unit = 'Invocation'; Amount = '0.01' }
        )
    }
    $out = Format-ModelPricingBlock -Pricing $p -PropertyName 'Pricing'
    Assert-True ($out -match '(?m)^                    Pricing = new AIModelPricing\r?$') 'property line at 20 spaces'
    Assert-True ($out -match 'new AIPriceEntry\("text_input", AIPriceUnit\.Token, 0\.0000001m\),') 'positional entry with m suffix'
    Assert-True ($out -match 'new AIPriceEntry\("tool:web_search", AIPriceUnit\.Invocation, 0\.01m\),') 'invocation entry'
    Assert-True ($out -notmatch 'Currency') 'USD currency omitted (C# default)'
    Assert-True ($out -notmatch 'TextInput =|Prompt =') 'no legacy flat field emitted'
}

Test-Case 'format: currency and discount retained in output' {
    $p = [pscustomobject]@{
        Currency = 'EUR'
        Discount = '0.25'
        Entries  = @([pscustomobject]@{ Kind = 'text_input'; Unit = 'Token'; Amount = '0.001' })
    }
    $out = Format-ModelPricingBlock -Pricing $p -PropertyName 'BatchPricing'
    Assert-True ($out -match 'BatchPricing = new AIModelPricing') 'BatchPricing property'
    Assert-True ($out -match 'Currency = "EUR",') 'currency emitted'
    Assert-True ($out -match 'Discount = 0\.25m,') 'discount emitted with m suffix'
}

# ---------------------------------------------------------------------------
# Merge semantics.
# ---------------------------------------------------------------------------
Test-Case 'merge: incoming wins on full kind/unit key; curated entries survive' {
    $existing = [pscustomobject]@{
        Currency = 'USD'
        Discount = '0.5'
        Entries  = @(
            [pscustomobject]@{ Kind = 'text_input';  Unit = 'Token'; Amount = '0.001' }
            [pscustomobject]@{ Kind = 'audio_input'; Unit = 'Minute'; Amount = '0.006' }
        )
    }
    $incoming = [pscustomobject]@{
        Currency = 'USD'
        Discount = $null
        Entries  = @(
            [pscustomobject]@{ Kind = 'text_input';  Unit = 'Token'; Amount = '0.002' }
            [pscustomobject]@{ Kind = 'audio_input'; Unit = 'Token'; Amount = '0.0001' }
        )
    }
    $m = Merge-ModelPricing -Existing $existing -Incoming $incoming
    Assert-Equal '0.002' (Find-Entry $m 'text_input' 'Token').Amount 'incoming wins same key'
    Assert-Equal '0.006' (Find-Entry $m 'audio_input' 'Minute').Amount 'curated other-unit entry survives'
    Assert-Equal '0.0001' (Find-Entry $m 'audio_input' 'Token').Amount 'new key appended'
    Assert-Equal '0.5' $m.Discount 'existing discount kept when incoming omits it'
    Assert-Equal 3 @($m.Entries).Count 'merged entry count'
}

Test-Case 'merge: incoming discount overwrites; different currency replaces whole pricing' {
    $existing = [pscustomobject]@{
        Currency = 'USD'; Discount = '0.5'
        Entries = @([pscustomobject]@{ Kind = 'text_input'; Unit = 'Token'; Amount = '0.001' })
    }
    $withDiscount = [pscustomobject]@{
        Currency = 'USD'; Discount = '0.1'
        Entries = @([pscustomobject]@{ Kind = 'text_input'; Unit = 'Token'; Amount = '0.002' })
    }
    $m = Merge-ModelPricing -Existing $existing -Incoming $withDiscount
    Assert-Equal '0.1' $m.Discount 'incoming discount wins'

    $eur = [pscustomobject]@{
        Currency = 'EUR'; Discount = $null
        Entries = @([pscustomobject]@{ Kind = 'text_input'; Unit = 'Token'; Amount = '0.003' })
    }
    $m2 = Merge-ModelPricing -Existing $existing -Incoming $eur
    Assert-Equal 'EUR' $m2.Currency 'different currency replaces whole pricing'
    Assert-Equal 1 @($m2.Entries).Count 'replaced bucket has only incoming entries'
    Assert-True ($null -eq $m2.Discount) 'replaced bucket drops old discount'
}

Test-Case 'merge: null handling' {
    $p = [pscustomobject]@{ Currency = 'USD'; Discount = $null; Entries = @([pscustomobject]@{ Kind = 'text_input'; Unit = 'Token'; Amount = '0.001' }) }
    Assert-True ([object]::ReferenceEquals($p, (Merge-ModelPricing -Existing $null -Incoming $p))) 'null existing returns incoming'
    Assert-True ([object]::ReferenceEquals($p, (Merge-ModelPricing -Existing $p -Incoming $null))) 'null incoming returns existing'
}

# ---------------------------------------------------------------------------
# OpenRouter conversion: full mapping, zero kept, video_input wins, negatives skipped.
# ---------------------------------------------------------------------------
Test-Case 'openrouter: full field map incl. image item vs image_token token' {
    $raw = [pscustomobject]@{
        prompt             = '0.00000015'
        completion         = '0.0000006'
        request            = '0.001'
        image              = '0.04'
        image_output       = '0.0001'
        image_token        = '0.0000002'
        audio              = '0.0003'
        audio_output       = '0.0004'
        video              = '0.05'
        video_input        = '0.06'
        video_output       = '0.07'
        input_audio_cache  = '0.00008'
        input_cache_read   = '0.000009'
        input_cache_write  = '0.00001'
        internal_reasoning = '0.00002'
        web_search         = '0.01'
        discount           = '0.25'
    }
    $p = ConvertFrom-OpenRouterPricing -Pricing $raw
    Assert-Equal 'USD' $p.Currency 'currency is USD'
    Assert-Equal '0.25' $p.Discount 'discount carried'
    Assert-Equal 15 @($p.Entries).Count 'entry count (video+video_input share one key)'

    $expect = [ordered]@{
        'text_input|Token'         = '0.00000015'
        'text_output|Token'        = '0.0000006'
        'request|Invocation'       = '0.001'
        'image_input|Item'         = '0.04'
        'image_output|Token'       = '0.0001'
        'image_input|Token'        = '0.0000002'
        'audio_input|Token'        = '0.0003'
        'audio_output|Token'       = '0.0004'
        'video_input|Token'        = '0.06'
        'video_output|Token'       = '0.07'
        'audio_cache_read|Token'   = '0.00008'
        'cache_read|Token'         = '0.000009'
        'cache_write|Token'        = '0.00001'
        'internal_reasoning|Token' = '0.00002'
        'tool:web_search|Invocation' = '0.01'
    }
    foreach ($key in $expect.Keys) {
        $kind, $unit = $key -split '\|'
        $hit = Find-Entry $p $kind $unit
        Assert-True ($null -ne $hit) "missing entry $key"
        Assert-Equal $expect[$key] $hit.Amount "$key amount"
    }
}

Test-Case 'openrouter: zero kept, negative and invalid skipped, video_input authoritative' {
    $raw = [pscustomobject]@{
        prompt      = '0'
        completion  = '-0.5'
        audio       = 'not-a-number'
        video       = '0.05'
        video_input = '0.06'
        image       = $null
    }
    $p = ConvertFrom-OpenRouterPricing -Pricing $raw
    Assert-Equal '0' (Find-Entry $p 'text_input' 'Token').Amount 'zero kept'
    Assert-True ($null -eq (Find-Entry $p 'text_output' 'Token')) 'negative skipped'
    Assert-True ($null -eq (Find-Entry $p 'audio_input' 'Token')) 'non-numeric skipped'
    Assert-Equal '0.06' (Find-Entry $p 'video_input' 'Token').Amount 'video_input wins over video'
    Assert-True ($null -eq (Find-Entry $p 'image_input' 'Item')) 'null skipped'
    Assert-Equal 2 @($p.Entries).Count 'only valid entries converted'

    $videoOnly = ConvertFrom-OpenRouterPricing -Pricing ([pscustomobject]@{ video = '0.05' })
    Assert-Equal '0.05' (Find-Entry $videoOnly 'video_input' 'Token').Amount 'video alone maps to video_input'

    Assert-True ($null -eq (ConvertFrom-OpenRouterPricing -Pricing $null)) 'null pricing -> null'
    Assert-True ($null -eq (ConvertFrom-OpenRouterPricing -Pricing ([pscustomobject]@{}))) 'empty pricing -> null'
}

Test-Case 'openrouter: invalid discount skipped, numeric rates accepted' {
    $p = ConvertFrom-OpenRouterPricing -Pricing ([pscustomobject]@{ prompt = '0.001'; discount = '1.5' })
    Assert-True ($null -eq $p.Discount) 'discount >= 1 dropped'
    $p2 = ConvertFrom-OpenRouterPricing -Pricing ([pscustomobject]@{ prompt = 0.0000001 })
    Assert-Equal '0.0000001' (Find-Entry $p2 'text_input' 'Token').Amount 'numeric (non-string) rate converts'
}

# ---------------------------------------------------------------------------
# End-to-end: Update-ProviderModels.ps1 with mocked Invoke-RestMethod.
# ---------------------------------------------------------------------------
Test-Case 'e2e: Update-ProviderModels merges OpenRouter pricing into temp file' {
    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("pmpricing-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tempDir | Out-Null
    $target = Join-Path $tempDir 'DeepSeekProviderModels.cs'
    @'
internal static class DeepSeekProviderModels
{
    private static List<AIModelCapabilities> RetrieveModels()
    {
        var provider = this.ProviderName.Name.ToLowerInvariant();
        var models = new List<AIModelCapabilities>
        {
            new AIModelCapabilities
            {
                Provider = provider,
                Model = "mock-model",
                Capabilities = AICapability.TextInput | AICapability.TextOutput,
                Verified = false,
                Rank = 50,
                Pricing = new AIModelPricing
                {
                    Entries =
                    {
                        new AIPriceEntry("custom_fee", AIPriceUnit.Item, 0.25m),
                    },
                },
            },
        };
        return models;
    }
}
'@ | Set-Content -Path $target -Encoding utf8NoBOM

    function Invoke-RestMethod {
        param($Uri, $Headers, $Method, $TimeoutSec)
        return [pscustomobject]@{
            data = @(
                [pscustomobject]@{
                    id             = 'deepseek/mock-model'
                    context_length = 64000
                    created        = 1700000000
                    architecture   = [pscustomobject]@{ input_modalities = @('text'); output_modalities = @('text') }
                    supported_parameters = @('tools')
                    pricing        = [pscustomobject]@{
                        prompt     = '0.0000005'
                        completion = '0.000001'
                        image_token = '0'
                        web_search = '0.01'
                        audio      = '-1'
                    }
                }
            )
        }
    }

    try {
        $scriptPath = Join-Path $PSScriptRoot '..' 'Update-ProviderModels.ps1'
        $reportJson = & $scriptPath -Provider DeepSeek -TargetFile $target -UpdateFile | Out-String
        $report = $reportJson.Trim() | ConvertFrom-Json
        Assert-True ($report.fileUpdated -eq $true) 'file should be rewritten'

        $written = Get-Content $target -Raw
        Assert-True ($written -match 'new AIPriceEntry\("text_input", AIPriceUnit\.Token, 0\.0000005m\)') 'prompt -> text_input/Token'
        Assert-True ($written -match 'new AIPriceEntry\("text_output", AIPriceUnit\.Token, 0\.000001m\)') 'completion -> text_output/Token'
        Assert-True ($written -match 'new AIPriceEntry\("image_input", AIPriceUnit\.Token, 0m\)') 'zero image_token kept'
        Assert-True ($written -match 'new AIPriceEntry\("tool:web_search", AIPriceUnit\.Invocation, 0\.01m\)') 'web_search -> tool kind'
        Assert-True ($written -match 'new AIPriceEntry\("custom_fee", AIPriceUnit\.Item, 0\.25m\)') 'curated entry preserved'
        Assert-True ($written -notmatch 'audio_input') 'negative audio rate skipped'

        # The rewritten file must re-parse cleanly (round-trip stability).
        $blockRx = [regex]::new('new\s+AIModelCapabilities\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\},?', [System.Text.RegularExpressions.RegexOptions]::Singleline)
        $bm = $blockRx.Matches($written) | Select-Object -First 1
        $reparsed = Read-ModelPricingBlock -BlockText $bm.Value -PropertyName 'Pricing'
        Assert-Equal 5 @($reparsed.Entries).Count 'reparsed entry count'
    }
    finally {
        Remove-Item -Path 'function:Invoke-RestMethod' -Force -ErrorAction SilentlyContinue
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------------------
Write-Host ''
Write-Host "Results: $($script:Pass) passed, $($script:Fail) failed"
if ($script:Failures.Count -gt 0) {
    foreach ($f in $script:Failures) { Write-Host "  - $f" }
}
exit $(if ($script:Fail -gt 0) { 1 } else { 0 })
