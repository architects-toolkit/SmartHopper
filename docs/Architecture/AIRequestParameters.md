# AIRequestParameters

Immutable per-request AI configuration controlling model selection, temperature, token limits, batch processing, timeout, and provider-specific extras.

- File: `src/SmartHopper.Infrastructure/AICall/Core/AIRequestParameters.cs`
- **Immutable record** — once constructed, cannot be modified directly
- Central to AI call customization; read by providers with fallback precedence: `AIRequestParameters` → global provider settings → provider defaults

## Core Fields

### Model Selection

- `Model` (string, nullable) — model override. Null means "use provider default". Allows per-request model switching without changing global settings.

### Sampling and Generation

- `Temperature` (double?, 0.0–2.0) — temperature override for sampling. Null means "use global setting". Controls randomness/creativity of responses.
- `TopP` (double?, 0.0–1.0) — nucleus sampling parameter. Null means omit. Filters tokens by cumulative probability.
- `Seed` (int?, nullable) — seed for reproducibility. Null means omit. Enables deterministic outputs when supported by provider.
- `MaxTokens` (int?, nullable) — max tokens override. Null means "use global setting". Limits response length.

### Timeout and Batch Processing

- `TimeoutSeconds` (int?, nullable) — timeout override in seconds. Null means "resolve from settings based on operation type". Enforced by `RequestTimeoutPolicy`.
- `BatchTier` (bool) — whether to use asynchronous batch processing. When `true`, all tool calls in a single component run are aggregated into one batch HTTP request and submitted via `IAIBatchProvider`. Only effective if the active provider implements `IAIBatchProvider`.

### Provider-Specific Extras

- `Extras` (IReadOnlyDictionary<string, JToken>, nullable) — provider-specific extra parameters as key/value pairs serialized to JSON. Well-known keys:
  - `"reasoning_effort"` — OpenAI o-series/gpt-5 reasoning level
  - `"safe_prompt"` — MistralAI safety flag
  - `"top_k"` — Anthropic top-k sampling
  - `"allow_fallback"` — OpenRouter fallback behavior
  - `"sort"` — OpenRouter sorting
  - `"presence_penalty"` / `"frequency_penalty"` — OpenAI/DeepSeek penalty parameters
  - `"parallel_tool_calls"` — OpenAI parallel tool execution

## Factory Methods

### Static Factories

- `Empty` (static property) — empty (default) instance with no overrides
- `Create()` — static factory returning a new `AIRequestParametersBuilder`
- `FromModel(string model)` — convenience factory creating parameters with just a model name; useful for backwards-compatible single-string wires

## AIRequestParametersBuilder

Fluent builder for constructing `AIRequestParameters`.

### Builder Methods

#### Model and Sampling

- `WithModel(string model)` — set the model override
- `WithTemperature(double? temperature)` — set the temperature override
- `WithTopP(double? topP)` — set the top-p override
- `WithSeed(int? seed)` — set the seed for reproducibility
- `WithMaxTokens(int? maxTokens)` — set the max tokens override

#### Timeout and Batch

- `WithTimeout(int? timeoutSeconds)` — set the timeout override in seconds
- `WithBatchTier(bool batchTier)` — set batch processing flag

#### Extras Management

- `WithExtra(string key, JToken value)` — add or overwrite a single extra parameter
- `WithExtras(IReadOnlyDictionary<string, JToken> extras)` — merge a dictionary of extra parameters (last write wins)
- `AddExtra(string key, JToken value)` — alias for `WithExtra()`
- `AddExtras(IReadOnlyDictionary<string, JToken> extras)` — alias for `WithExtras()`
- `RemoveExtra(string key)` — remove an extra parameter by key

#### Clear Methods

- `ClearModel()` — clear the model override
- `ClearTemperature()` — clear the temperature override
- `ClearMaxTokens()` — clear the max tokens override
- `ClearTimeout()` — clear the timeout override
- `ClearTopP()` — clear the top-p override
- `ClearSeed()` — clear the seed override
- `ClearExtras()` — clear all extra parameters

#### Finalization

- `Build()` — produce the immutable `AIRequestParameters` record

### Builder Workflow

```csharp
var parameters = AIRequestParameters.Create()
    .WithModel("gpt-4")
    .WithTemperature(0.7)
    .WithMaxTokens(2000)
    .WithTimeout(30)
    .WithExtra("reasoning_effort", JToken.FromObject("high"))
    .Build();
```

## Grasshopper Wrapper (GH_AIRequestParameters)

- File: `src/SmartHopper.Core.Grasshopper/AICall/GH_AIRequestParameters.cs`
- Grasshopper component wrapper exposing `AIRequestParameters` as input parameters
- Provides UI-friendly parameter inputs for Grasshopper users
- Converts Grasshopper inputs to immutable `AIRequestParameters` for infrastructure use

## Usage Context

1. **Per-Request Customization**: Pass `AIRequestParameters` to `AIRequestCall` to override global settings for a single call
2. **Provider Encoding**: Providers read each property individually with fallback precedence
3. **Policy Application**: `RequestTimeoutPolicy` and other policies read and normalize parameters
4. **Tool Execution**: Tool wrappers may pass parameters to nested AI calls

## Safety and Performance Notes

- **Immutability**: Once constructed, parameters cannot be modified; use builder to create new instances
- **Null Handling**: Null values mean "use default"; empty string/zero are valid overrides
- **Extras Validation**: Provider-specific extras are passed as-is; validate in provider encoding layer
- **Timeout Bounds**: Enforced by `RequestTimeoutPolicy` to safe range (min/max from `TimeoutDefaults`)
