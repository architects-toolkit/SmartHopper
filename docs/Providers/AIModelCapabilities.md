# AI Model Capabilities

This document describes `AIModelCapabilities` and how SmartHopper uses it to drive model selection and badges.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.ProviderSdk/AIModels/AIModelCapabilities.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-08-28 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

`AIModelCapabilities` is the central data structure that tells SmartHopper what each model can do, which model to pick by default, and how to surface compatibility warnings. If you are adding or tuning model support, this is the document to read.

**You should read this if you:**

- Are adding a new model to a provider and need to declare its capabilities
- Want to understand how SmartHopper chooses the default model for a given task
- Need to mark a model as verified, deprecated, or discouraged for specific tools

---

## End-User Guide

### Fields

- **Provider**
  Provider id (e.g., "openai", "mistralai").
  Normalized to lower-case by `AIModelCapabilityRegistry` when registering capabilities.

- **Model**
  Concrete API-ready model name (e.g., "gpt-4o-mini", "mistral-small-latest").

- **Capabilities**
  `AICapability` bitmask of what the model can do
  (for example `Text2Text`, `ToolChat`, `Text2Json`, `Text2Image`, etc.).

- **Default**
  `AICapability` bitmask of the capabilities this model is the default for.
  A model can be capable of multiple things but be default only for a subset.

- **Verified**
  Boolean; model is verified to work end-to-end in SmartHopper.

- **Deprecated**
  Boolean; avoid unless selected explicitly or no better option exists.

- **Rank**
  Integer tie-breaker; higher is preferred after `Verified` / `Deprecated` filters.

- **Aliases**
  List of alternative names resolving to this model.
  Lookups in `AIModelCapabilityRegistry` use the exact model name or any alias.

- **SupportsStreaming**
  Bool; provider supports streaming with this model.

- **SupportsPromptCaching**
  Bool; provider supports prompt caching with this model.

- **SupportsBatch**
  `bool?` (nullable). When `true`, the model can be used with the provider's batch API for discounted, asynchronous multi-item processing. When `false` or `null`, batch requests are blocked with a runtime diagnostic.

- **BatchPricing**
  Optional `AIModelPricing` with discounted prompt/completion costs used when the model is executed through a batch provider.

- **CacheKeyStrategy**
  Optional provider-defined cache key hint/strategy name.

- **DiscouragedForTools**
  List of AI tool names for which this model is discouraged.
  Used by `ComponentBadgesAttributes` to show a "not recommended" badge when
  components that use those tools select this model.

### Best practices

- **Defaults per capability**
  Mark at most one default per capability per provider.
  Use `AIModelCapabilityRegistry.SetDefault(...)` with `exclusive = true` to enforce this.

- **Concrete models only**
  Keep `Model` concrete (no wildcards). Use `Aliases` for alternative names;
  lookup and selection use exact/alias matching only.

- **Ranking and metadata**
  Use `Verified`, `Deprecated`, and `Rank` sparingly but deliberately.
  `AIModelCapabilityRegistry.SelectBestModel(...)` uses these to choose between candidates
  that satisfy the required capability.

- **Tool-specific guidance**
  Use `DiscouragedForTools` only when a model is technically compatible but
  known to be sub-optimal for a particular tool (for example poor JSON
  structure for `list_generate`). This does not block the model; it only
  surfaces a warning badge.

### Examples

- **Text-only default**
  - Capabilities: `Text2Text | ToolChat`
  - Default: `Text2Text`

- **Tool-call default**
  - Capabilities: `Text2Text | ToolChat`
  - Default: `ToolChat`

### See also

- Model selection policy: `ModelSelection.md`
- Manager API: `ProviderManager.md`, `AIProviderModels.md`
- Badges and UI integration: `../Components/Helpers/AIComponentAttributes.md`

---

## Developer Reference

`AIModelCapabilities` is typically populated by a provider's `AIProviderModels` implementation and registered with `AIModelCapabilityRegistry` during initialization.

```csharp
// Declaring capabilities for a model
var capabilities = new AIModelCapabilities
{
    Provider = "openai",
    Model = "gpt-4o-mini",
    Capabilities = AICapability.Text2Text | AICapability.ToolChat | AICapability.Text2Json,
    Default = AICapability.Text2Text,
    Verified = true,
    Deprecated = false,
    Rank = 100,
    Aliases = new List<string> { "gpt-4o-mini-2024-07-18" },
    SupportsStreaming = true,
    SupportsPromptCaching = false,
    SupportsBatch = true,
    BatchPricing = new AIModelPricing { Prompt = 0.000001m, Completion = 0.000002m },
    DiscouragedForTools = new List<string>()
};

AIModelCapabilityRegistry.RegisterCapabilities(capabilities);

```

```csharp
// Registering a default with exclusivity
AIModelCapabilityRegistry.SetDefault(
    provider: "openai",
    model: "gpt-4o-mini",
    caps: AICapability.Text2Text,
    exclusive: true);

```

---

## Architecture & Design

`AIModelCapabilities` acts as the bridge between provider-specific model knowledge and SmartHopper's centralized model selection policy. Each provider declares what it knows; `AIModelCapabilityRegistry` consumes these declarations as the single source of truth.

The design uses bitmasks for capabilities and defaults so that a single model can serve multiple roles, while still having a clear default for each role. The `Verified`, `Deprecated`, and `Rank` fields create a simple scoring system that `AIModelCapabilityRegistry.SelectBestModel` uses when multiple models satisfy the requested capability.

`DiscouragedForTools` is intentionally non-blocking: it feeds UI badges rather than hard restrictions, keeping the system flexible while still guiding users toward better choices.

---

## Remote catalogue layer (`smarthopper-models/1`)

Provider `AIProviderModels` seed lists are still shipped in the assembly, but at startup `ModelCatalogueService` (in `SmartHopper.ProviderSdk.Catalogue`) may overlay fresher metadata fetched from `https://models.smarthopper.xyz/api`:

- The service serves **signed** artefacts (`catalogue.json`, `keys.json` and `manifest.json`, each + detached ES256 signature over canonical JSON). The plugin verifies signatures **before parsing**, then strictly re-validates every field (closed property sets, bounded identifiers, decimal-string money, no URL/HTML-capable fields). The manifest signature is verified before `latest_version`, `key_id` or artefact paths are trusted — so a path attacker cannot pin clients to an old release; a *missing* `manifest.sig` is tolerated during rollout (hint-only manifest) while an *invalid* one fails closed.
- A verified copy is cached atomically under `%APPDATA%/Grasshopper/SmartHopper/models-catalogue.{json,sig,meta}` and reapplied on later starts when the network is unavailable or the remote serves no newer `catalogue_version`. The cached signature is re-verified on every load — the directory is treated as untrusted input, so a tampered cache is ignored and the plugin falls back to seed/unknown-model behavior (same as having no cache). The cache is a local mirror of the remote signed artefact, never a source of truth: the git-managed artefact store in `smarthopper-models` is.
- A persisted version floor (`models-catalogue.floor`) records the highest `catalogue_version` ever accepted and ratchets only upward. It survives cache deletion/corruption, so replayed older-but-validly-signed releases are rejected after the first successful fetch — remote documents at or below the floor fail closed, and a cached artefact below it is ignored. The floor is user-writable local state: it defends against network-path replay, not a local attacker (who could patch the plugin anyway). Rollback of a bad release means publishing a higher version containing the fix.
- The catalogue is the **source of truth for the providers it covers**: entries win over embedded seeds for the same `provider.model` key, and every registry model absent from the document is **removed** — whether it shadowed a catalogue-declared alias (folded to the canonical model) or was dropped from the catalogue by review (treated as unknown, not deprecated-and-callable). Deprecated models stay visible because the document itself carries them with `deprecated = true`. Providers the document does not cover (Ollama, LocalAI, …) keep their own tables untouched.
- Fetches are retried on transient failures — network errors, timeouts, and 408/429/5xx responses get up to 3 attempts with exponential backoff; other failures fail fast. A non-success result is always treated as "unreachable", never as data.
- Provider re-registration (the **Refresh Providers** menu action) repopulates the registry from embedded seed tables; `SmartHopperInitializer.Reinitialize` therefore re-runs the catalogue refresh with `force: true` so the verified document — remote or signed cache — keeps authority.
- A manual refresh lives in the SmartHopper menu: **Refresh Model Catalogue** (between *Refresh Providers* and *Verify Providers Hash*) opens a dialog that streams the service's progress log and reports the applied source — remote, verified cache, or built-in seed — plus descriptive errors.
- Every failure path is silent for AI calls: signature mismatch, malformed JSON, schema violations, HTTP errors, or a missing cache leave the registry usable — unknown models behave exactly as they did before.

Signing keys rotate through a signed keyset (`keys.json` + `keys.sig`): new keys are trusted only when signed by an already-trusted key. Local development can inject trust anchors via `SMARTHOPPER_CATALOGUE_KEYS` (`keyid=base64spki;...`); production embeds the key in `ModelCatalogueTrust.EmbeddedKeys`.
