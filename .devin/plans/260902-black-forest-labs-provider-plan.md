---
agent: devin-local
session: kind-line
created: 2026-09-02T19:28:51Z
---
# Implement Black Forest Labs (BFL) as an image-first SmartHopper provider

Add a new SmartHopper.Providers.Bfl provider that exposes the curated FLUX.2 text-to-image endpoints, handles BFL's async submit-and-poll API inside a new AIProvider.ExecuteCall hook, supports provider-specific generation extras, and introduces per-megapixel image pricing while reusing the existing text2img tool and AIText2ImgComponent.

# Plan: Black Forest Labs (BFL) image-first provider

## 1. Objective

Create a first-class BFL provider (`SmartHopper.Providers.Bfl`) that plugs into the existing SmartHopper image generation pipeline:

- Reuse the central `text2img` AI tool and `AIText2ImgComponent`.
- Support the curated FLUX.2 text-to-image endpoints with BFL's async submit-and-poll flow.
- Let users control BFL-specific generation parameters through `AIExtraSettingsComponent`.
- Model BFL's credit/megapixel pricing in `AIModelPricing` and `AICostCalculator`.
- Keep the polling logic as a localized quirk inside `BflProvider.ExecuteCall`, using a new `AIProvider.ExecuteCall` hook so `AIProvider.Call` stays non-virtual and owns the full lifecycle.

## 2. Acceptance criteria

- `SmartHopper.Providers.Bfl` builds for `net7.0` and `net7.0-windows` and is discovered by `ProviderManager`.
- BFL appears in the provider list and can be selected on `AIText2ImgComponent` and `AISettingsComponent`.
- `AIText2ImgComponent` with BFL selected generates an image from a prompt and returns a `GH_VersatileImage`.
- BFL's `submit → polling_url → Ready` flow works without manual user intervention.
- BFL errors (`Error`, `Failed`, `Request Moderated`, `Content Moderated`, `429`, `402`) are surfaced as runtime messages.
- Provider extras (`seed`, `safety_tolerance`, `output_format`, `prompt_upsampling`, `steps`, `guidance`, `webhook_url`) flow from `AIExtraSettingsComponent` → `AISettingsComponent` → `text2img` tool → `BflProvider.Encode`.
- `text2img` tool forwards `AIRequestParameters` and `aspect_ratio` into the `AIRequestCall`.
- `AIModelPricing` and `AICostCalculator` support per-image base price plus per-megapixel pricing.
- `BflProviderModels` registers the curated FLUX.2 catalog with capabilities and BFL-published megapixel pricing.
- `AIProvider.Call` stays non-virtual; a new `protected virtual Task<IAIReturn> ExecuteCall` is added and overridden by `BflProvider`.
- Provider SDK unit tests cover `BflProvider.Encode`, `BflProvider.Decode`, `BflProvider.ExecuteCall` polling, and the `ExecuteCall` hook (no Rhino/Grasshopper references).
- `CHANGELOG.md` and `docs/Providers/Bfl.md` are updated under `[Unreleased]`. New files are documented; existing architecture docs are updated.

## 3. Scope

### In scope (phase 1)

- New `SmartHopper.Providers.Bfl` project, added to `SmartHopper.sln`.
- `BflProvider`, `BflProviderFactory`, `BflProviderSettings`, `BflProviderModels`.
- Curated FLUX.2 text-to-image models:
  - `flux-2-pro-preview`
  - `flux-2-pro`
  - `flux-2-max`
  - `flux-2-flex`
  - `flux-2-klein-4b`
  - `flux-2-klein-9b-preview`
  - `flux-2-klein-9b`
  - `flux-kontext-pro` (text-to-image only)
- Provider-specific extras exposed through `BflProvider.GetExtraDescriptors()`:
  - `output_format` ("jpeg", "png")
  - `seed` (string, empty = random)
  - `safety_tolerance` (int, 0–6)
  - `prompt_upsampling` (bool)
  - `steps` (int, 1–50, only meaningful for `flex`)
  - `guidance` (double, 1.5–10, only meaningful for `flex`)
  - `webhook_url` (string)
- Async submit-and-poll flow localized in `BflProvider.ExecuteCall` via the new `AIProvider.ExecuteCall` hook.
- SDK pricing extensions for per-megapixel cost.
- `text2img` tool fixes to pass `AIRequestParameters` and `aspect_ratio`.
- Documentation and changelog updates.

### Out of scope (phase 2 or later)

- Image-to-image / multi-reference editing (`input_image`, `input_image_2-8`).
- Video generation (FLUX 3).
- BFL MCP integration.
- Webhook delivery consumption / server-side async notification handling.
- Batch API support for BFL.
- Model auto-discovery from a live BFL `/models` endpoint (does not exist today).
- Converting `AI2ImgComponent` to use the `text2img` tool when the provider is image-only.
- Generalizing the async single-call pattern beyond BFL (e.g., a future `IAIAsyncCallProvider` interface). The `ExecuteCall` hook is intentionally the extension point, not a public async-call contract.

## 4. Constraints and assumptions

- Polling is a one-off provider quirk inside `BflProvider.ExecuteCall`.
- `AIProvider.Call` stays non-virtual. A new `protected virtual Task<IAIReturn> ExecuteCall` is the hook for the actual HTTP execution.
- `AIProvider.Call` continues to own the lifecycle: `PreCall` → `IsConfigured` → `IsValid` → `ExecuteCall` → `SetCompletionTime` → `PostCall`.
- BFL uses the `x-key` header for API-key authentication (not `Authorization: Bearer`).
- BFL has no `/models` endpoint, so model metadata is hardcoded.
- `text2img` is the single central AI tool for image generation; component-level parameter count must not explode.
- Provider-specific extras are the mechanism for BFL generation parameters (no new `AIImageExtraSettingsComponent` in phase 1).
- Image URL lifetime: BFL `result.sample` URLs expire after 10 minutes. SmartHopper `VersatileImage` can load from a URL, so the user must recompute if the URL expires. Phase 1 does not download and cache the image inside the provider.
- New provider project follows existing conventions: `SmartHopper.Providers.*` naming, `ProviderSdk` reference only, `Resources.resx` for icon, `using` directives sorted (System first, then others alphabetically).
- `AI2ImgComponent` uses `CallAIAsync` and expects a chat-style provider that can emit an image. It is not BFL-compatible in phase 1.

## 5. Image-related UX and WebChat

### Central image-generation surface

- **Primary component**: `AIText2ImgComponent` (`src/SmartHopper.Components/Img/AIText2ImgComponent.cs`) is the central, provider-agnostic image generator.
  - Basic inputs: `Prompt`, `Size`, `Quality`, `Style`, `Aspect Ratio`.
  - `Settings` input carries `AIRequestParameters` (model, extras from `AISettingsComponent`).
  - Uses the `text2img` AI tool.
- **Provider extras**: `AIExtraSettingsComponent` is the existing equivalent the user asked for. When provider is `BFL`, it exposes seed, output format, safety tolerance, etc. The JSON output connects to `AISettingsComponent.Extras`.
- **No new image-only extras component in phase 1**: the existing `AIExtraSettingsComponent` + `AISettingsComponent` pattern already carries provider-specific extras. A future `AIImageExtraSettingsComponent` can be added if image-common extras (e.g., seed, output format) need to be shared across image providers, but this is out of scope.

### Component compatibility

- `AIText2ImgComponent`: fully BFL-compatible.
- `AI2ImgComponent`: **not BFL-compatible** because it uses `CallAIAsync` (chat/responses completion). It works with OpenAI/Gemini image-capable chat models but not with BFL's standalone image endpoints. No change in phase 1.
- `AISettingsComponent`: no change. It already accepts `Extras` JSON and `Model` override.

### `text2img` tool as the provider-agnostic bridge

The tool is the single chokepoint where all image components converge:

1. Read `prompt`, `size`, `quality`, `style`, `aspect_ratio` from tool arguments.
2. Build `AIInteractionImage` request.
3. Set `AIRequestCall.Parameters = toolCall.Parameters` so provider extras reach the provider.
4. Execute `AIRequestCall` with the selected provider.
5. Extract `AIInteractionImage` from the result.

This keeps components simple and pushes provider-specific mapping into the provider's `Encode` method.

### WebChat / canvas chat

- WebChat already renders any `IAIRenderInteraction` (including `AIInteractionImage`) through `WebChatObserver`.
- No dedicated image-generation chat command is required in phase 1. A chat message can trigger the `text2img` tool if the agent decides to call it; the resulting `AIInteractionImage` will render in the chat history.
- If the chat needs an explicit "generate image" command, this should be a separate, follow-up component/tool and is out of scope.

## 6. Detailed implementation steps

### Step 1: Create the provider project

**Files to create**

- `src/SmartHopper.Providers.Bfl/SmartHopper.Providers.Bfl.csproj`
- `src/SmartHopper.Providers.Bfl/Properties/AssemblyInfo.cs` (or attributes in `BflProvider.cs`)
- `src/SmartHopper.Providers.Bfl/Properties/Resources.resx`
- `src/SmartHopper.Providers.Bfl/Resources/bfl_icon.png`
- `src/SmartHopper.Providers.Bfl/BflProvider.cs`
- `src/SmartHopper.Providers.Bfl/BflProviderFactory.cs`
- `src/SmartHopper.Providers.Bfl/BflProviderSettings.cs`
- `src/SmartHopper.Providers.Bfl/BflProviderModels.cs`

**Tasks**

1. Copy the `.csproj` pattern from `SmartHopper.Providers.OpenAI`:
   - `TargetFrameworks`: `net7.0-windows;net7.0`
   - `EnableDynamicLoading`: `true`
   - `NoWarn`: `NU1701;NETSDK1086;SA1124;SA1200`
   - `GenerateResourceUsePreserializedResources`: `true`
   - Reference `SmartHopper.ProviderSdk`.
2. Add the project to `SmartHopper.sln` with a new GUID.
3. Add assembly attributes:
   ```csharp
   [assembly: SmartHopper.ProviderSdk.Metadata.BuiltAgainstSdk("<current-sdk-version>")]
   [assembly: SmartHopper.ProviderSdk.Metadata.MinHostSdk("<current-sdk-version>")]
   [assembly: SmartHopper.ProviderSdk.Metadata.SmartHopperProviderId("Bfl")]
   ```
   (use the same version pattern as other providers).

### Step 2: Implement `BflProviderSettings`

**File**: `src/SmartHopper.Providers.Bfl/BflProviderSettings.cs`

**Design**

- Inherit from `AIProviderSettings`.
- Constructor takes `BflProvider`.
- `GetSettingDescriptors()` returns:
  - `ApiKey` (string, secret)
  - `Endpoint` (string, default `https://api.bfl.ai`, allowed values: `https://api.bfl.ai`, `https://api.eu.bfl.ai`, `https://api.us.bfl.ai`)
  - `Model` (string, lazy default from `provider.GetDefaultModel(AICapability.Text2Image)`)
  - `TimeoutSeconds` (int, default from `TimeoutDefaults`)
- `ValidateSettings` only requires a non-empty `ApiKey`.

### Step 3: Implement `BflProviderModels`

**File**: `src/SmartHopper.Providers.Bfl/BflProviderModels.cs`

**Design**

- Inherit from `AIProviderModels`.
- `RetrieveModels()` returns a hardcoded `List<AIModelCapabilities>` for the curated set.
- Each entry:
  - `Provider = "bfl"`
  - `Model = "<endpoint-suffix>"` (e.g., `flux-2-pro-preview`)
  - `Capabilities = AICapability.TextInput | AICapability.ImageOutput`
  - `Default = AICapability.Text2Image`
  - `SupportsStreaming = false`
  - `SupportsBatch = false`
  - `Verified = false`
  - `Rank` set by quality/cost (max highest, klein lowest)
  - `Pricing` with `ImageOutput` (first-MP price) and `ImageOutputPerMegapixel` (per additional MP price) per the BFL published rates:
    - `flux-2-klein-4b`: `ImageOutput = 0.014m`, `ImageOutputPerMegapixel = 0.001m`
    - `flux-2-klein-9b`: `ImageOutput = 0.015m`, `ImageOutputPerMegapixel = 0.002m`
    - `flux-2-pro`: `ImageOutput = 0.03m`, `ImageOutputPerMegapixel = 0.015m`
    - `flux-2-max`: `ImageOutput = 0.07m`, `ImageOutputPerMegapixel = 0.03m`
    - `flux-2-flex`: `ImageOutput = 0.05m`, `ImageOutputPerMegapixel = 0.05m`
    - `flux-kontext-pro`: use `pro` rates as an approximation
- `RetrieveApiModels()` returns the hardcoded model names; this is the hook for a future live endpoint.

### Step 4: Add `AIProvider.ExecuteCall` hook

**File**: `src/SmartHopper.ProviderSdk/AIProviders/AIProvider.cs`

Change the existing `Call` method to delegate the actual HTTP execution to a new protected hook. `Call` remains non-virtual and continues to own the full lifecycle.

```csharp
public async Task<IAIReturn> Call(AIRequestCall request, CancellationToken cancellationToken = default)
{
    // Start stopwatch
    var stopwatch = new Stopwatch();
    stopwatch.Start();

    // Execute PreCall
    request = this.PreCall(request);

    // Ensure the provider is configured before attempting any API call.
    if (!this.IsConfigured)
    {
        stopwatch.Stop();
        var configurationError = new AIReturn();
        var configurationMetrics = new AIMetrics
        {
            FinishReason = "error",
            CompletionTime = stopwatch.Elapsed.TotalSeconds,
        };

        configurationError.CreateError($"{this.Name} provider is not configured. Please set the required provider settings in SmartHopper settings.", request, configurationMetrics);

        return configurationError;
    }

    // Validate request before calling the API (structured messages)
    (bool isValid, List<SHRuntimeMessage> messages) = request.IsValid();
    if (!isValid)
    {
        stopwatch.Stop();
        var result = new AIReturn();
        var metrics = new AIMetrics
        {
            FinishReason = "error",
            CompletionTime = stopwatch.Elapsed.TotalSeconds,
        };

        result.CreateError("The request is not valid", request, metrics);

        return result;
    }

    // Execute the provider-specific call implementation (HTTP round-trip or async job)
    var response = await this.ExecuteCall(request, cancellationToken).ConfigureAwait(false);

    // For backoffice/admin-style calls, return raw without chat decoding or timestamping
    if (request?.RequestKind == AIRequestKind.Backoffice)
    {
        stopwatch.Stop();
        return response;
    }

    // Add provider specific metrics
    stopwatch.Stop();
    response.SetCompletionTime(stopwatch.Elapsed.TotalSeconds);

    // Execute PostCall
    response = this.PostCall(response);

    return response;
}

/// <summary>
/// Executes the core provider request. The default implementation performs a single
/// HTTP round-trip via <see cref="CallApi"/>. Providers with async job semantics
/// (e.g., submit-and-poll) can override this method.
/// </summary>
/// <param name="request">The pre-processed request to execute.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>The provider response.</returns>
protected virtual async Task<IAIReturn> ExecuteCall(AIRequestCall request, CancellationToken cancellationToken)
{
    return await this.CallApi(request, cancellationToken).ConfigureAwait(false);
}
```

This preserves the original design:
- `Call` is the strict template and stays non-virtual.
- Lifecycle (PreCall, validation, timing, PostCall) is still owned by `AIProvider.Call`.
- No provider can skip validation, timing, or `PostCall`.
- Existing providers are unaffected; only BFL overrides `ExecuteCall`.
- `CallApi` stays private.

### Step 5: Implement `BflProvider`

**File**: `src/SmartHopper.Providers.Bfl/BflProvider.cs`

**Class skeleton**

```csharp
public sealed class BflProvider : AIProvider<BflProvider>
{
    public override string Name => "BFL";
    public override Uri DefaultServerUrl => new Uri(this.GetSetting<string>("Endpoint") ?? "https://api.bfl.ai");
    public override bool IsEnabled => true;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(this.GetApiKey());
    public override Image Icon => this.LoadIconFromResources(Properties.Resources.bfl_icon);

    private BflProvider()
    {
        this.Models = new BflProviderModels(this);
    }

    // ... Encode, Decode, PreCall, ExecuteCall, PostCall, GetExtraDescriptors
}
```

**`PreCall(AIRequestCall request)`**

1. `request = base.PreCall(request);`
2. `request.HttpMethod = "POST";`
3. `request.ContentType = "application/json";`
4. `request.Authentication = "none";` (BFL uses `x-key`, not `Authorization`)
5. Set `request.Headers["x-key"] = this.GetApiKey()`.
6. If `request.Endpoint` is empty or `/images/generations`, map it to the selected model:
   - `request.Endpoint = "/v1/" + this.SelectModel(AICapability.Text2Image, request.Model ?? this.GetDefaultModel(AICapability.Text2Image));`
7. Ensure `request.Body` contains the `AIInteractionImage` request.
8. Return `request`.

**`Encode(AIRequestCall request)`**

1. Extract the `AIInteractionImage` from `request.Body.Interactions`.
2. Build the BFL request JSON:
   - `prompt` from `image.OriginalPrompt`
   - `width`/`height` from `image.ImageSize` (parse `"1024x1024"`, fallback to aspect ratio math)
   - `output_format` from `request.Parameters.Extras["output_format"]` or default `"jpeg"`
   - `seed` from extras if present and non-empty
   - `safety_tolerance` from extras or default `2`
   - `prompt_upsampling` from extras or default `false` for all models except `flex` where BFL defaults to `true`
   - `steps` and `guidance` from extras if model is `flux-2-flex` (otherwise ignored)
   - `webhook_url` from extras if present
3. Return the JSON string.

**`ExecuteCall(AIRequestCall request, CancellationToken cancellationToken)`**

1. Submit:
   - `using var httpClient = this.CreateHttpClient(TimeSpan.FromSeconds(request.TimeoutSeconds ?? TimeoutDefaults.DefaultTimeoutSeconds));`
   - `var submitUrl = this.BuildFullUrl(request.Endpoint);`
   - POST JSON (`this.Encode(request)`) with `x-key` header.
   - Read `JObject submitResponse`.
2. Parse `submitResponse`:
   - If `polling_url` is missing, create `AIReturn` with `submitResponse` as raw and error message.
   - If status is already `Ready`, skip polling.
   - If status is an error, create `AIReturn` with error.
3. Poll:
   - Loop while not cancelled.
   - GET `polling_url` with `x-key` header.
   - Parse `JObject pollResponse`.
   - If `status == "Ready"`, break.
   - If `status` is `Error`, `Failed`, `Request Moderated`, `Content Moderated`, create `AIReturn` with error and stop.
   - If `status` is `Pending`/`InQueue`/`Processing`, sleep 500–1000ms and continue.
   - Honor a `BflProvider` setting `PollTimeoutSeconds` or `request.TimeoutSeconds`; if total elapsed exceeds timeout, create timeout error.
4. Final result:
   - `var result = new AIReturn { Request = request };`
   - `result.SetBody(pollResponse);` (this invokes `this.Decode(pollResponse)`)
   - Return `result`.

Note: `ExecuteCall` does **not** call `PostCall` or `SetCompletionTime`; `AIProvider.Call` does that after `ExecuteCall` returns.

**`Decode(JObject response)`**

1. Validate `response["status"]?.ToString() == "Ready"`.
2. Extract `result["sample"]` (image URL), `result["id"]` (request id), and optional `result["prompt"]` (revised prompt).
3. Extract `width` and `height` from `result["width"]`/`result["height"]` if present, else parse from the original request or default to `1024`x`1024`.
4. Create `AIInteractionImage`:
   - `Agent = AIAgent.Assistant`
   - `ImageUrl = new Uri(sampleUrl)`
   - `OriginalPrompt = originalPrompt`
   - `RevisedPrompt = revisedPrompt ?? originalPrompt`
   - `ImageSize = $"{width}x{height}"`
   - `MimeType = outputFormat`
   - `Metrics = new AIMetrics { OutputImageWidth = width, OutputImageHeight = height, FinishReason = "stop" }`
5. Return a list containing this interaction.

**`PostCall(IAIReturn response)`**

- Default passthrough. Optionally add BFL-specific runtime messages (e.g., credit warnings) if needed. Keep minimal.

**`GetExtraDescriptors()`**

Return `IEnumerable<AIExtraDescriptor>`:

- `output_format` (string, default "jpeg", allowed ["jpeg", "png"])
- `seed` (string, default "", description "Random seed; leave empty for random")
- `safety_tolerance` (int, default 2)
- `prompt_upsampling` (bool, default false)
- `steps` (int, default 28, description "FLUX.2 [flex] only, 1–50")
- `guidance` (double, default 4.5, description "FLUX.2 [flex] only, 1.5–10")
- `webhook_url` (string, default "")

**`SelectModel` and `GetDefaultModel`**

- Use the base implementation; `BflProviderModels` registers the curated catalog so `AIModelCapabilityRegistry` can resolve it.
- `GetDefaultModel(AICapability.Text2Image)` should return `flux-2-pro-preview`.

### Step 6: SDK pricing and metrics changes

**Files**: `src/SmartHopper.ProviderSdk/AIModels/AIModelPricing.cs`, `src/SmartHopper.ProviderSdk/AICall/Metrics/AIMetrics.cs`, `src/SmartHopper.ProviderSdk/AICall/Metrics/AICostCalculator.cs`

1. `AIModelPricing`
   - Add `public decimal? ImageOutputPerMegapixel { get; set; }` with XML doc: "Price per additional megapixel for image generation (e.g. BFL megapixel pricing)."

2. `AIMetrics`
   - Add `public int? OutputImageWidth { get; init; }`
   - Add `public int? OutputImageHeight { get; init; }`
   - Update `WithCombined` to propagate these values when combining metrics (e.g., fallback chains or multi-item runs). Use the right-hand value if both sides have it, otherwise the non-null side.

3. `AICostCalculator.Calculate`
   - After token-based cost calculation, check if `metrics.OutputImageWidth` and `metrics.OutputImageHeight` are set and `pricing.ImageOutput` or `pricing.ImageOutputPerMegapixel` are positive.
   - Compute megapixels: `mp = width * height / 1_000_000.0`.
   - BFL formula: `cost = GetPositivePrice(pricing.ImageOutput) + Math.Max(0, mp - 1) * GetPositivePrice(pricing.ImageOutputPerMegapixel)`.
   - If `ImageOutputPerMegapixel` is not positive but `ImageOutput` is positive, use flat `ImageOutput` as a fallback.
   - Ensure token-based costs are not also added for image calls where `OutputImageWidth`/`OutputImageHeight` are set.

### Step 7: Fix `text2img` tool to forward parameters and aspect ratio

**File**: `src/SmartHopper.Core.Grasshopper/AITools/text2img.cs`

1. In `GenerateImageToolWrapper(AIToolCall toolCall)`:
   - Read `args["aspect_ratio"]` and pass it to `AIBodyBuilder.AddImageRequest(..., aspectRatio: aspectRatio)`.
   - After `aiRequest.Initialize(...)`, set `aiRequest.Parameters = toolCall.Parameters;` (or `AIRequestParameters.FromModel(modelName)` with extras merged if `toolCall.Parameters` is null).
2. No change to the parameters schema (extras are not validated by the tool schema; `additionalProperties` is not forbidden).

This makes the central tool provider-agnostic: the component keeps its basic inputs, and BFL specifics flow through `AIRequestParameters.Extras`.

### Step 8: Ensure `AIText2ImgComponent` and `AI2ImgComponent` behavior

**Files**: `src/SmartHopper.Components/Img/AIText2ImgComponent.cs`, `src/SmartHopper.Components/Output/AI2ImgComponent.cs`

1. `AIText2ImgComponent` already has a `Settings` input and uses `text2img`. No structural changes. It should pass `aspect_ratio` in the parameters `JObject` (it already does).
2. `AI2ImgComponent` uses `CallAIAsync` and is not BFL-compatible. No structural changes in phase 1. Document this limitation in `docs/Providers/Bfl.md` and `AI2ImgComponent` tooltips if helpful.

### Step 9: Model auto-retrieval fallback

**File**: `src/SmartHopper.Providers.Bfl/BflProviderModels.cs`

- `RetrieveModels()` is the source of truth (hardcoded metadata and pricing).
- `RetrieveApiModels()` returns the hardcoded model names. Add a `// TODO` comment noting that a live BFL endpoint can be wired here if one becomes available.
- No call to an external BFL `/models` endpoint in phase 1 (it does not exist).

### Step 10: Unit tests

**Test project**: create `src/SmartHopper.Providers.Bfl.Tests/SmartHopper.Providers.Bfl.Tests.csproj`.

Reference `SmartHopper.Providers.Bfl`, `SmartHopper.ProviderSdk`, and the `SmartHopper.ProviderSdk.Tests` test helpers. Use `[Collection("ProviderSdk")]` to match existing provider test conventions.

**Test cases**

- `BflProviderModels_ReturnsCuratedModels` — verifies the catalog count and pricing.
- `BflProvider_Encode_CreatesCorrectJson` — verifies `prompt`, `width`, `height`, `output_format`, and extras.
- `BflProvider_Decode_ReturnsImageInteraction` — verifies `AIInteractionImage` and `AIMetrics.OutputImageWidth/Height`.
- `BflProvider_ExecuteCall_PollsUntilReady` — uses a fake `HttpMessageHandler` to simulate submit → pending → ready and verify the final `IAIReturn`.
- `BflProvider_ExecuteCall_SurfacesModerationError` — verifies `Request Moderated` becomes an error `AIReturn`.
- `BflProvider_ExecuteCall_RespectsCancellationToken` — verifies the polling loop stops when cancelled.
- `AIProvider_ExecuteCall_DefaultCallsCallApi` — verifies existing providers still go through `CallApi` when `ExecuteCall` is not overridden.
- `AICostCalculator_ImageMegapixelPricing` — verifies the per-MP formula.
- `Text2ImgTool_ForwardsParametersAndAspectRatio` — verifies `AIRequestCall.Parameters` is set.

Do not add tests requiring Rhino/Grasshopper references.

### Step 11: Documentation and changelog

**Files**

- `docs/Providers/Bfl.md` (new)
- `docs/Providers/index.md` (add BFL link)
- `CHANGELOG.md` (add under `[Unreleased]`)

**Content for `docs/Providers/Bfl.md`**

- Provider name, project path, since version.
- Supported models and capabilities.
- Authentication: `x-key` header.
- Async submit-and-poll flow.
- Extra parameters table.
- Pricing model.
- Component compatibility (`AIText2ImgComponent` vs `AI2ImgComponent`).
- Example Grasshopper wiring: `AISettingsComponent` + `AIExtraSettingsComponent` + `AIText2ImgComponent`.

**Content for `CHANGELOG.md`**

Under `[Unreleased]` → `Added`:
- New `SmartHopper.Providers.Bfl` provider.
- BFL FLUX.2 text-to-image model support.
- Provider-specific extra parameters for image generation (`output_format`, `seed`, `safety_tolerance`, `prompt_upsampling`, `steps`, `guidance`, `webhook_url`).
- `AIModelPricing.ImageOutputPerMegapixel` and `AICostCalculator` per-megapixel cost support.
- `AIMetrics.OutputImageWidth` and `OutputImageHeight`.
- `text2img` tool now forwards `AIRequestParameters` and `aspect_ratio`.
- `AIProvider.ExecuteCall` protected hook for provider-specific execution semantics (used by BFL for submit-and-poll).

## 7. Files to modify

| File | Change |
| --- | --- |
| `SmartHopper.sln` | Add `SmartHopper.Providers.Bfl` and `SmartHopper.Providers.Bfl.Tests` projects. |
| `src/SmartHopper.ProviderSdk/AIProviders/AIProvider.cs` | Add `protected virtual ExecuteCall`; update `Call` to delegate to it. |
| `src/SmartHopper.ProviderSdk/AIModels/AIModelPricing.cs` | Add `ImageOutputPerMegapixel`. |
| `src/SmartHopper.ProviderSdk/AICall/Metrics/AIMetrics.cs` | Add `OutputImageWidth` and `OutputImageHeight`; update `WithCombined`. |
| `src/SmartHopper.ProviderSdk/AICall/Metrics/AICostCalculator.cs` | Compute per-megapixel image cost. |
| `src/SmartHopper.Core.Grasshopper/AITools/text2img.cs` | Forward `toolCall.Parameters` and `aspect_ratio` to `AIRequestCall`. |
| `src/SmartHopper.Providers.Bfl/*` | New provider (see Step 5). |
| `src/SmartHopper.Providers.Bfl.Tests/*` | New tests. |
| `docs/Providers/Bfl.md` | New documentation. |
| `docs/Providers/index.md` | Link to BFL doc. |
| `CHANGELOG.md` | Log changes. |

## 8. Verification plan

- Build:
  - `dotnet build src/SmartHopper.Providers.Bfl/SmartHopper.Providers.Bfl.csproj`
  - `dotnet build SmartHopper.sln` (no signing)
- Tests:
  - `dotnet test src/SmartHopper.Providers.Bfl.Tests/SmartHopper.Providers.Bfl.Tests.csproj -p:SignAssembly=false`
  - `dotnet test src/SmartHopper.ProviderSdk.Tests/SmartHopper.ProviderSdk.Tests.csproj -p:SignAssembly=false`
- Runtime (manual, Rhino required):
  - Place `SmartHopper.Providers.Bfl.dll` in the SmartHopper provider search path.
  - Open Grasshopper, drop `AIText2ImgComponent`, set provider to `BFL`, connect prompt and `AISettingsComponent` with a valid `ApiKey`.
  - Add an `AIExtraSettingsComponent`, select `BFL`, set `output_format` to `png` and `seed`.
  - Run and verify a `GH_VersatileImage` is produced.
  - Check `Metrics` output for estimated cost.

## 9. Risks and considerations

- **`x-key` auth**: handled as a request header inside `BflProvider.PreCall`; this keeps the base `CallApi` auth logic clean. No framework change to auth enums.
- **Image URL lifetime**: BFL `result.sample` URLs expire after 10 minutes. SmartHopper `VersatileImage` can load from a URL, so the user must recompute if the URL expires. Phase 1 does not download and cache the image inside the provider.
- **Pricing accuracy**: BFL's published rates are in US dollars; the SDK uses `decimal`. The per-megapixel formula matches BFL's documented structure, but promotional pricing or regional differences may not be captured.
- **Model metadata**: hardcoded. New BFL endpoints require a code change until a live model list endpoint exists.
- **Grasshopper extras discoverability**: provider-specific extras are hidden in `AIExtraSettingsComponent`. Users must know to add it. This is acceptable per the user's choice and matches the existing OpenAI/Mistral pattern.
- **Rate limiting**: BFL limits active tasks to 24 (6 for `flux-kontext-max`). `BflProvider.ExecuteCall` should surface `429` and `402` errors clearly but does not implement client-side queueing.
- **AI2ImgComponent incompatibility**: because it uses `CallAIAsync` (chat/responses), it cannot drive BFL's standalone image endpoints. Users must use `AIText2ImgComponent`.
- **Backoffice calls for async providers**: `AIProvider.Call` returns `response` directly when `RequestKind == AIRequestKind.Backoffice`. For BFL, `ExecuteCall` returns the final `AIReturn`; backoffice callers will receive the final decoded body, not the submit response. Document this in `docs/Providers/Bfl.md`.

## 10. Open decisions / follow-up

- Image-to-image editing is intentionally deferred to phase 2. When implemented, it will reuse `Image2Image` capability and add `input_image` handling to `BflProvider.Encode`.
- Webhook delivery (`webhook_url`) is accepted as an extra but not consumed by SmartHopper in phase 1.
- Video generation (FLUX 3) is dormant; it will need a new `AICapability.VideoOutput` component/tool when prioritized.
- A future `IAIAsyncCallProvider` public contract can be layered on top of `ExecuteCall` if a second or third submit-poll provider appears. `ExecuteCall` is intentionally the stable, lower-level extension point.
