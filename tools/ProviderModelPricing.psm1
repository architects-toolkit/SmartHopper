<#
.SYNOPSIS
    Entries-first (smarthopper-models/2) pricing helpers for *ProviderModels.cs tooling.

.DESCRIPTION
    Shared parsing, merging and formatting of v2 `AIModelPricing` blocks used by
    Update-ProviderModels.ps1 and by the deterministic tooling tests in
    tools/tests/ProviderModelPricing.Tests.ps1.

    Only the entries-first shape is supported:
        Pricing = new AIModelPricing
        {
            Currency = "USD",
            Discount = 0.5m,
            Entries =
            {
                new AIPriceEntry("text_input", AIPriceUnit.Token, 0.0000001m),
            },
        },

    Flat v1 member fields (TextInput = ..., Prompt = ...) are rejected loudly
    instead of silently dropping rates.

    Internal pricing object shape (amounts stay invariant decimal text so
    precision survives parse -> merge -> format round-trips):

        [pscustomobject]@{
            Currency = 'USD'      # ^[A-Z]{3}$, default 'USD'
            Discount = '0.25'     # normalized decimal text, 0 <= d < 1, or $null
            Entries  = @(
                [pscustomobject]@{ Kind = 'text_input'; Unit = 'Token'; Amount = '0.0000001' }
            )
        }

    Kind vocabulary matches the catalogue contract
    (^[a-z][a-z0-9_]{0,31}(:[a-z][a-z0-9_]{0,31})?$); Unit is the C#
    AIPriceUnit enum member name (Token, Second, Minute, Character, Item,
    Megapixel, ComputeSecond, Credit, Invocation).
#>

$script:PriceKindPattern = '^[a-z][a-z0-9_]{0,31}(:[a-z][a-z0-9_]{0,31})?$'
$script:CurrencyPattern  = '^[A-Z]{3}$'
$script:PriceUnits       = @('Token', 'Second', 'Minute', 'Character', 'Item', 'Megapixel', 'ComputeSecond', 'Credit', 'Invocation')
$script:MaxEntries       = 64
# C# decimal ceiling: 29 significant digits, max value 79228162514264337593543950335.
$script:DecimalMaxInt    = '79228162514264337593543950335'

# ---------------------------------------------------------------------------
# Internal: expand a C# decimal literal to plain invariant decimal text.
# Accepts digits with optional fraction/exponent and the m/M suffix.
# Scientific notation is expanded with pure string arithmetic (never a double),
# so no precision is lost. Non-exponent literals keep their authored spelling.
# ---------------------------------------------------------------------------
function ConvertTo-NormalizedDecimalLiteral {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string] $Literal)

    $t = $Literal.Trim()
    if ($t.EndsWith('m') -or $t.EndsWith('M')) { $t = $t.Substring(0, $t.Length - 1) }

    $intPart = $null; $fracPart = ''; $expPart = $null; $leadingDot = $false
    $m = [regex]::Match($t, '^([0-9]+)(?:\.([0-9]+))?(?:[eE]([+-]?[0-9]+))?$')
    if ($m.Success) {
        $intPart = $m.Groups[1].Value
        if ($m.Groups[2].Success) { $fracPart = $m.Groups[2].Value }
        if ($m.Groups[3].Success) { $expPart = $m.Groups[3].Value }
    }
    else {
        # `.5m` is a valid C# real literal too.
        $m = [regex]::Match($t, '^\.([0-9]+)(?:[eE]([+-]?[0-9]+))?$')
        if (-not $m.Success) { throw "Invalid C# decimal literal '$Literal'." }
        $leadingDot = $true
        $intPart = ''
        $fracPart = $m.Groups[1].Value
        if ($m.Groups[2].Success) { $expPart = $m.Groups[2].Value }
    }

    $exp = 0L
    if (-not [string]::IsNullOrEmpty($expPart)) {
        try { $exp = [long]::Parse($expPart) } catch { throw "Invalid C# decimal literal '$Literal'." }
        if ([math]::Abs($exp) -gt 30) { throw "Decimal literal '$Literal' is outside the representable range." }
    }

    if ([string]::IsNullOrEmpty($expPart) -and -not $leadingDot) {
        $normalized = $t
    }
    else {
        $digits = $intPart + $fracPart
        $point  = $intPart.Length + $exp
        if ($point -le 0) {
            $normalized = '0.' + ('0' * (-$point)) + $digits
        }
        elseif ($point -ge $digits.Length) {
            $normalized = $digits + ('0' * ($point - $digits.Length))
        }
        else {
            $normalized = $digits.Substring(0, $point) + '.' + $digits.Substring($point)
        }
    }

    # Canonical wire shape: strip leading integer zeros only; authored
    # fractional zeros and precision are retained in the output.
    $normalized = $normalized -replace '^0+(?=\d)', ''

    # Exact C# decimal representability: coefficient must fit decimal.MaxValue
    # and the effective scale must not exceed 28 digits. Pure lexical
    # comparison on the equal-length coefficient — no floating point.
    $split    = $normalized -split '\.', 2
    $whole    = $split[0]
    $fraction = if ($split.Count -gt 1) { $split[1] } else { '' }
    $scaleDigits  = $fraction -replace '0+$', ''
    $coefficient  = ($whole + $scaleDigits) -replace '^0+', ''
    if ($coefficient -eq '') { $coefficient = '0' }
    if (($coefficient -ne '0' -and $scaleDigits.Length -gt 28) -or
        $coefficient.Length -gt $script:DecimalMaxInt.Length -or
        ($coefficient.Length -eq $script:DecimalMaxInt.Length -and [string]::CompareOrdinal($coefficient, $script:DecimalMaxInt) -gt 0)) {
        throw "Decimal literal '$Literal' cannot be represented exactly as a C# decimal."
    }

    return $normalized
}

# ---------------------------------------------------------------------------
# Internal: balanced-brace extraction starting at the '{' at $OpenBraceIndex.
# String-literal aware so braces inside "..." do not miscount.
# ---------------------------------------------------------------------------
function Read-BalancedBlock {
    param([string] $Text, [int] $OpenBraceIndex)

    $depth = 0
    $inString = $false
    $stringChar = [char]0
    for ($i = $OpenBraceIndex; $i -lt $Text.Length; $i++) {
        $ch = $Text[$i]
        if ($inString) {
            if ($ch -eq $stringChar -and ($i -eq 0 -or $Text[$i - 1] -ne '\')) { $inString = $false }
            continue
        }
        if ($ch -eq '"' -or $ch -eq "'") { $inString = $true; $stringChar = $ch; continue }
        if ($ch -eq '{') { $depth++ }
        elseif ($ch -eq '}') {
            $depth--
            if ($depth -eq 0) {
                return @{ Body = $Text.Substring($OpenBraceIndex + 1, $i - $OpenBraceIndex - 1); End = $i }
            }
        }
    }
    return $null
}

# ---------------------------------------------------------------------------
# Internal: parse the body of an `Entries = { ... }` block into entry objects.
# Throws on malformed entries so no rate silently disappears.
# ---------------------------------------------------------------------------
function Read-PriceEntries {
    param([string] $EntriesBody, [string] $Context)

    $decLit  = '(?:[0-9]+(?:\.[0-9]+)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?[mM]?'
    $entryRx = [regex]::new('new\s+AIPriceEntry\s*\(\s*"([^"]+)"\s*,\s*AIPriceUnit\.([A-Za-z_][A-Za-z0-9_]*)\s*,\s*(' + $decLit + ')\s*\)')
    $looseRx = [regex]::new('new\s+AIPriceEntry\s*\(')

    $bodyNoComments = [regex]::Replace($EntriesBody, '//[^\r\n]*', '')

    $strictMatches = $entryRx.Matches($bodyNoComments)
    $looseCount    = $looseRx.Matches($bodyNoComments).Count
    if ($strictMatches.Count -ne $looseCount) {
        throw "${Context}: malformed AIPriceEntry literal in AIModelPricing Entries ($($strictMatches.Count) of $looseCount parsed)."
    }

    # After removing every parsed entry only separators may remain — anything
    # else is content we would otherwise drop silently.
    $residual = $entryRx.Replace($bodyNoComments, '')
    if ($residual -match '[^\s,]') {
        $bad = [regex]::Match($residual, '\S+').Value
        throw "${Context}: unsupported content in AIModelPricing Entries near '$bad'."
    }

    $entries = [System.Collections.Generic.List[object]]::new()
    $seen = @{}
    foreach ($m in $strictMatches) {
        $kind = $m.Groups[1].Value
        $unit = $m.Groups[2].Value
        if (-not [regex]::IsMatch($kind, $script:PriceKindPattern)) {
            throw "${Context}: invalid price kind '$kind'."
        }
        if (-not ($script:PriceUnits -ccontains $unit)) {
            throw "${Context}: unsupported AIPriceUnit '$unit'."
        }
        $key = "$kind|$unit"
        if ($seen.ContainsKey($key)) {
            throw "${Context}: duplicate price entry '$kind'/'$unit'."
        }
        $seen[$key] = $true
        $entries.Add([pscustomobject][ordered]@{
            Kind   = $kind
            Unit   = $unit
            Amount = ConvertTo-NormalizedDecimalLiteral -Literal $m.Groups[3].Value
        })
    }
    if ($entries.Count -gt $script:MaxEntries) {
        throw "${Context}: $($entries.Count) entries exceeds the wire maximum of $script:MaxEntries."
    }
    return $entries.ToArray()
}

# ---------------------------------------------------------------------------
# Internal: parse the inner body of `new AIModelPricing { ... }`.
# Only Currency, Discount and Entries are valid members; anything else
# (flat v1 fields, stray expressions) throws.
# ---------------------------------------------------------------------------
function ConvertFrom-PricingBody {
    param([string] $Body, [string] $Context)

    $currency = 'USD'
    $discount = $null
    $entries  = @()

    # --- Entries member (optional): `Entries = { ... }` or `Entries = new ... { ... }`
    $entriesMatch = [regex]::Match($Body, '(?<![A-Za-z0-9_])Entries\s*=')
    $residual = $Body
    if ($entriesMatch.Success) {
        $j = $entriesMatch.Index + $entriesMatch.Length
        while ($j -lt $Body.Length -and [char]::IsWhiteSpace($Body[$j])) { $j++ }
        $openIdx = -1
        if ($j -lt $Body.Length -and $Body[$j] -eq '{') {
            $openIdx = $j
        }
        elseif ($j -lt $Body.Length -and $Body.Substring($j) -match '^new\b') {
            # `Entries = new List<AIPriceEntry> { ... }` / `new() { ... }` / `new[] { ... }`
            $bm = [regex]::Match($Body.Substring($j), '\{')
            if ($bm.Success) { $openIdx = $j + $bm.Index }
        }
        if ($openIdx -lt 0) {
            throw "${Context}: unsupported Entries expression in AIModelPricing (expected a collection initializer)."
        }
        $blk = Read-BalancedBlock -Text $Body -OpenBraceIndex $openIdx
        if (-not $blk) { throw "${Context}: unbalanced braces in AIModelPricing Entries." }
        $entries  = Read-PriceEntries -EntriesBody $blk.Body -Context $Context
        $residual = $Body.Substring(0, $entriesMatch.Index) + $Body.Substring($blk.End + 1)
    }

    $residual = [regex]::Replace($residual, '//[^\r\n]*', '')

    # Every member assignment left must be Currency or Discount; a flat v1
    # field or a second Entries member fails here instead of being dropped.
    foreach ($mm in [regex]::Matches($residual, '(?<![A-Za-z0-9_])([A-Za-z_][A-Za-z0-9_]*)\s*=(?!=)')) {
        $name = $mm.Groups[1].Value
        if ($name -ne 'Currency' -and $name -ne 'Discount') {
            throw "${Context}: unsupported AIModelPricing member '$name' (flat-field pricing is not supported — use Entries)."
        }
    }

    # After removing well-formed Currency/Discount members only separators may
    # remain; a malformed value or stray expression (e.g. a bare AIPriceEntry
    # outside Entries) leaves residue that must not be swallowed.
    $check = $residual
    $check = [regex]::Replace($check, '(?<![A-Za-z0-9_])Currency\s*=\s*"[^"]*"\s*,?', '')
    $check = [regex]::Replace($check, '(?<![A-Za-z0-9_])Discount\s*=\s*(?:[0-9]+(?:\.[0-9]+)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?[mM]?\s*,?', '')
    $badMember = [regex]::Match($check, '(?<![A-Za-z0-9_])([A-Za-z_][A-Za-z0-9_]*)\s*=(?!=)')
    if ($badMember.Success) {
        throw "${Context}: malformed $($badMember.Groups[1].Value) value in AIModelPricing."
    }
    if ($check -match '[^\s,]') {
        $bad = [regex]::Match($check, '[A-Za-z_][A-Za-z0-9_.:]*|\S').Value
        throw "${Context}: unsupported content '$bad' in AIModelPricing — only Currency, Discount and Entries are valid members."
    }

    $currencyMatch = [regex]::Match($residual, '(?<![A-Za-z0-9_])Currency\s*=\s*"([^"]*)"')
    if ($currencyMatch.Success) { $currency = $currencyMatch.Groups[1].Value }
    if (-not [regex]::IsMatch($currency, $script:CurrencyPattern)) {
        throw "${Context}: invalid currency '$currency'."
    }

    $discountMatch = [regex]::Match($residual, '(?<![A-Za-z0-9_])Discount\s*=\s*((?:[0-9]+(?:\.[0-9]+)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?[mM]?)')
    if ($discountMatch.Success) {
        $discount = ConvertTo-NormalizedDecimalLiteral -Literal $discountMatch.Groups[1].Value
        # Contract: discount factor must be >= 0 and strictly < 1.
        if ($discount -notmatch '^0(\.[0-9]+)?$') {
            throw "${Context}: Discount must be >= 0 and < 1, got '$($discountMatch.Groups[1].Value)'."
        }
    }

    return [pscustomobject][ordered]@{
        Currency = $currency
        Discount = $discount
        Entries  = $entries
    }
}

# ---------------------------------------------------------------------------
# Internal: re-validate an internal pricing object (used by Merge/Format so
# invalid input can never reach the emitted C#).
# ---------------------------------------------------------------------------
function Assert-ModelPricing {
    param($Pricing, [string] $Context)

    if ($null -eq $Pricing) { throw "${Context}: pricing object is null." }

    $currency = 'USD'
    if ($Pricing.PSObject.Properties['Currency'] -and -not [string]::IsNullOrWhiteSpace([string]$Pricing.Currency)) {
        $currency = [string]$Pricing.Currency
    }
    if (-not [regex]::IsMatch($currency, $script:CurrencyPattern)) {
        throw "${Context}: invalid currency '$currency'."
    }

    $discount = $null
    if ($null -ne $Pricing.Discount -and -not [string]::IsNullOrWhiteSpace([string]$Pricing.Discount)) {
        $discount = ConvertTo-NormalizedDecimalLiteral -Literal ([string]$Pricing.Discount)
        if ($discount -notmatch '^0(\.[0-9]+)?$') {
            throw "${Context}: discount must be >= 0 and < 1, got '$($Pricing.Discount)'."
        }
    }

    $entries = [System.Collections.Generic.List[object]]::new()
    $seen = @{}
    foreach ($e in @($Pricing.Entries | Where-Object { $null -ne $_ })) {
        $kind   = [string]$e.Kind
        $unit   = [string]$e.Unit
        $amount = [string]$e.Amount
        if (-not [regex]::IsMatch($kind, $script:PriceKindPattern)) { throw "${Context}: invalid price kind '$kind'." }
        if (-not ($script:PriceUnits -ccontains $unit))    { throw "${Context}: unsupported AIPriceUnit '$unit'." }
        $key = "$kind|$unit"
        if ($seen.ContainsKey($key)) { throw "${Context}: duplicate price entry '$kind'/'$unit'." }
        $seen[$key] = $true
        $entries.Add([pscustomobject][ordered]@{
            Kind   = $kind
            Unit   = $unit
            Amount = ConvertTo-NormalizedDecimalLiteral -Literal $amount
        })
    }
    if ($entries.Count -gt $script:MaxEntries) {
        throw "${Context}: $($entries.Count) entries exceeds the wire maximum of $script:MaxEntries."
    }

    return [pscustomobject][ordered]@{
        Currency = $currency
        Discount = $discount
        Entries  = $entries.ToArray()
    }
}

# ---------------------------------------------------------------------------
# Public: extract and parse `<PropertyName> = new AIModelPricing { ... }` from
# an AIModelCapabilities block. $null when absent; throws on malformed or
# unsupported (flat-field) pricing. The exact property-name boundary keeps
# 'Pricing' from matching the 'BatchPricing' member.
# ---------------------------------------------------------------------------
function Read-ModelPricingBlock {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string] $BlockText,
        [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string] $PropertyName
    )

    $rx = [regex]::new('(?<![A-Za-z0-9_])' + [regex]::Escape($PropertyName) + '\s*=\s*new\s+AIModelPricing(?:\s*\(\s*\))?\s*\{')
    $m = $rx.Match($BlockText)
    if (-not $m.Success) { return $null }

    $blk = Read-BalancedBlock -Text $BlockText -OpenBraceIndex ($m.Index + $m.Length - 1)
    if (-not $blk) { throw "Unbalanced braces in '$PropertyName = new AIModelPricing'." }

    return ConvertFrom-PricingBody -Body $blk.Body -Context $PropertyName
}

# ---------------------------------------------------------------------------
# Public: emit a `<PropertyName> = new AIModelPricing { ... }` C# block at the
# 20-space member indentation used inside AIModelCapabilities initializers.
# Currency is only emitted when non-default (USD is the C# default); Discount
# is emitted when set; Entries emit as positional AIPriceEntry literals with
# the m suffix. An explicitly empty pricing round-trips as `Entries = { }`.
# ---------------------------------------------------------------------------
function Format-ModelPricingBlock {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)] $Pricing,
        [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z_][A-Za-z0-9_]*$')][string] $PropertyName
    )

    $p = Assert-ModelPricing -Pricing $Pricing -Context $PropertyName

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("                    $PropertyName = new AIModelPricing")
    $lines.Add('                    {')
    if ($p.Currency -ne 'USD') {
        $lines.Add("                        Currency = `"$($p.Currency)`",")
    }
    if ($null -ne $p.Discount) {
        $lines.Add("                        Discount = $($p.Discount)m,")
    }
    if ($p.Entries.Count -eq 0) {
        $lines.Add('                        Entries = { },')
    }
    else {
        $lines.Add('                        Entries =')
        $lines.Add('                        {')
        foreach ($e in $p.Entries) {
            $lines.Add("                            new AIPriceEntry(`"$($e.Kind)`", AIPriceUnit.$($e.Unit), $($e.Amount)m),")
        }
        $lines.Add('                        },')
    }
    $lines.Add('                    },')
    return ($lines -join "`r`n")
}

# ---------------------------------------------------------------------------
# Public: merge two internal pricing objects on the full (kind, unit) key.
# Same currency: incoming entries overwrite matching keys (kept at their
# existing position) and append new keys; curated entries with no incoming
# counterpart survive; Discount comes from incoming when supplied, else
# existing. A different incoming currency replaces the whole pricing so a
# bucket never mixes money.
# ---------------------------------------------------------------------------
function Merge-ModelPricing {
    [CmdletBinding()]
    param($Existing, $Incoming)

    if ($null -eq $Existing) { return $Incoming }
    if ($null -eq $Incoming) { return $Existing }

    $e = Assert-ModelPricing -Pricing $Existing -Context 'existing pricing'
    $i = Assert-ModelPricing -Pricing $Incoming -Context 'incoming pricing'

    if ($i.Currency -ne $e.Currency) { return $Incoming }

    $existingKeys = @{}
    foreach ($en in $e.Entries) { $existingKeys["$($en.Kind)|$($en.Unit)"] = $true }
    $incomingByKey = @{}
    foreach ($en in $i.Entries) { $incomingByKey["$($en.Kind)|$($en.Unit)"] = $en }

    $merged = [System.Collections.Generic.List[object]]::new()
    foreach ($en in $e.Entries) {
        $key = "$($en.Kind)|$($en.Unit)"
        if ($incomingByKey.ContainsKey($key)) { $merged.Add($incomingByKey[$key]) }
        else { $merged.Add($en) }
    }
    foreach ($en in $i.Entries) {
        if (-not $existingKeys.ContainsKey("$($en.Kind)|$($en.Unit)")) { $merged.Add($en) }
    }

    return [pscustomobject][ordered]@{
        Currency = $e.Currency
        Discount = if ($null -ne $i.Discount) { $i.Discount } else { $e.Discount }
        Entries  = $merged.ToArray()
    }
}

# ---------------------------------------------------------------------------
# Public: convert a raw OpenRouter `pricing` object into the internal pricing
# shape. snake_case fields map to (kind, unit) entries in the declared order;
# `video` and `video_input` share the video_input/Token bucket so an explicit
# video_input wins when both are present. Absent, non-numeric and negative
# values are skipped; explicit zero is kept. Currency is always USD; a valid
# `discount` factor (< 1) is carried over.
# ---------------------------------------------------------------------------
function ConvertFrom-OpenRouterPricing {
    [CmdletBinding()]
    param($Pricing)

    if ($null -eq $Pricing) { return $null }

    $map = [ordered]@{
        prompt             = @{ Kind = 'text_input';         Unit = 'Token' }
        completion         = @{ Kind = 'text_output';        Unit = 'Token' }
        request            = @{ Kind = 'request';            Unit = 'Invocation' }
        image              = @{ Kind = 'image_input';        Unit = 'Item' }
        image_output       = @{ Kind = 'image_output';       Unit = 'Token' }
        image_token        = @{ Kind = 'image_input';        Unit = 'Token' }
        audio              = @{ Kind = 'audio_input';        Unit = 'Token' }
        audio_output       = @{ Kind = 'audio_output';       Unit = 'Token' }
        video              = @{ Kind = 'video_input';        Unit = 'Token' }
        video_input        = @{ Kind = 'video_input';        Unit = 'Token' }
        video_output       = @{ Kind = 'video_output';       Unit = 'Token' }
        input_audio_cache  = @{ Kind = 'audio_cache_read';   Unit = 'Token' }
        input_cache_read   = @{ Kind = 'cache_read';         Unit = 'Token' }
        input_cache_write  = @{ Kind = 'cache_write';        Unit = 'Token' }
        internal_reasoning = @{ Kind = 'internal_reasoning'; Unit = 'Token' }
        web_search         = @{ Kind = 'tool:web_search';    Unit = 'Invocation' }
    }

    $byKey = [ordered]@{}
    foreach ($kvp in $map.GetEnumerator()) {
        $prop = $Pricing.PSObject.Properties[$kvp.Key]
        if (-not $prop -or $null -eq $prop.Value) { continue }
        $raw = $prop.Value
        $s = if ($raw -is [string]) { $raw }
             else { [System.Convert]::ToString($raw, [System.Globalization.CultureInfo]::InvariantCulture) }
        if ([string]::IsNullOrWhiteSpace($s)) { continue }
        $amount = $null
        try { $amount = ConvertTo-NormalizedDecimalLiteral -Literal $s } catch { continue }
        $byKey["$($kvp.Value.Kind)|$($kvp.Value.Unit)"] = [pscustomobject][ordered]@{
            Kind   = $kvp.Value.Kind
            Unit   = $kvp.Value.Unit
            Amount = $amount
        }
    }

    $discount = $null
    $dprop = $Pricing.PSObject.Properties['discount']
    if ($dprop -and $null -ne $dprop.Value -and -not [string]::IsNullOrWhiteSpace([string]$dprop.Value)) {
        try {
            $d = ConvertTo-NormalizedDecimalLiteral -Literal ([string]$dprop.Value)
            if ($d -match '^0(\.[0-9]+)?$') { $discount = $d }
        } catch { }
    }

    if ($byKey.Count -eq 0 -and $null -eq $discount) { return $null }

    return [pscustomobject][ordered]@{
        Currency = 'USD'
        Discount = $discount
        Entries  = @($byKey.Values)
    }
}

Export-ModuleMember -Function @(
    'Read-ModelPricingBlock',
    'Format-ModelPricingBlock',
    'Merge-ModelPricing',
    'ConvertFrom-OpenRouterPricing'
)
