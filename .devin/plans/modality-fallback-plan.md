# SmartHopper v2 — Modality Fallback Resolver Plan (Phase 7)

Implements the last major pending feature from the v2 unified plan: an opt-in fallback chain
that converts unsupported input/output modalities into supported ones via extra AI calls,
with pre-execution warnings so users never silently consume tokens.

This plan also covers a cross-cutting prerequisite refactor (Phase M) to the entire metrics
pipeline so that any component that uses more than one provider/model pair for a single output
emits a **list** of metrics entries rather than a single collapsed object.

---

## Comparison: Original Plan vs Current Code

### What the original plan specified (Phase 7 + Phase 4.1)

| Item | Original spec | Current state |
|---|---|---|
| `IModalityFallback` interface | New interface in `Infrastructure` | ❌ Not implemented |
| `ModalityFallbackResolver` | New resolver class | ❌ Not implemented |
| `SmartHopperSettings.ModalityFallback` | Global enum (`Disabled`/`ConfiguredProvider`/`AnyProvider`), default `Disabled` | ❌ Not implemented |
| Per-component override via `AISettings` | Extra descriptor / input | ❌ Not implemented |
| `ComponentCapabilityValidator.ValidateSync` | Basic validation (Phase 0) | ✅ Implemented — but **binary** (valid/error only) |
| `ValidationResult.HasWarnings` / `FallbackDescription` | Carry fallback info | ⚠️ Partially — `WarningCount` exists on `ValidationResult`; no `FallbackDescription` |
| Yellow warning badge pre-execution | In `SolveInstance` before work starts | ❌ Not implemented (validator throws hard error instead) |

### What changed since the plan was written (outdated assumptions)

1. **`AIOutputAdapterBase` exists and already calls the validator** — the plan assumed
   `AIOutputComponentBase.SolveInstance` would be the hook point. In reality validation runs in
   `AIOutputAdapterBase.PrepareInputs` (per-branch, inside the worker), throwing
   `InvalidOperationException` on capability mismatch. The pre-validation hook should be added
   where the plan suggested (sync, before work dispatch), but the **per-branch validation also
   stays** since `RequiredCapability` can vary with payload contents.

2. **`ModelManager.SelectBestModel` already implements model-level fallback** — when a
   user-specified model lacks a capability, it silently falls back to a capable model *within the
   same provider*. This is a different axis than modality fallback (which transforms the
   *request*, not the model), but it means part of the "fallback" UX already exists without
   warnings. The new resolver must NOT duplicate this; instead it should kick in only when **no
   model in the provider supports the capability at all**.

3. **Audio is fully implemented (Phase 8 done)** — `AIInteractionAudio`, STT/TTS endpoints on
   OpenAI/MistralAI/Gemini all exist. The plan's example chain `AudioInput → STT model →
   TextInput` is now concretely realizable using existing provider audio APIs rather than
   hypothetical ones.

4. **`AIInputPayload.InputCapabilityAtSource` exists** — payloads already declare which
   capability their source requires (used by `AIInputPayloadMerger`). The resolver can derive the
   required modality directly from merged payload capabilities instead of needing new metadata.

5. **`img2text` tool exists and is batch-interceptable** — the plan's example
   `ImageInput (no vision model) → img2text → text` can reuse the existing tool via
   `CallAiToolAsync`, including under batch mode.

6. **`SHRuntimeMessage` system replaced `AIRuntimeMessage`** — warnings should be emitted as
   `SHRuntimeMessage(Warning, Validation, ...)` and surfaced via the existing
   `SetPersistentRuntimeMessage` / `SurfaceMessagesFromReturn` paths.

### Still required (validated against current code)

- The core resolver and interface — nothing equivalent exists.
- The global setting — `SmartHopperSettings` has no `ModalityFallback` property.
- `FallbackDescription` on `ValidationResult` (or a wrapper) — currently impossible to tell the
  user *what* fallback would run.
- Warning-not-error path in `ComponentCapabilityValidator` when fallback is available + enabled.

---

## Phase M — Multi-Provider Metrics List Refactor (prerequisite)

This is a **cross-cutting refactor** that must land before Phase 7. It is not specific to
modality fallback: the same need arises whenever any component routes work through more than
one provider/model pair (e.g. `img2text` using OpenAI vision inside `AIFile2MdComponent`
while the main call uses DeepSeek). Today that information is **silently lost** because
`AIMetrics.Combine()` overwrites `Provider`/`Model` with the last-seen values and sums all
token counts into a single object.

### M.1 Current state — what breaks today

| Location | Problem |
|---|---|
| `AIMetrics.Combine()` | Overwrites `Provider`/`Model` with the *last* entry; multi-provider token split is invisible |
| `BatchRunState.PersistedMetrics` | Single `AIMetrics` instance — cannot hold multiple provider/model pairs |
| `CombineIntoPersistedMetrics()` | Calls `Combine()` — same lossy collapse |
| `SetMetricsOutput()` | Serialises one `JObject`; `DeconstructMetricsComponent` reads `GH_ParamAccess.item` |
| `DeconstructMetricsComponent` | Single-item input, single-item outputs — entirely item-level |
| `AIOutputAdapterBase` aggregation | Sums branch metrics via `Combine()` — multi-provider context lost |

### M.2 New data model — `AIMetricsList`

Add a thin wrapper in `SmartHopper.Infrastructure.AICall.Metrics`:

```csharp
/// <summary>
/// Ordered list of per-call metrics for a single component solve.
/// Single-entry lists represent the common case (one provider/model).
/// Multi-entry lists arise when modality fallback, img2text, or any other
/// sub-call uses a different provider or model from the main call.
/// </summary>
public sealed class AIMetricsList
{
    private readonly List<AIMetrics> _entries = new();

    public IReadOnlyList<AIMetrics> Entries => this._entries;

    /// <summary>True when all entries share the same Provider and Model.</summary>
    public bool IsSingleProvider =>
        this._entries.Count <= 1 ||
        this._entries.All(m => m.Provider == this._entries[0].Provider
                             && m.Model    == this._entries[0].Model);

    /// <summary>
    /// Adds an entry. Entries with the same Provider+Model are merged (Combine);
    /// entries with a different Provider or Model are appended as a new entry.
    /// </summary>
    public void Add(AIMetrics metrics, string role = null)
    {
        if (metrics == null) return;
        metrics.Role = role;          // see §M.2a
        var existing = this._entries
            .FirstOrDefault(e => e.Provider == metrics.Provider && e.Model == metrics.Model);
        if (existing != null)
            existing.Combine(metrics);
        else
            this._entries.Add(metrics);
    }

    /// <summary>
    /// Returns a single aggregated AIMetrics (sum of all entries).
    /// Used for backwards-compat paths that still need a scalar total.
    /// </summary>
    public AIMetrics ToAggregate()
    {
        if (this._entries.Count == 0) return null;
        var agg = new AIMetrics { Provider = this._entries[0].Provider, Model = this._entries[0].Model };
        foreach (var e in this._entries) agg.Combine(e);
        return agg;
    }
}
```

#### M.2a `AIMetrics.Role` — new property

```csharp
/// <summary>
/// Optional human-readable label for this metrics entry, e.g.
/// "main", "fallback:ImageToText", "tool:img2text".
/// Null for the primary call (serialized as absent, not null).
/// </summary>
[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
public string Role { get; set; }
```

### M.3 Storage change — `BatchRunState`

Replace the single `AIMetrics PersistedMetrics` with `AIMetricsList`:

```csharp
// Before:
public AIMetrics PersistedMetrics { get; set; }

// After:
public AIMetricsList PersistedMetricsList { get; set; }
```

`ResetForNextRun()` sets it to `null`. The `PersistedMetrics` property on
`AIStatefulAsyncComponentBase` becomes a computed shim for backwards compatibility:

```csharp
// Keep as compatibility shim — reads/writes through PersistedMetricsList
protected AIMetrics PersistedMetrics
{
    get => this._batchState.PersistedMetricsList?.ToAggregate();
    set
    {
        this._batchState.PersistedMetricsList ??= new AIMetricsList();
        this._batchState.PersistedMetricsList.Add(value, role: "main");
    }
}
```

`CombineIntoPersistedMetrics(AIMetrics metrics)` gains an optional `role` parameter and
delegates to `AIMetricsList.Add(metrics, role)`:

```csharp
protected void CombineIntoPersistedMetrics(AIMetrics metrics, string role = null)
{
    this._batchState.PersistedMetricsList ??= new AIMetricsList();
    this._batchState.PersistedMetricsList.Add(metrics, role);
}
```

### M.4 Serialization — `SetMetricsOutput()`

#### M.4a `data_count` and `iterations_count` move into `AIMetrics`

Both fields are currently read from the component (`this.DataCount`, `this.ProgressInfo.Total`)
at serialization time and collapsed into a single value for the whole run. This is wrong: each
role in a multi-call run can have **different counts**.

Example: `AIFile2MdComponent` with one input file and image description enabled:
- Main call: `data_count = 1`, `iterations_count = 1` (one text summary)
- `tool:img2text` entries: `data_count = 3`, `iterations_count = 3` (three images described)

The counts must therefore be **set per-entry by the caller**, not stamped globally:

```csharp
// New properties on AIMetrics:
[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
public int? DataCount { get; set; }

[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
public int? IterationsCount { get; set; }
```

**Who sets them:** every call site that creates an `AIMetrics` and adds it to the list is
responsible for setting the correct counts for that role. For the main call this is the
existing component-level `this.DataCount` / `this.ProgressInfo.Total`. For sub-calls (e.g.
`img2text` inside `AIFile2MdComponent`, modality fallback) it is the count of items that
specific sub-call processed.

**`SetMetricsOutput` no longer stamps these fields** — it just serializes whatever is on each
entry. Entries for which the caller did not set a count serialize with the field absent
(`NullValueHandling.Ignore`), which is correct for e.g. a single-turn fallback call where
the concept of "data count" doesn't apply.

`AIMetrics.Combine()` **sums** the values (not max — when two same-provider entries merge,
their combined counts accumulate):

```csharp
this.DataCount = (this.DataCount ?? 0) + (other.DataCount ?? 0);
this.IterationsCount = (this.IterationsCount ?? 0) + (other.IterationsCount ?? 0);
```

`NullValueHandling.Ignore` ensures existing serialized metrics without these fields
deserialise cleanly (null → absent from JSON).

#### M.4b Serialization logic

`SetMetricsOutput` inspects `PersistedMetricsList`:

- **Single entry** (or `IsSingleProvider == true`): serialise as a single `JObject` — **no
  breaking change** to existing `DeconstructMetricsComponent` or user scripts.
- **Multiple entries** (different providers/models): serialise as a `JArray`, one object per
  entry.

Per-entry JSON shape (same keys as today, plus `role`; `data_count`/`iterations_count`
present only when set by the caller for that role):

```json
{
  "ai_provider": "openai",
  "ai_model": "gpt-4o",
  "role": "main",
  "tokens_input": 120,
  "tokens_input_prompt": 100,
  "tokens_input_cached": 20,
  "tokens_input_cache_write": 0,
  "tokens_output": 80,
  "tokens_output_reasoning": 0,
  "tokens_output_generation": 80,
  "finish_reason": "stop",
  "completion_time": 1.23,
  "context_usage_percent": 0.012,
  "data_count": 1,
  "iterations_count": 1
}
```

#### M.4c Multi-entry output shape (illustrating differing counts per role)

```json
[
  {
    "ai_provider": "deepseek",  "ai_model": "deepseek-chat",
    "role": "main",
    "tokens_input": 500, "tokens_output": 300,
    "finish_reason": "stop",  "completion_time": 2.1,  "context_usage_percent": 0.04,
    "data_count": 1,  "iterations_count": 1
  },
  {
    "ai_provider": "openai",  "ai_model": "gpt-4o",
    "role": "tool:img2text",
    "tokens_input": 900,  "tokens_output": 450,
    "finish_reason": "stop",  "completion_time": 1.2,  "context_usage_percent": 0.003,
    "data_count": 3,  "iterations_count": 3
  }
]
```

The main call processed 1 document; the `img2text` sub-call described 3 images from that
document — entirely independent counts, both meaningful to the user.

### M.5 `DeconstructMetricsComponent` refactor

The component **keeps the same `Metrics` input pin** but upgrades from `GH_ParamAccess.item`
to `GH_ParamAccess.list` so it can receive any mix of single-object or array JSON strings.

New behaviour:
- Parse each input string: if it is a `JObject`, treat as a single-entry list; if it is a
  `JArray`, iterate entries.
- **All output pins** upgrade from `GH_ParamAccess.item` to `GH_ParamAccess.list` so each
  output carries one value per metrics entry received.
- Entry order is preserved (main call first, fallback/tool calls after).
- `data_count` and `iterations_count` are per-role counts set by each call site (see §M.4a) —
  they differ between the main call and sub-calls, so their output pins upgrade to list access
  like all others.

**Upgraded output pins** (same names, same indices — no breaking GH param index change):

| Pin | Old access | New access | Notes |
|---|---|---|---|
| AI Provider | item | list | One string per entry |
| AI Model | item | list | One string per entry |
| Input Tokens | item | list | One int per entry |
| Output Tokens | item | list | One int per entry |
| Finish Reason | item | list | One string per entry |
| Completion Time | item | list | One double per entry |
| Context Usage | item | list | One double? per entry |
| Data Count | item | list | Per-role count; may differ across entries |
| Iterations Count | item | list | Per-role count; may differ across entries |
| **Role** *(new)* | — | list | One string per entry; null/absent for main |

> **Breaking change for downstream scripts:** any GH script that wires any output pin of
> `DMetrics` into a single-value parameter will now receive a list. This is a known breaking
> change and must be documented prominently in the CHANGELOG.

### M.6 Callers to update

All places that currently call `Combine()` directly on `PersistedMetrics` or write to it as
a scalar — they all need to route through `AIMetricsList.Add()` with an appropriate `role`:

| Location | Current call | New call |
|---|---|---|
| `AIStatefulAsyncComponentBase.Processing.cs` | `_batchState.PersistedMetrics = aggregatedMetrics` | Build via `AIMetricsList.Add(m, "main")` per batch item |
| `AIOutputAdapterBase.cs` (line ~510) | `this.PersistedMetrics = aggregatedMetrics` | `this.CombineIntoPersistedMetrics(m, "main")` per branch |
| `CombineIntoPersistedMetrics()` | `PersistedMetrics.Combine(metrics)` | `PersistedMetricsList.Add(metrics, role)` |
| `ConversationSession.cs` (multi-turn) | each turn calls `Combine` | each turn calls `Add(m, $"turn:{i}")` |
| Modality fallback `ApplyAsync` result | *(new)* `ExtraMetricsList` | `Add(m, $"fallback:{fallback.Name}")` |
| `img2text` tool in `AIFile2MdComponent` | *(currently lost)* | `Add(m, "tool:img2text")` |

The `img2text` entry in the last row is a bonus fix: currently the vision-model tokens used by
`AIFile2MdComponent` to describe images are not surfaced at all. With this refactor they
automatically appear as a labelled entry.

### M.7 Implementation steps for Phase M

1. `AIMetrics.Role` property (additive, `[JsonProperty(NullValueHandling.Ignore)]`).
2. `AIMetricsList` class in `Infrastructure.AICall.Metrics/`.
3. `BatchRunState.PersistedMetricsList` replaces `PersistedMetrics`; compatibility shim kept.
4. `CombineIntoPersistedMetrics(AIMetrics, role)` updated.
5. `AIMetrics.DataCount` and `AIMetrics.IterationsCount` new nullable properties;
   `Combine()` sums them; `SetMetricsOutput` serializes whatever is set on each entry
   (absent when null — `NullValueHandling.Ignore`).
6. `SetMetricsOutput()` — single-vs-list branch; per-entry JSON includes `data_count`/
   `iterations_count` only when the caller set them for that role.
8. Update all callers listed in §M.6 to pass `role` strings.
9. Update `AIOutputAdapterBase` branch-aggregation path.
10. Update `ConversationSession` multi-turn path.
11. Tests: single-entry serialization round-trip (plain object, no regression),
    multi-entry serialization (array, `data_count`/`iterations_count` on every entry),
    `DeconstructMetrics` with single-entry JSON (all list outputs have one item),
    `DeconstructMetrics` with multi-entry JSON (all list outputs have N items, same
    `data_count`/`iterations_count` value on each), `AIMetricsList.Add` merges same-provider entries.
12. CHANGELOG entry: list-form metrics, `Role` field, `data_count`/`iterations_count` on every
    entry, `DMetrics` all-pins list-access upgrade (breaking change callout).

---

## Design

### F.1 `IModalityFallback` — `SmartHopper.Infrastructure.AICall.Fallback`

```csharp
public interface IModalityFallback
{
    /// <summary>Modality this fallback can eliminate from a request (e.g. AudioInput).</summary>
    AICapability Handles { get; }

    /// <summary>Capability required to perform the conversion (e.g. vision model for ImageToText).</summary>
    AICapability RequiresCapability { get; }

    /// <summary>Capability produced after transformation (e.g. TextInput).</summary>
    AICapability ResultsIn { get; }

    /// <summary>Human-readable description for warnings, e.g. "audio transcribed via STT".</summary>
    string Description { get; }

    /// <summary>True if this fallback can run with the given provider (has a configured API key
    /// and has a model that satisfies RequiresCapability).</summary>
    bool IsAvailable(string providerName);

    /// <summary>Transforms the body, replacing unsupported interactions with supported ones.
    /// May perform extra AI calls (token cost). Returns the transformed body plus per-call metrics.
    /// The providerName passed is the one the fallback will use (may differ from the component's).</summary>
    Task<ModalityFallbackResult> ApplyAsync(AIBody body, string providerName, CancellationToken ct);
}

public sealed class ModalityFallbackResult
{
    public AIBody TransformedBody { get; set; }

    /// <summary>
    /// One entry per AI call made during the fallback transformation.
    /// Callers pass each entry to CombineIntoPersistedMetrics(m, role);
    /// AIMetricsList (Phase M) handles same-provider merging vs. new-entry
    /// appending automatically — no manual Case A / Case B needed.
    /// </summary>
    public List<AIMetrics> ExtraMetricsList { get; set; } = new();

    public List<SHRuntimeMessage> Messages { get; set; } = new();
}
```

### F.2 `ModalityFallbackResolver` — same namespace

```csharp
public sealed class ModalityFallbackResolver
{
    // Registry of known fallbacks (DI-free, static registration like ToolManager)
    public static void Register(IModalityFallback fallback);

    /// <summary>
    /// Finds a chain that converts unsupported capabilities in `required` into capabilities
    /// supported by the target provider/model.
    /// mode == ConfiguredProvider  → only chains where IsAvailable(providerName) is true
    /// mode == AnyProvider → chains where IsAvailable(any configured provider) is true;
    ///         sets FallbackChain.ActualProvider to the chosen alternate provider.
    /// Returns null if no chain can cover all missing capabilities.
    /// </summary>
    public FallbackChain Resolve(
        string providerName,
        string modelName,
        AICapability required,
        ModalityFallbackMode mode);
}

public sealed class FallbackChain
{
    public IReadOnlyList<IModalityFallback> Steps { get; }
    public string Description { get; }                 // joined step descriptions for the warning

    /// <summary>
    /// The provider that will actually execute the fallback call(s).
    /// Equals the component's configured provider unless mode is AnyProvider
    /// and a different provider was chosen.
    /// </summary>
    public string ActualProvider { get; }

    public bool UsesAltProvider => this.ActualProvider != null;   // set when a different provider is used
    public AICapability EffectiveCapability { get; }              // capability after all steps applied

    public Task<ModalityFallbackResult> ApplyAsync(AIBody body, CancellationToken ct);
}
```

**Resolution logic** (single-step chains only for v1):
1. Compute `missing = required & ~modelCapabilities`.
2. For each missing flag:
   - `ConfiguredProvider`: look up a registered fallback where `Handles == flag`,
     `IsAvailable(providerName)`, and `ResultsIn` is supported by the target model.
   - `AnyProvider`: same, but iterate all providers that have a configured API key
     (via `AIProviderManager`); pick the first one where `IsAvailable(p)` is true.
     Record the chosen provider as `ActualProvider`.
3. If every missing flag has a fallback → chain found. Otherwise → null.

### F.3 Concrete fallbacks (initial set)

| Class | Handles | ResultsIn | Mechanism |
|---|---|---|---|
| `ImageToTextFallback` | `ImageInput` | `TextInput` | Calls existing `img2text` tool against a vision-capable model/provider; replaces `AIInteractionImage` with `AIInteractionText` description |
| `AudioToTextFallback` | `AudioInput` | `TextInput` | Calls provider STT endpoint (`whisper-1` / `voxtral`); replaces `AIInteractionAudio` with transcript text |

*(Original plan's examples; both now implementable with existing tools/endpoints.)*

### F.4 Settings — `SmartHopperSettings`

#### F.4.1 Global mode setting (enum)

```csharp
/// <summary>
/// Controls whether and how modality fallback is applied when a provider/model
/// does not support a required input capability.
/// </summary>
public enum ModalityFallbackMode
{
    /// <summary>No fallback; unsupported modalities produce a hard error (default).</summary>
    Disabled = 0,

    /// <summary>Convert the unsupported modality using the component's configured provider/model.
    /// If that provider also lacks the conversion capability, falls back to a hard error.</summary>
    ConfiguredProvider = 1,

    /// <summary>Convert using whichever configured provider can perform the conversion.
    /// Token costs and the provider used are reported per-branch in the Metrics output.
    /// If no configured provider can handle the conversion, falls back to a hard error.</summary>
    AnyProvider = 2,
}

[JsonProperty]
public ModalityFallbackMode ModalityFallback { get; set; } = ModalityFallbackMode.Disabled;
```

- Add a **dropdown** in `ProvidersSettingsPage` (or a dedicated "Fallback" settings page)
  labelled "Modality fallback" with the three options above and a brief one-line description
  of each, matching the `ModalityFallbackMode` enum values.
- Default serialized value is `0` (`Disabled`) — zero-cost default, no behaviour change for
  existing installations.

#### F.4.2 Per-fallback provider/model pinning

For each registered `IModalityFallback`, the user can optionally pin the exact provider and
model used for that conversion. This is especially relevant in `AnyProvider` mode
where automatic selection might pick an unexpected or expensive provider.

**Data structure:**

```csharp
/// <summary>
/// Pinned provider/model overrides for specific modality fallback conversions.
/// Keyed by IModalityFallback.Name (e.g. "ImageToText", "AudioToText").
/// Null or missing entries → automatic selection per ModalityFallbackMode logic.
/// </summary>
[JsonProperty]
public Dictionary<string, FallbackProviderPin> FallbackProviderPins { get; set; } = new();

public sealed class FallbackProviderPin
{
    [JsonProperty] public string Provider { get; set; }   // null = auto
    [JsonProperty] public string Model { get; set; }      // null = provider default for RequiresCapability
}
```

`IModalityFallback` gains a `string Name { get; }` property (e.g. `"ImageToText"`) used as
the dictionary key. This is stable and serialization-safe.

**Settings UI:**

Below the mode dropdown, add a collapsible sub-section "Fallback model overrides" that shows
one row per registered fallback (only visible when mode ≠ `Disabled`). Each row contains:

- A label: fallback `Description` (e.g. "Image → text via vision model")
- Provider dropdown: `"Auto"` + all providers that have a configured API key and satisfy
  `fallback.IsAvailable(provider)` — populated at render time from
  `AIModelCapabilityRegistry.FindModelsWithCapabilities(fallback.RequiresCapability)`
  filtered to providers present in `SmartHopperSettings.ProviderSettings`.
- Model dropdown (enabled only when provider ≠ Auto): models for the chosen provider that
  satisfy `fallback.RequiresCapability`, sourced from
  `AIModelCapabilityRegistry.FindModelsWithCapabilities(fallback.RequiresCapability)`
  filtered to `m.Provider == selectedProvider`.

When provider is `"Auto"`, the `FallbackProviderPin` entry is removed (or set to null values)
so the resolver uses its automatic strategy.

**Resolution logic update (F.2):**

Before the automatic provider search, the resolver checks `FallbackProviderPins[fallback.Name]`:
- If a pin exists with a non-null `Provider`:
  - Verify `fallback.IsAvailable(pin.Provider)`. If not → hard error with a clear message
    "Pinned provider '{pin.Provider}' cannot perform {fallback.Name} conversion."
  - Use `pin.Model ?? ModelManager.SelectBestModel(pin.Provider, null, fallback.RequiresCapability)`
    as the `ActualModel` on the chain.
  - Record `ActualProvider = pin.Provider`, `ActualModel = pin.Model` (or resolved default).
- If no pin → proceed with automatic selection as before.

**`FallbackChain` gains `ActualModel`:**

```csharp
public string ActualProvider { get; }   // provider executing the fallback
public string ActualModel { get; }      // model executing the fallback (resolved, never null when chain is non-null)
```

`ApplyAsync` uses `ActualProvider`/`ActualModel` explicitly when calling the provider,
bypassing `GetDefaultModel` to avoid double-selection.

#### F.4.3 Per-component override via `Extras`

**Defer the per-component `AISettings` override** to a follow-up: the `AISettingsComponent`
parameter list is breaking-change-sensitive (see the `Timeout`/`Extras` index-shift incident
in the changelog). A string extras key (`"modality_fallback": "disabled"|"configured_provider"|"any_configured_provider"`) routed through the existing `Extras` JSON input avoids parameter insertion entirely — use that instead of a new input. The per-component value, when present, overrides the global mode for that component only. Provider/model pins always come from global settings (no per-component pin override in v1).

### F.5 Validator extension — `ComponentCapabilityValidator`

Extend `ValidateSync` (current hard-error step 3). The validator receives the effective
`ModalityFallbackMode` (already resolved from global setting + per-component `Extras` override):

```
supportsCapability == false:
  1. mode = effective ModalityFallbackMode

  2. if mode == Disabled:
        → IsValid = false (current behavior — hard error)

  3. chain = ModalityFallbackResolver.Resolve(provider, model, capability, mode)
        // mode == ConfiguredProvider  → only chains using the same provider are considered
        // mode == AnyProvider → chains using any provider with a configured key are considered

  4. if chain == null:
        → IsValid = false, Error message notes that fallback is enabled but no chain exists
          (capability gap the user must resolve by choosing a capable provider/model)

  5. if chain != null:
        → IsValid = true (execution continues), add Warning message:
          "[Fallback] {chain.Description}. Extra tokens will be consumed."
        → store chain on ValidationResult for the worker to apply

  6. if mode == AnyProvider && chain.UsesAltProvider:
        → add additional Info message naming the alternate provider that will be used
```

Add to `ValidationResult` (non-breaking, additive):

```csharp
public string FallbackDescription { get; set; }    // null when no fallback applies
public FallbackChain FallbackChain { get; set; }   // non-null when IsValid and fallback applies
public bool HasWarnings => this.WarningCount > 0;  // convenience per original plan
```

### F.6 Wiring into `AIOutputAdapterBase`

> **Prerequisite:** Phase M (`AIMetricsList`) must be complete before this step.
> The Case A / Case B distinction from earlier drafts is eliminated — both cases are now
> handled uniformly by `AIMetricsList.Add(metrics, role)`.

#### Hook 1 — Pre-validation (sync, badge level)

In `SolveInstance` before dispatching the worker: resolve the effective `ModalityFallbackMode`
(global setting overridden by per-component `Extras` key if present), then run
`ValidateSync(RequiredCapability, mode)`. On warning (fallback resolved), emit
`SetPersistentRuntimeMessage("fallback_warning", GH_RuntimeMessageLevel.Warning, chain.Description + " — extra tokens will be consumed.")`.
On error, stop (red badge). This is the user's main visibility point before any token is spent.

#### Hook 2 — Application (async, worker level)

In `ProcessBranchAsync`, after `PrepareInputs` builds the merged `AIBody` and the resolved
`FallbackChain` is non-null: call `chain.ApplyAsync(body, ct)` to transform the body before
`CallAIAsync`. For each entry in `result.ExtraMetricsList`:

```csharp
foreach (var m in result.ExtraMetricsList)
    this.CombineIntoPersistedMetrics(m, role: $"fallback:{fallback.Name}");
```

`AIMetricsList.Add` handles same-provider merging vs. new-entry appending automatically
(Phase M §M.3). The component's main-call metrics are added with `role: "main"` via the
existing path. `SetMetricsOutput` then emits a single `JObject` if `IsSingleProvider`, or a
`JArray` if multiple providers appear — entirely driven by Phase M §M.4.

#### Batch caveat

Fallback transformation performs synchronous AI calls, which conflicts with batch queuing.
For v1, when batch mode is active and a fallback is required, emit a hard error:
"Modality fallback is not supported in batch mode. Run without batch mode to use fallback."
(mirrors the existing `img2text` batching restriction in `AIFile2MdComponent`).

---

## Implementation Steps

### Phase M — Multi-Provider Metrics List (prerequisite, do first)

See §M.7 for the detailed sub-steps. Summary:

M1. `AIMetrics.Role` property.
M2. `AIMetricsList` class.
M3. `BatchRunState.PersistedMetricsList`; backwards-compat shim for `PersistedMetrics`.
M4. `CombineIntoPersistedMetrics(metrics, role)` updated.
M5. `SetMetricsOutput()` single-vs-array branch.
M6. `DeconstructMetricsComponent` — input stays item, outputs upgrade to list, add `Role` pin.
M7. Update all callers (batch aggregation, branch aggregation, `ConversationSession`, `AIFile2MdComponent`).
M8. Phase M tests + CHANGELOG.

### Phase 7 — Modality Fallback Resolver (depends on Phase M)

1. `Infrastructure.AICall.Fallback/` — `ModalityFallbackMode` enum, `IModalityFallback`
   (with `Name`, `RequiresCapability`, `Handles`, `ResultsIn`, `Description`,
   `IsAvailable`, `ApplyAsync`), `ModalityFallbackResult` (with `List<AIMetrics>
   ExtraMetricsList`), `FallbackChain` (with `ActualProvider`, `ActualModel`,
   `UsesAltProvider`), `ModalityFallbackResolver` (static registry, three-mode
   resolution, pin lookup).
2. `SmartHopperSettings`:
   - `ModalityFallback` enum property + settings UI **dropdown** (three options, 0–2).
   - `FallbackProviderPins` dictionary + `FallbackProviderPin` record.
   - Settings UI "Fallback model overrides" sub-section: one row per registered fallback,
     provider dropdown (Auto + capable providers), model dropdown (capable models for chosen
     provider); uses `AIModelCapabilityRegistry.FindModelsWithCapabilities` for population.
3. Extend `ValidationResult` (`FallbackDescription`, `FallbackChain`, `HasWarnings`) and
   `ComponentCapabilityValidator.ValidateSync` (three-mode logic: disabled→error,
   resolved→warning+chain stored, unresolvable→error; pin verification produces clear error
   if pinned provider is unavailable).
4. `ImageToTextFallback` (`Name = "ImageToText"`, reuses `img2text` tool); register at
   Infrastructure load.
5. `AudioToTextFallback` (`Name = "AudioToText"`, reuses provider STT endpoint); register at
   Infrastructure load.
6. Wire pre-validation warning into `AIOutputAdapterBase.SolveInstance`.
7. Wire `chain.ApplyAsync(body, ct)` into `ProcessBranchAsync`; call
   `CombineIntoPersistedMetrics(m, $"fallback:{fallback.Name}")` for each entry in
   `ExtraMetricsList` — Phase M handles the rest.
8. Block fallback in batch mode with a clear error.
9. `Extras`-routed per-component mode override
   (`"modality_fallback": "disabled"|"configured_provider"|"any_provider"`).
10. Tests: resolver mode matrix (disabled/configured/any × pin present/absent/invalid),
    validator three-way branch, pin verification error, image→text round-trip (same provider
    + alt provider + pinned), metrics list output, batch-mode rejection.
11. CHANGELOG + `docs/` entry (dropdown options, per-fallback model override UI, token cost
    transparency, per-component mode override via `Extras`).

## Verification

### Phase M checks

- [ ] `AIMetricsList.Add` — same provider+model entries are merged (Combine); different
      provider+model entries are appended as separate entries.
- [ ] `SetMetricsOutput` — single-entry list → plain `JObject` (no regression);
      multi-entry list → `JArray` with per-role `data_count`/`iterations_count` (absent when
      not set for a given role).
- [ ] `DeconstructMetricsComponent` with single-object JSON input → all output lists have
      one item each, including `Data Count` and `Iterations Count`.
- [ ] `DeconstructMetricsComponent` with 2-entry array JSON input (main + img2text) → all
      list outputs have two items; `Data Count` list contains `[1, 3]` (not the same value).
- [ ] `AIFile2MdComponent` — `img2text` vision metrics appear as a second list entry
      with `role = "tool:img2text"` and their own `data_count` / `iterations_count`
      reflecting the number of images described, independent of the main call's count.
- [ ] Existing GH test files that wire `DMetrics` item-access outputs still connect (list
      access is backwards-compatible for single-item lists in Grasshopper).

### Phase 7 checks

- [ ] `ModalityFallbackResolver.Resolve` covering all modes × pin states:
      - `Disabled` → always null.
      - `ConfiguredProvider`, provider capable → chain; `ActualProvider` = configured provider.
      - `ConfiguredProvider`, provider incapable → null.
      - `AnyProvider`, alternate capable → chain; `ActualProvider` set.
      - `AnyProvider`, no provider capable → null.
      - Pin present, provider capable → `ActualProvider = pin.Provider`, `ActualModel = pin.Model`.
      - Pin present, provider incapable → hard error (not silent null).
- [ ] `ComponentCapabilityValidator.ValidateSync` three-way: disabled→error,
      enabled+chain→warning, enabled+no chain→error.
- [ ] Manual GH test: `Img2AI` → `AI2Text` with text-only model (`deepseek-chat`):
      - `Disabled`: red error.
      - `ConfiguredProvider`: red error (DeepSeek has no vision).
      - `AnyProvider` (OpenAI also configured, no pin): yellow warning; `Metrics`
        pin is a 2-entry array (`deepseek` main + `openai` fallback).
      - `AnyProvider` + pin `ImageToText → openai / gpt-4o`: `ActualModel = gpt-4o`.
      - `AnyProvider` + pin `ImageToText → deepseek` (incapable): red error.
- [ ] Settings UI: provider dropdown for `ImageToText` row only lists vision-capable providers;
      model dropdown updates dynamically when provider selection changes.
- [ ] Batch mode + fallback → clear error, no silent queue corruption.
- [ ] Build + existing test suite passes.

## Risks / Considerations

### Phase M risks

- **`DMetrics` output-access upgrade is a breaking change.** Any GH canvas that wires
  any `DMetrics` output pin (including `Data Count` and `Iterations Count`) into a component
  expecting a scalar will now receive a list. Mitigation: prominent CHANGELOG warning;
  recommend users add a `List Item` component to extract the first element if they only care
  about the main call.
- **`ConversationSession` multi-turn metrics** currently calls `Combine` once per turn,
  giving a running total with the last turn's provider/model. After Phase M, each turn is
  a separate entry with `role = "turn:N"`. For long conversations this list can grow large —
  consider a `MaxMetricsEntries` cap (default: unlimited; configurable) that triggers a
  fallback to the aggregate-only form beyond the cap.

### Phase 7 risks

- **Token cost transparency** is the whole point: every fallback call must appear in the
  `Metrics` output; never swallow `ExtraMetricsList` entries.
- **Recursion guard:** a fallback's own AI call must not itself trigger fallback resolution
  (pass `noFallback: true` or call provider endpoints directly, bypassing the validator).
- **Provider discovery for `AnyProvider`:** the resolver must only consider providers that
  have a configured and non-empty API key, not all registered providers. Use the existing
  `SmartHopperSettings.GetProviderSettings` + `IsAvailable` check.
- **Pin validation at settings save time:** when the user saves a `FallbackProviderPin`,
  validate immediately that `fallback.IsAvailable(pin.Provider)` is true and surface a
  settings-level warning if not. The settings dialog already has a validation pattern.
- **`ContextUsagePercent` per entry:** each `AIMetricsList` entry computes its own
  `contextUsagePercent` from its own `provider`/`model` — no cross-entry ambiguity.
- **`ModelManager.SelectBestModel` silent model reassignment** — today, when the user's
  chosen model is incapable, `SelectBestModel` silently picks a different one (only
  `Debug.WriteLine`). This is a separate axis from modality fallback but causes the same
  "surprise token spend" UX problem. Planned as a **separate follow-up task**: add an
  optional `Action<string, string> onFallback` callback parameter to `SelectBestModel` so
  callers (e.g. `AIProvider.GetModel`) can emit an `SHRuntimeMessage(Info, Validation, ...)`
  of the form "Model '{requested}' does not support {capability}; using '{selected}' instead."
  This is intentionally scoped out of Phase 7 to keep it focused, but should be filed as a
  tracked issue immediately so it is not forgotten.
