# Changelog

All notable changes to SmartHopper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### ⚠️ BREAKING CHANGES

- **Renamed AI Tools** (old → new):
  - `text_generate` → `text2text`
  - `text_evaluate` → `text2boolean`
  - `list_generate` → `text2textlist`
  - `list_evaluate` → `textlist2boolean`
  - `img_generate` → `text2img`
  - `img_to_text` → `img2text`
  - `file_to_md` → `file2md`
  - `web_to_md` → `web2md`
  - `web_generic_page_read` → **REMOVED** (use `web2md` instead)

- **Renamed Components** (class and file names changed):
  - `AITextGenerate` → `AIText2TextComponent`
  - `AITextEvaluate` → `AIText2BooleanComponent`
  - `AITextListGenerate` → `AIText2TextListComponent`
  - `AIListEvaluate` → `AIList2BooleanComponent`
  - `AIImgGenerateComponent` → `AIText2ImgComponent`
  - `AIImgToTextComponent` → `AIImg2TextComponent`
  - `AIFileToMdComponent` → `AIFile2MdComponent`
  - `WebPageReadComponent` → **REMOVED** (use `Web2MdComponent` instead)
  - `AIInstructionsComponent` → `AIPromptComponent`

- **Batch API breaking changes** (for custom `IAIBatchProvider` implementations):
  - `AIBatchStatus.ResultBody` replaced by `Results` (dictionary mapping custom ID to response body)
  - `IAIBatchProvider.SubmitBatchAsync` signature changed to accept multiple items
  - `OnBatchCompleted` signature changed from `(AIReturn)` to `(IReadOnlyDictionary<string, JObject>)`

- **Settings input change**: `AIStatefulAsyncComponentBase` `Model (M)` input replaced by `Settings (S)` parameter accepting `AIRequestParameters` or plain model name string

- **Removed `service_tier` extra descriptor** from OpenAI, Anthropic, and MistralAI providers. Use the dedicated `Batch` input on `AISettingsComponent` instead.

- **Removed legacy interaction classes**: `AIInteractionError`, `AIInteractionInfo`, `AIInteractionWarning`, `AIInteractionDebug`, `AIInteractionDiagnosticBase`. Use `AIInteractionRuntimeMessage` with explicit `Severity` instead.

- **`ComponentBase` deep refactor** (`src/SmartHopper.Core/ComponentBase/`):
  - `IProviderComponent.HasProviderChanged()` removed — its accessor mutated state on read. Replace with the new `ProviderSelectionCore.ProviderChanged` event and idempotent `IsProviderChangedSinceLastCommit()`/`CommitProviderChange()` methods. `ProviderComponentHelper` was folded into `ProviderSelectionCore`.
  - `AIStatefulAsyncComponentBase.SetAIReturnSnapshot` is now `protected` (was `public`); `_persistedMetrics` is now `private` (was `protected`) — derived classes must use the new `protected SetPersistedMetrics(AIMetrics)` setter or `CombineIntoPersistedMetrics`.
  - Inner attributes classes `SelectingComponentAttributes` and `AISelectingComponentAttributes` retained but now thin shells over the new `internal SelectingButtonBehavior` helper.
  - **Legacy `Value_*` / `Type_*` persistence reader removed** along with `PersistenceConstants.EnableLegacyRestore`. Files saved with the V2 schema (anything from the public release line) restore unchanged; pre-V2 alpha files lose persistent outputs on first open and must be re-run once.

### Added

- **LocalAI provider** (`SmartHopper.Providers.LocalAI`): new built-in provider for self-hosted [LocalAI](https://localai.io/) instances. Talks to the standard OpenAI-compatible `/v1/chat/completions` endpoint, with full support for tools/function calling, streaming, JSON Schema structured outputs, and an `Authorization: Bearer` API key when one is configured. Settings include a required **Base URL** (default `http://localhost:8080/v1`) plus optional API Key, Model, Streaming, Max Tokens, and Temperature. The provider does not auto-discover models — users list whatever models they have installed locally.
- **Ollama provider** (`SmartHopper.Providers.Ollama`): new built-in provider for [Ollama](https://ollama.com/) running locally via its [OpenAI-compatible endpoint](https://ollama.com/blog/openai-compatibility). Mirrors the LocalAI capabilities (tools, streaming, JSON Schema, optional bearer token for reverse-proxy setups) plus an extra `seed` extra-descriptor for reproducible sampling. Settings include a required **Base URL** (default `http://localhost:11434/v1`) plus optional API Key, Model (e.g. `llama3.1`, `qwen2.5:14b`, `mistral:7b-instruct` — install with `ollama pull <model>`), Streaming, Max Tokens, and Temperature.
- **DEV.md provider model sync automation**: added `tools/Update-DevProviderModels.ps1` and a GitHub workflow that validates provider model documentation on PRs and opens sync PRs after protected-branch provider registry updates.
- **README Trademark and Logo Usage Policy**: explicit policy clarifying that the SmartHopper name and logo are not licensed under LGPL, listing permitted uses (articles, tutorials, educational materials, references to the unmodified official plug-in) and uses requiring prior written permission (commercial bundling, forks, materials that may imply endorsement).

#### 📋 List I/O components

- **Typed list adapters** (8 new components) that collapse / expand JSON arrays per branch:
  - **Inputs (`SmartHopper > Input`)**: `TextList2AI`, `NumberList2AI`, `IntegerList2AI`, `BooleanList2AI` — read a typed `GH_ParamAccess.list` and emit a single `GH_AIInputPayload` whose payload text is a `JsonConvert.SerializeObject(list)` JSON array. One payload per branch.
  - **Outputs (`SmartHopper > Output`)**: `AI2TextList`, `AI2NumberList`, `AI2IntegerList`, `AI2BooleanList` — instruct the model to return a JSON array of the requested type, then expand each parsed element into the output branch (`GH_ParamAccess.list`). Each gains an optional `Fallback` (F) typed list input and `Used Fallback` (UF) boolean output: when the AI response is unparseable or yields zero parseable items, the fallback list is emitted (or empty if not provided) and `UsedFallback=true`. Built on the unified `OutputMapping.Extractor` enumerable contract.
- **`OutputMapping.Extractor` unified**: single `Func<AIReturn, IEnumerable<IGH_Goo>>` covers both scalar and list outputs. Replaces the prior scalar `Func<AIReturn, IGH_Goo>` and the short-lived `ListExtractor` field. Scalar callsites use the new `OutputMapping.Single(...)` helper. **Breaking** for any external `OutputMapping` consumer — migration is a 1-line wrap.
- **`AIOutputAdapterBase` batch ↔ non-batch parity**: both legs now route mapping decoding through a single private `DecodeAllMappings(...)` helper. New mapping-aware `ProcessMappingsBatchResults` walks the primary sentinel tree, decodes each branch's `AIReturn` once via `provider.Decode`, and `AppendRange`'s the per-mapping items at the sentinel's path — so list-shaped outputs (e.g. `AI2*List`) work transparently in batch mode. The `AIOutputAdapterBase`-level override of `SentinelTransformOutputs` was removed; the virtual hook remains on `AIStatefulAsyncComponentBase` for legacy non-adapter components (`AIText2BooleanComponent`, `AIList2BooleanComponent`).
- **`AIInputAdapterBase` output centralization**: the standard `Input >` output is now sealed-registered by the base class. Subclasses no longer duplicate the registration; they may tailor the tooltip via the new `PayloadOutputDescription` virtual property and add extra outputs via the new `RegisterAdditionalOutputParams` hook. Refactored `Text2AI`, `Img2AI`, `Audio2AI`, `Json2AI`, `AIInstructions`, and the four `*List2AI` components.

#### 🆕 New AI Providers and Models

- **AI model registry refresh** across providers, aligned with official documentation (Apr 2026):
  - **OpenAI**: added `gpt-5.5` (Rank 90, Default `Text2Text | ReasoningChat`) and `gpt-image-2` (new image flagship, Default `Text2Image | Image2Image`).
  - **Anthropic**: added `claude-opus-4-7` (Rank 90, no `Default` set; existing `claude-sonnet-4-6` and `claude-haiku-4-6` retain capability defaults).
  - **DeepSeek**: added `deepseek-v4-pro` (Rank 100) and `deepseek-v4-flash` (Rank 95, Default `Text2Text | ToolChat | ToolReasoningChat`, with `Reasoning` capability).
  - **MistralAI**: added dated aliases `mistral-medium-3-1-25-08`, `mistral-small-3-2-25-06`, `magistral-medium-1-2-25-09`, `magistral-small-1-2-25-09`, `voxtral-mini-transcribe-26-02`, and new `devstral-2-25-12` code-agent model. Kept `*-latest` aliases (Mistral repoints them automatically).
  - **OpenRouter**: added `openai/gpt-5.5`, `anthropic/claude-opus-4-7`, `deepseek/deepseek-v4-flash`, `mistralai/mistral-small-4`.

- **Centralized JSON Formatting Utility** (`JsonFormatHelper`):
  - New utility class in `SmartHopper.Infrastructure.Utilities` for consistent JSON formatting
  - Core methods: `JsonToString()` (JToken/string to minified JSON), `StringToJson()` (string to JToken), `IsValidJson()` (validation with optional parsing)
  - All methods support optional error handling via `out string error` parameter
  - **Automatic markdown code block extraction**: String-based methods automatically extract JSON from ` ```json ... ``` `, ` ```txt ... ``` `, ` ```text ... ``` `, and ` ``` ... ``` ` blocks before processing
  - Ensures all JSON output is minified (no unnecessary whitespace) across all JSON components and tools
  - Provides graceful error handling with detailed error messages for invalid JSON
  - Integrates seamlessly with existing `JsonPathHelper` for error messaging
  - Follows `AIResponseParser` patterns for consistency with existing response parsing logic
  - **`JsonSanitizerComponent`** (`SmartHopper > JSON > JSON Sanitizer`): sync component that recovers a valid JSON object from a malformed / AI-generated string. Runs the full recovery pipeline (direct parse → strip markdown fences → brace-depth extraction → sanitization), outputs a minified single-line JSON plus a human-readable summary of the steps performed. Supports tree input with preserved path/branch structure on both outputs.
  - **`JsonFormatHelper.TryRecoverJsonToken`** / **`TryRecoverJsonObject`** and **`ExtractFirstJsonContainer`** / **`ExtractFirstJsonObject`**: centralized JSON recovery utilities. Token-level recovery preserves JSON **array** roots (`[...]`) in addition to objects; object-only wrapper rejects array roots for callers that need object semantics. `AIResponseParser.ParseJsonObjectFromResponse` now delegates to `TryRecoverJsonObject` instead of re-implementing the recovery pipeline. `JsonSanitizerComponent` uses `TryRecoverJsonToken` so array inputs round-trip as arrays.


- **Google Gemini Provider**: Full integration of Google's Gemini AI models
  - Support for Gemini 3.1, 2.5, 2.0, and 1.5 models
  - Text generation with streaming support
  - Image generation, structured outputs, tool calling
  - Extended thinking/reasoning with configurable levels
  - Batch processing with priority support
  - Service tier selection (`standard`, `flex`, `priority`)

- **Phase 8 Audio Support (OpenAI, MistralAI, Gemini)**:
  - Speech-to-Text (STT) and Text-to-Speech (TTS) API endpoints
  - OpenAI: `whisper-1`, `gpt-4o-transcribe`, `gpt-4o-mini-transcribe` for STT; `tts-1`, `tts-1-hd` for TTS
  - MistralAI: `voxtral-small-latest`, `voxtral-mini-latest` for STT; `voxtral-tts-26-03` for TTS
  - Gemini: Native audio input; `gemini-2.5-flash/preview-tts` for TTS; `lyria-3-clip/pro` for music generation

- Community model verification flow:
  - New issue template `.github/ISSUE_TEMPLATE/model-verification.yml` with tests grouped by location ("Components on the Grasshopper canvas" — `AITextGenerate`, `AITextListGenerate`, `AIImgToText`, `AIImgGenerate`, audio — and "Chat interface" — streaming, ToolChat/FunctionCalling, Reasoning, multi-turn `ConversationSession`), each test specifying the **exact prompt** to use and the expected behavior. The template also embeds a copy-paste codeblock (with a `/verify-confirm` header and a hidden `<!-- model-verification-confirm -->` marker) for additional verifiers to use as their certification comment.
  - New workflow `.github/workflows/model-verification.yml` that triggers only when an issue comment starts with `/verify-confirm` (and contains the template marker) or `/verify-force`, tallies distinct GitHub users (issue author + valid `/verify-confirm` commenters), and opens a PR promoting the model to `Verified = true` once two distinct users have certified it. `/verify-force` is restricted to `OWNER`/`MEMBER`/`COLLABORATOR`.
  - New helper `tools/Update-ModelVerified.ps1` that locates the matching `new AIModelCapabilities { Model = "..." }` block in `src/SmartHopper.Providers.<Provider>/<Provider>ProviderModels.cs` and flips `Verified = false` to `Verified = true` (or inserts the flag when missing).

- Automated provider model discovery CI (OpenRouter as single source of truth):
  - New workflow `.github/workflows/chore-update-provider-models.yml` that runs weekly (Sundays 05:00 UTC) and on `workflow_dispatch`. It queries OpenRouter's unified `/models` endpoint for each supported provider, compares the returned metadata with the static declarations in `*ProviderModels.cs`, and opens a PR that both auto-inserts new models and marks disappeared/expiring models as `Deprecated = true`.
  - New composite action `.github/actions/ai/fetch-models/action.yml` that invokes `tools/Update-ProviderModels.ps1` with an OpenRouter API key, passing the provider name and `update-file` flag.
  - New PowerShell tool `tools/Update-ProviderModels.ps1` invoked by the workflow.  Accepts `-Provider`, `-OpenRouterApiKey` (OpenRouter key), and an optional `-TargetFile`.  It queries OpenRouter, filters by provider prefix, maps `architecture.input_modalities`/`output_modalities` and `supported_parameters` to `AICapability` flags, auto-generates full `AIModelCapabilities` blocks for new models (with `ContextLimit`, `Verified=false`), and marks models with `expiration_date` < 1 year or absent from OpenRouter as `Deprecated = true`.  `Rank` values are auto-computed from OpenRouter `created` timestamp (newer models rank higher) and output pricing (cheapest first, considering `pricing.completion`, `pricing.image`, and `pricing.audio_output`).  Existing model capabilities, context limits, and ranks are refreshed on every run.  Emits a structured JSON report containing `newModels`, `deprecatedModels`, and `unchangedModels`.

### Changed

- **AI model rebalancing**:
  - **OpenAI**: `gpt-5.4-mini` retains Rank 100 with `Default = ToolChat | Text2Json | ToolReasoningChat`; moved `Default = Text2Image | Image2Image` from `gpt-image-1-mini` to `gpt-image-2`; cleared `Default = Text2Image` from `dall-e-3` (Rank 80 → 70); demoted `gpt-image-1.5` Rank 75 → 65.
  - **Anthropic**: demoted `claude-opus-4-6` Rank 80 → 75 (superseded by `claude-opus-4-7`).
  - **DeepSeek**: cleared `Default` from `deepseek-chat` (Rank 90 → 70) and `deepseek-reasoner` (Rank 80 → 60); both aliased to `deepseek-v4-flash` per official docs.
  - **OpenRouter**: aligned mirrored OpenAI model `Default` flags with the native OpenAI provider entries — `openai/gpt-5.4-mini` and `openai/gpt-5-mini` now use `ToolChat | Text2Json | ToolReasoningChat`; cleared `Default` on `openai/gpt-5.4` to match native (no Default).

- **Refactored Timeout Configuration System**:
  - **AIRequestBase.TimeoutSeconds** is now nullable (`int?`) to allow null/empty values
  - **RequestTimeoutPolicy** now resolves timeout from settings when null:
    - Reads `Timeout` from settings (with fallback to `HttpTimeoutSeconds` for backward compatibility)
    - Uses `TimeoutDefaults.DefaultTimeoutSeconds` (300s) as final fallback
  - **AISettingsComponent** now has "Timeout" input parameter (before "Extras") for custom timeout override
    - **Breaking (saved files)**: Inserting `Timeout` before `Extras` shifts the `Extras` parameter index by one. Grasshopper persists wire connections by parameter index, so existing `.ghx`/`.gh` files with a wire into `Extras` will, after upgrade, resolve that wire to the new `Timeout` (integer) input. Because the source value (Extras JSON string) is type-incompatible with `Timeout`, the input is silently ignored and the previously wired provider-specific extras are lost on file load. Reconnect the `Extras` input on `AISettingsComponent` after upgrading.
  - **AIStatefulAsyncComponentBase** additions:
    - New `ConfigureRequestTimeout()` helper method - centralizes timeout configuration for both batch and regular paths
  - **AIProvider** simplified - removed duplicate timeout resolution logic, now relies on RequestTimeoutPolicy
  - **Renamed HTTP Timeout Setting** (UI label and setting key):
    - `HTTP Timeout (Regular Calls)` → `Timeout` (setting key: `TimeoutSeconds`, default: 300s)
    - Old setting keys (`HttpTimeoutSeconds`, `ResponseGenerationTimeoutSeconds`) automatically migrated for backward compatibility
  - **Timeout resolution priority** (highest to lowest):
    1. Custom timeout from AI Settings component input
    2. Settings-based timeout (`TimeoutSeconds`)
    3. Safe default (300s)
  - **Unified default across layers**: introduced `SmartHopper.Infrastructure.AICall.Core.TimeoutDefaults` with `DefaultTimeoutSeconds = 300`, `MinTimeoutSeconds = 1`, `MaxTimeoutSeconds = 600`. The following call sites now reference these constants instead of hardcoded literals (previously: 300s policy, 600s provider/batch, 120s tool, 600s streaming idle):
    - `RequestTimeoutPolicy` (default + bounds)
    - `AIProvider.Call()` and `AIProvider.CreateBatchHttpClient()` (default + bounds)
    - `AIToolCall.Exec()` (default + bounds)
    - Streaming idle-timeout fallbacks in OpenAI, OpenRouter, MistralAI, DeepSeek, Anthropic, and Gemini providers
    - `ProvidersSettingsPage` Timeout `NumericStepper` Value/Min/Max
  - Behavior under normal flow is unchanged (policy always resolves first); this aligns the safety-net fallback when the policy pipeline is bypassed and ensures the settings UI bounds match the runtime clamp.

- Components can now mix different `IGH_Goo` types in input trees (e.g., `GH_String` for text inputs, `GH_Boolean` for fallback values)
- Foundation laid for future extensibility to support `GH_Integer`, `GH_Number`, `GH_Path`, and other Grasshopper data types
- Existing `GH_String`-only workers remain compatible and can be migrated individually when needed
- **`AIText2BooleanComponent`**: Migrated to mixed-type IGH_Goo processing pipeline
  - `inputTree` field changed from `Dictionary<string, GH_Structure<GH_String>>` to `Dictionary<string, GH_Structure<IGH_Goo>>`
  - Fallback input now stored natively as `GH_Boolean` without string conversion round-trip
  - Uses `GHStructureConverter.ConvertToGooTree()` for type conversions
  - Accesses results via `ProcessingResult<IGH_Goo>.Outputs` and `ExtractTypedTree<GH_String>()`

- **`AIList2BooleanComponent`**: Migrated to mixed-type IGH_Goo processing pipeline
  - Same structural changes as `AIText2BooleanComponent`
  - Fallback input stored natively as `GH_Boolean`
- **`AIFile2MdComponent` batch context persistence**: `_fileContexts` (base markdown + image slot metadata per file) is now serialized via `Write`/`Read` so batch results can be reconstructed after a Grasshopper file save/reload. A `_batchContextLost` flag prevents `GatherInput` from resetting `_fileContextsInitialized` while a batch is active, fixing a same-session overwrite bug where each poll tick re-initialized the context to empty before `OnBatchCompleted` could use it.
- **`AIFile2MdComponent` batch image descriptions**: `OnBatchCompleted` now extracts image descriptions from `AIInteractionText.Content` (the actual batch response type) instead of `AIInteractionToolResult`, which was never produced in batch mode since `BuildDescribeRequest` bypasses the tool execute wrapper.
- **`AIFile2MdComponent` batch metrics**: Per-slot image decode metrics are now accumulated manually and merged into `AIReturnSnapshot` after `ProcessBatchResults`, since `ProcessBatchResults` only sees the representative sentinel (one per file) and misses all non-first image slots.
- **`img2text` AI Tool**: Extracted shared `BuildRequestBody` and `ExtractDescription` helpers so `DescribeImageAsync` (execute path) and `BuildDescribeRequest` (batch path) send identical requests and decode using the same logic. `ExtractDescription` reads from `AIInteractionText.Content` directly, making both paths consistent.
- **`file2md` AI Tool**: `DescribeImageAsync` internal helper now reads `AIInteractionToolResult["description"]` with a clean single-expression return, removing a dead fallback that logged `assistantText.Content` but always returned `[Image could not be described]`.

- **`AIFile2MdComponent`**: Reworked batch wiring so only the AI calls (image descriptions) are batched. File conversion and image extraction now run locally via `file2md` with `describeImages=false`; each image is then described via `CallAiToolAsync("img2text", ...)` which is batch-interceptable. `OnBatchCompleted` reassembles the final markdown from locally-stored per-file context and batch image description results. `UsingAiTools` updated from `file2md` to `img2text`. Format and Images outputs are computed locally and persisted immediately.
- **`img2text` AI Tool**: Added `BuildRequest` delegate so the tool supports batch mode. The delegate mirrors the existing `DescribeImageAsync` request construction without executing it.

- **`DataTreeProcessor.RunAsync` heterogeneous output support**: Added `RunAsync<T>` overload (delegates to `RunAsync<T, IGH_Goo>`) and `ExtractTypedTree<U>` helper so a single processing call can populate output channels of different concrete `IGH_Goo` types. Matching `RunProcessingAsync<T>` overload added to `StatefulComponentBase`.
- **`File2MdComponent` / `AIFile2MdComponent`**: Replaced manual `foreach` tree iteration with `RunProcessingAsync<GH_String>` + `ExtractTypedTree<U>`, gaining flat-tree broadcasting and consistent `ItemGraft` path management. `ComponentProcessingOptions` property added to both components.

#### 🛠️ New AI Tools and Components

- **`text2json` AI Tool** + **`AIText2JsonComponent`**: Generate structured JSON from a prompt using a JSON Schema
- **`img2text` AI Tool** + **`AIImg2TextComponent`**: Describe/analyze images using vision models
- **`GH_ExtractedImage`**: New Grasshopper Goo type for extracted images with base64 data, MIME type, and metadata
- **`OpenAIProvider` now always sends `reasoning_effort` for o-series / gpt-5 models** regardless of whether tools are present. A speculative `if (!hasTools)` guard — introduced in an unrelated timeout refactor with a comment citing a "gpt-5.4 incompatibility" — was silently stripping `reasoning_effort` from any tool-enabled request on reasoning models. This has no basis in OpenAI's official API reference or the GPT-5 cookbook, both of which document `reasoning_effort` + function calling as fully supported on o-series and gpt-5 families. Users on `gpt-5*` / `o3` / `o4-mini` with function calling will now get the configured reasoning effort as intended.
- **HTTP error classification & streaming structured errors**: Non-success HTTP responses are now classified consistently across non-streaming and streaming paths via a new shared `AIProvider.ClassifyHttpError()` helper. 5xx, 408 and 429 are surfaced as network-style errors (`AIReturn.CreateNetworkError`), while 401/403/413 and other 4xx remain provider errors (`CreateProviderError`), restoring the prior Network/Provider distinction that was momentarily lost when `CallApi` switched from throwing to returning structured `AIReturn`. Streaming adapters (`OpenAI`, `OpenRouter`, `MistralAI`, `DeepSeek`, `Anthropic`, `Gemini`) now use the same classifier and yield a properly tagged `AIReturn` instead of generic provider errors. The base `AIProviderStreamingAdapter.ReadSseDataAsync` now throws a typed `ProviderHttpStatusException` (carrying status code + classification) for callers that don't pre-check status.
- **`FinishReasonNormalizeResponsePolicy` null-`Body` hardening**: The policy now early-returns when `response.Body == null` instead of attempting `AIBodyBuilder.FromImmutable(null).ReplaceLast(...)`. This guards against the slightly more frequent null-body path now that HTTP errors return structured `AIReturn` (with no body) and flow through the response policy pipeline.
- **`AIInteractionImage.SetResult(string, string, string)` silently dropped invalid URL when no image data**: when `imageUrl` was a non-null but invalid (non-absolute / malformed) URI string and `imageData` was `null`, the method returned without setting any image data and without throwing, masking the failure from callers. Now throws `ArgumentException` (paramName `imageUrl`) in that case; the legitimate "invalid URL but valid `imageData`" path still sets `ImageData` as before. Regression tests added in `AIInteractionImageTests`.
- **`AIText2BooleanComponent` / `AIList2BooleanComponent` "Used Fallback" output always reported `false`** in non-batch mode when the fallback was actually applied. The components only forwarded `toolResult["result"]` (which after fallback contains a parseable boolean string like `"True"`) and re-inferred `usedFallback` from the string's parseability, masking the tool's authoritative `usedFallback` flag. `ProcessData` now emits parallel `Result` and `UsedFallback` `IGH_Goo` lists carrying both values from the tool result; `ConvertStringTreeToBoolean` consumes them directly instead of inferring the flag.
- **`AIText2BooleanComponent` / `AIList2BooleanComponent` triple provider-Decode per batch item**: `OnBatchCompleted` was calling `ProcessBatchResults` twice (once for `Result`, once for `Used Fallback`), which iterated the sentinel tree twice and ran `provider.Decode` once internally per pass plus once inside the user lambda — three decodes per item. The second `ProcessBatchResults` call also overwrote `_persistedMetrics` and `AIReturnSnapshot` set by the first. Collapsed into a single `ProcessBatchResults` pass that populates `Result` directly and emits `Used Fallback` via a `TransformOutputs` override using the existing per-sentinel parse cache.
- **`AIText2BooleanComponent` / `AIList2BooleanComponent` user fallback was silently lost in batch mode**: the batch path bypasses the `text2boolean` / `textlist2boolean` tool's execute body, so the parsing+fallback logic in those tools never ran on batch results, producing `null` outputs for unparseable AI responses even when the user had wired a Fallback value. Additionally, `ParseBooleanWithFallback` used strict `bool.TryParse` rather than the tool's lenient `AIResponseParser.ParseBooleanFromResponse`, so common AI replies like "yes"/"no" were rejected. Fixed by capturing the Fallback input value in `GatherInput` into a component field, passing it into `ParseBooleanWithFallback`, and switching the parser to `AIResponseParser.ParseBooleanFromResponse` so batch and non-batch paths now produce identical results.
- **Batch output lost after file close/reopen during polling**: When a Grasshopper file was saved and reopened while a batch job was still in-flight, `_sentinelTrees` (the path layout + sentinel strings needed to reconstruct output trees) was lost because it was only kept in memory. On reload, `GetSentinelTree()` returned `null`, causing `OnBatchCompleted` to exit early and produce no output. Fixed by serializing the full sentinel tree structure (paths + sentinel strings per branch) in `Write()` via a new `BatchSentinelTrees` key, and restoring it in `Read()` before polling resumes. Added diagnostic logging to `GetSentinelTree()` to surface the null case.

#### 📄 File and Web Processing

- **`file2md` AI Tool** + **`File2MdComponent`**: Convert PDF, DOCX, XLSX, PPTX, HTML, CSV, JSON, XML, TXT, EML, EPUB, RTF to Markdown
  - PDF layout intelligence (column detection, reading order, header/footer removal, table detection)
  - Image extraction from PDF, DOCX, PPTX as `GH_ExtractedImage`
  - Native .NET implementation (no Python dependencies)

- **`GeminiProvider.Encode(AIRequestCall)` duplicated per-interaction encoding**: The full-request encoder re-implemented all part-building logic (text, tool call, tool result, image/URL fetch) inline instead of delegating to the existing single-interaction `EncodeToJToken`, creating a maintenance hazard where a fix to one path could miss the other (e.g. the recent image-fetch hardening). `Encode(AIRequestCall)` now loops and delegates to `EncodeToJToken`, which already returns `null` for System/Context (handled separately as `system_instruction`) and for UI-only `AIInteractionError`. Removes ~100 lines of duplication; no behavior change.

- **`GeminiProvider` image URL fetch**: Replaced obsolete `System.Net.WebClient` (deprecated since .NET 6, `SYSLIB0014`) in `FetchImageFromUrl` with a shared `HttpClient`. Fetch is now bounded by `TimeoutDefaults.DefaultTimeoutSeconds` via a `CancellationTokenSource`, capped at 20 MB (Gemini's `inline_data` limit), and prefers the response `Content-Type` for the MIME type before falling back to the URL extension. Gemini's REST API still requires arbitrary HTTP(S) images to be inlined as base64 (its `file_data.file_uri` only accepts Gemini File API URIs or YouTube), so a download remains unavoidable on this path.

- **`AIImgToTextComponent`**: Fixed MistralAI (and other providers) receiving a placeholder string (`"Image [img-3] Page 13 (image/jpeg)"`) instead of actual base64 data when a `GH_ExtractedImage` was connected to the Image input. Changed input from `AddTextParameter` to `AddGenericParameter` and added explicit `GH_ExtractedImage` detection in the worker to extract `Base64Data` and `MimeType` directly, instead of relying on Grasshopper's string cast which calls `ToString()`.

- **`AIFile2MdComponent`**: AI-powered file conversion with image handling modes:
  - `embed` (default): Base64 data URI with AI caption
  - `describe`: AI text description replaces image
  - `caption`: Short AI title replaces image

- **`web2md` AI Tool** + **`Web2MdComponent`**: Convert web pages to Markdown
  - Specialized handlers for Wikipedia, GitHub, GitLab, Discourse, Stack Exchange
  - Readability scoring for generic pages

#### ⚙️ Settings Components

- **`AISettingsComponent`**: Assemble `AIRequestParameters` from universal inputs (Model, Temperature, Max Tokens, Top P, Seed, Timeout, Extras)
- **`AIExtraSettingsComponent`**: Dynamically generated inputs based on selected provider's extra descriptors
- **`AIRequestParameters`**: Immutable per-request configuration with fluent builder

#### 📦 JSON Components (Non-AI)

- **`JsonSchemaComponent`**: Build JSON Schema from property definitions with dot-notation support
- **`JsonSchemaPropComponent`**, **`JsonSchemaPropObjectComponent`**, **`JsonSchemaPropArrayComponent`**: Visual schema builders
- **`JsonObjectComponent`**, **`JsonArrayComponent`**: Create JSON from Grasshopper data
- **`JsonArray2TextListComponent`**, **`JsonObject2TextComponent`**: Parse/serialize JSON
- **`JsonGetValueComponent`**: Extract nested values via dot-notation paths
- **`JsonMergeComponent`**: Shallow merge multiple JSON objects

#### 🚀 Batch Processing

- **Batch API Support** in OpenAI, Anthropic, and MistralAI providers
  - Multi-item queue with `BatchTier` parameter on `AISettingsComponent`
  - Live progress counter (`Processing batch (YY/XX)...`)
  - Automatic save/restore of batch state across file close/reopen
  - Order-based fallback for result loading when sentinel mapping unavailable
  - Per-item custom IDs for traceability

#### 🔧 Infrastructure

- **`JsonFormatHelper`**: Centralized JSON formatting utility with automatic markdown code block extraction and added `JsonFormatHelper.SanitizeJsonString`, a best-effort sanitizer for common AI malformations — escapes unescaped control characters (newline, carriage return, tab, other C0) inside string literals, normalizes smart/curly quotes and zero-width spaces, strips trailing commas before `}` or `]`, closes unterminated string literals with a trailing `"`, and closes unbalanced `{` / `[` in LIFO order so truncated responses become structurally parseable. Wired as a final fallback in `AIResponseParser.ParseJsonObjectFromResponse`, so tools such as `text2json` recover from AI responses containing raw newlines inside JSON string values or prematurely cut off by token limits.
- **HTTP Error Handling**: Structured error messages for 503, 429, 401/403 with actionable guidance
- **Mixed-Type Data Tree Support**: Components can mix `IGH_Goo` types (e.g., `GH_String` + `GH_Boolean`)
- **Unified Diagnostic Interaction** (`AIInteractionRuntimeMessage`): Single interaction type with `Severity`, `Origin`, `Code` replacing per-severity classes
- **Centralized Runtime Message Collection**: Thread-safe message collection in `AsyncWorkerBase` with proper persistence across state transitions

### Changed

- **Batch Processing UX**:
  - Cancellation now shows immediate feedback ("Cancelling batch {id}..." → "Batch {id} cancelled successfully")
  - Provider-agnostic batch result loading from files
  - Progress messages distinguish queuing (`Preparing`) from execution (`Processing`)

- **Timeout Configuration**:
  - Consolidated timeout settings (single `Timeout` setting, default 600s)
  - Resolution priority: Component input → Settings → Default

- **Provider Parameter Resolution**: All providers now read from `request.Parameters` first, then fall back to global settings

- **AI Components Migrated to Mixed-Type Pipeline**:
  - `AIText2BooleanComponent`, `AIList2BooleanComponent` now use `IGH_Goo` trees with native `GH_Boolean` fallback storage

### Fixed

- **Batch Processing**:
  - Sentinel trees now survive file close/reopen and manual result loading
  - Batch item errors properly surfaced as Grasshopper runtime messages
  - HTTP timeout increased for large batch file upload/download (300s default)
  - Order-based fallback for result loading when sentinel mapping unavailable

- **Progress messages**: Live progress counter now shown during batch processing
    - On batch submission: message immediately shows `Processing batch (0/XX)...` instead of the static `Processing batch...`
    - During polling: counter updates live to `Processing batch (YY/XX)...` as items complete (OpenAI: `request_counts.completed`, MistralAI: `succeeded_requests`, Anthropic: `request_counts.succeeded`)
    - During data-tree collection phase (batch mode): progress message shows `Preparing X/X...` instead of `Processing X/X...` to distinguish queuing from actual execution
    - On new run: stale progress count and `ProgressInfo` are reset so old values never bleed into the next run
    - **Fixed**: `GetStateMessage()` now checks `CurrentState` and returns base message for terminal states (Completed, Error, Cancelled, Waiting), preventing stale batch progress messages (e.g., `Processing batch (84/84)...`) from persisting after batch completion or failure
  - **`AIBatchStatus`**: Added optional `CompletedCount` property to non-completed status for in-progress progress reporting; `ResultBody` (single `JObject`) replaced by `Results` (`IReadOnlyDictionary<string, JObject>`) mapping each custom ID to its provider response body. *(Breaking change for custom `IAIBatchProvider` implementations.)*
  - **`IAIBatchProvider.SubmitBatchAsync`**: Signature changed from single-item `(AIRequestCall, CancellationToken)` to multi-item `(IReadOnlyList<(string CustomId, AIRequestCall Request)>, CancellationToken)`. All three providers (Anthropic, OpenAI, MistralAI) updated accordingly. *(Breaking change for custom `IAIBatchProvider` implementations.)*
  - **`AIBatchSubmission`**: `CustomId` (single string) superseded by `CustomIds` (`IReadOnlyList<string>`); `CustomId` is now a compat shim returning the first element. `GenerateCustomId()` now accepts `endpoint` and `index` parameters for richer IDs.
  - **`OnBatchCompleted`**: Signature changed from `(AIReturn)` to `(IReadOnlyDictionary<string, JObject>)` to carry per-item results. *(Breaking change for components overriding this hook.)*

- **`AIStatefulAsyncComponentBase`**: `Model (M)` generic text input replaced by `Settings (S)` generic parameter. Accepts `AIRequestParameters` (from `AISettingsComponent`) or a plain model name string for backwards compatibility. `GetModel()` now reads from `AIRequestParameters.Model` with the same provider-default fallback as before.

- **All providers** (`OpenAI`, `Anthropic`, `MistralAI`, `DeepSeek`, `OpenRouter`): `Encode()` now performs per-property resolution — each parameter (Temperature, MaxTokens, TopP, Seed, and provider-specific extras) reads from `request.Parameters` first, falling back to global provider settings. Previously all providers read exclusively from global settings.

- **`AIRequestBase`**: Added `AIRequestParameters Parameters { get; set; }` property.

- chore(rules): clarified Windsurf rules and workflows to reduce overlap, stale platform assumptions, and ambiguous SmartHopper architecture guidance.
- ci(pr-build-hash-validation): the `validate-no-manual-hash-edits` job now only blocks a PR when a changed file under `hashes/` differs from its counterpart on `main` (the source of truth). PRs that carry a hash commit verbatim from main (e.g., via branch update/rebase) are allowed.
- ci(concurrency): added top-level `concurrency:` to 25 workflows to prevent race conditions and save runner minutes:
  - Auto-commit / auto-PR workflows grouped per ref with `cancel-in-progress: false` (queue, never interrupt a push-back): `chore-version-date`, `chore-update-contributors`, `chore-version-badge`, `pr-anonymize-public-key`, `dev-update-manifest`, `github-labels-sync`, `chore-version-main-release`, `pr-license-headers`, `stabilization-0-init`.
  - Entity-scoped workflows grouped per issue/milestone/release/PR with `cancel-in-progress: false`: `model-verification`, `github-issue-labels-on-close`, `github-issue-labels-close`, `milestone-management`, `release-4-build`, `release-2-pr-to-dev-closed`, `release-3-pr-to-main-closed`. `release-5-deploy-pages` uses the standard `pages` group with `cancel-in-progress: true`.
  - PR validation workflows grouped per PR with `cancel-in-progress: true` so superseded pushes are cancelled: `ci-dotnet-tests`, `pr-validation`, `pr-build-hash-validation`, `pr-version-validation`, `pr-manifest-validation`, `pr-dependency-validation`, `pr-block-dev-to-main`, `pr-milestone`.
- ci(auto-commit hardening): belt-and-braces against external commits landing between fetch and push.
  - `dev-update-manifest` now does `git pull --rebase --autostash origin dev` with retry (×3) before pushing to `dev`.
  - `pr-license-headers` now does `git pull --rebase --autostash` with retry (×3) before pushing back to the PR head branch (handles the contributor pushing a new commit mid-run).
  - `chore-version-badge` gained a `paths: [Solution.props]` filter on its `push` trigger so it no longer runs on every unrelated push to `main`/`dev`; the version source of truth is the only relevant change.
  - Auto-PR workflows that follow the delete-and-recreate pattern (`chore-update-contributors`, `pr-anonymize-public-key`) and those built on `peter-evans/create-pull-request` (`chore-version-date`, `chore-version-badge`, `chore-version-main-release`) already reuse existing PRs and were verified as safe; no changes needed.

- **DeepSeek Provider**: HTTP 400 "insufficient tool messages" caused by merging consecutive tool result messages

- **`AIImgToTextComponent`**: Fixed placeholder string being sent instead of actual base64 data for `GH_ExtractedImage` inputs

- **`text2json` tool**: JSON parsing now uses `AIResponseParser.ParseJsonObjectFromResponse` for robust extraction from markdown code blocks (e.g. fenced ```json blocks), prefatory text, and trailing content via brace-depth tracking. Previously failed when the model wrapped output in fences alongside other content.

- **Runtime Messages**: Fixed error messages disappearing after state transitions due to premature list clearing

- **`AI2Boolean`, `AI2Integer`, `AI2Number`**: Added optional `Fallback` (F) input and `Used Fallback` (UF) boolean output. When the AI response cannot be parsed, the component emits the user-supplied fallback (or `null` if none) and `UsedFallback = true`. Parsing is delegated to the centralized `BooleanResultResolver` / `IntegerResultResolver` / `NumberResultResolver` utilities to keep behavior aligned with the legacy `text2boolean` tool.
- **`AI2Json`**: Added optional `Fallback` (F) text input (parsed as JSON object or array) and `Used Fallback` (UF) boolean output. Extraction now delegates to `JsonResultResolver.Resolve`, which handles markdown code-block wrapping (` ```json … ``` `), prefatory text, and brace-depth recovery — bringing parity with the `text2json` tool's robustness. Replaces the previous `JsonFormatHelper.JsonToString` extractor.

- **`AI2*` output components**: Updated `UsingAiTools` references to current tool names after the v2 rename:
  - `AI2Text`, `AI2Boolean`, `AI2Number`, `AI2Integer`, `AI2Markdown`: `text_generate` → `text2text`
  - `AI2Json`: `text_generate` → `text2json`
  - `AI2Img`: `img_generate` → `text2img`
  - `AI2Script`: `script_new` → `script_generate`

### Removed

- **`boolean_classify` AI tool**: Removed orphaned tool (`SmartHopper.Core.Grasshopper/AITools/boolean_classify.cs`) and its companion `BooleanClassificationResolver` utility. Boolean classification is now handled directly by `AI2BooleanComponent` via `text2text` + local response parsing, removing the unused tool indirection.
- **`integer_extract`, `number_extract`, `markdown_format` AI tools**: Removed orphaned tools with no consumers. Their roles are covered by the new `AI2Integer`, `AI2Number`, and `AI2Markdown` output components, which use `text2text` directly with local response parsing via `AIResponseParser`.
- **`img_generate` AI tool**: Removed legacy tool — superseded by `text2img` (renamed in this release per BREAKING CHANGES). All image generation components now consume `text2img`.

## [1.4.2-beta] - 2026-04-15

Many thanks to the following contributors to this release:

- [marc-romu](https://github.com/marc-romu)

----

### Added

- ci(main-sync-to-dev): new workflow `.github/workflows/main-sync-to-dev.yml` that, on pushes to `main` (or manual dispatch), auto-opens/reuses a PR from `main` into `dev` and into every `dev-*` stabilization branch. For `dev` the PR is a plain `main → dev`. For each `dev-*`, the workflow maintains a `sync/main-to-<dev-*>` branch onto which it **cherry-picks only allow-listed files** from `main` (any change under `.github/`, `.windsurf/`, `.githooks/`, `hashes/`, plus *modifications* — not add/rename/delete — to existing `src/SmartHopper.Providers.*/*ProviderModels.cs`). Non-allow-listed files (feature source, docs, `CHANGELOG.md`, etc.) stay on `main` only, so a mixed commit on `main` still propagates its infra/model parts to stabilization lines; use `patch-propagate.yml` for targeted backports of the rest. Reuses an existing open PR per target instead of creating duplicates, and skips entirely when there is no effective allow-listed diff.
- Community model verification flow:
  - New issue template `.github/ISSUE_TEMPLATE/model-verification.yml` with tests grouped by location ("Components on the Grasshopper canvas" — `AITextGenerate`, `AITextListGenerate`, `AIImgToText`, `AIImgGenerate`, audio — and "Chat interface" — streaming, ToolChat/FunctionCalling, Reasoning, multi-turn `ConversationSession`), each test specifying the **exact prompt** to use and the expected behavior. The template also embeds a copy-paste codeblock (with a `/verify-confirm` header and a hidden `<!-- model-verification-confirm -->` marker) for additional verifiers to use as their certification comment.
  - New workflow `.github/workflows/model-verification.yml` that triggers only when an issue comment starts with `/verify-confirm` (and contains the template marker) or `/verify-force`, tallies distinct GitHub users (issue author + valid `/verify-confirm` commenters), and opens a PR promoting the model to `Verified = true` once two distinct users have certified it. `/verify-force` is restricted to `OWNER`/`MEMBER`/`COLLABORATOR`.
  - New helper `tools/Update-ModelVerified.ps1` that locates the matching `new AIModelCapabilities { Model = "..." }` block in `src/SmartHopper.Providers.<Provider>/<Provider>ProviderModels.cs` and flips `Verified = false` to `Verified = true` (or inserts the flag when missing).

- Automated provider model discovery CI (OpenRouter as single source of truth):
  - New workflow `.github/workflows/chore-update-provider-models.yml` that runs weekly (Sundays 05:00 UTC) and on `workflow_dispatch`. It queries OpenRouter's unified `/models` endpoint for each supported provider, compares the returned metadata with the static declarations in `*ProviderModels.cs`, and opens a PR that both auto-inserts new models and marks disappeared/expiring models as `Deprecated = true`.
  - New composite action `.github/actions/ai/fetch-models/action.yml` that invokes `tools/Update-ProviderModels.ps1` with an OpenRouter API key, passing the provider name and `update-file` flag.
  - New PowerShell tool `tools/Update-ProviderModels.ps1` invoked by the workflow.  Accepts `-Provider`, `-ApiKey` (OpenRouter key), and an optional `-TargetFile`.  It queries OpenRouter, filters by provider prefix, maps `architecture.input_modalities`/`output_modalities` and `supported_parameters` to `AICapability` flags, auto-generates full `AIModelCapabilities` blocks for new models (with `ContextLimit`, `Verified=false`), and marks models with `expiration_date` < 1 year or absent from OpenRouter as `Deprecated = true`.  `Rank` values are auto-computed from OpenRouter `created` timestamp (newer models rank higher) and output pricing (cheapest first, considering `pricing.completion`, `pricing.image`, and `pricing.audio_output`).  Existing model capabilities, context limits, and ranks are refreshed on every run.  Emits a structured JSON report containing `newModels`, `deprecatedModels`, and `unchangedModels`.

### Changed

- **Infrastructure**: Migrated critical fixes including provider stability improvements, timeout policy refinements, and streaming adapter fixes
- **Thread Safety**: `ProviderManager` now uses `ConcurrentDictionary` for all provider collections to improve concurrent access safety
- **Code Quality**: Applied consistent code style with `this.` qualifiers and `ConfigureAwait()` patterns across Infrastructure and Providers

### Fixed

- `ProviderManager` now exposes `IsInfrastructureReady` flag to signal when provider infrastructure initialization completes
- All AI providers (Anthropic, DeepSeek, MistralAI, OpenAI, OpenRouter) received stability improvements and extended known list of models

### Deprecated

- **Anthropic**: marked deprecated `claude-opus-4-5`, `claude-sonnet-4-5`, `claude-sonnet-4-5-20250929`, `claude-haiku-4-5`, `claude-haiku-4-5-20251001` (superseded by 4-6 / 4-7 series).
- **DeepSeek**: `deepseek-chat` and `deepseek-reasoner` flagged `Deprecated = true` (DeepSeek docs state both will be deprecated; they alias `deepseek-v4-flash` non-thinking/thinking modes).
- **OpenAI**: `gpt-4o-mini-tts` marked deprecated per OpenAI docs.

## [1.4.2-alpha] - 2026-03-14

Many thanks to the following contributors to this release:

- [marc-romu](https://github.com/marc-romu)

----

This release successfully passes macOS verification of provider hashes! This is the first macOS compatible release available through Yak/Rhino package manager. Thank you [nofcfy-fanqi](https://github.com/nofcfy-fanqi) for testing!

### Fixed

- Fixed `Id` missing GhJSON validation error in script tools.
- (automatically added) Fixes "Compatibility with Mac" ([#263](https://github.com/architects-toolkit/SmartHopper/issues/263)).

## [1.4.1-alpha] - 2026-03-09

Many thanks to the following contributors to this release:

- [marc-romu](https://github.com/marc-romu)
- [nofcfy-fanqi](https://github.com/nofcfy-fanqi)

----

### Added

- **Contributors Workflow**: Added automated GitHub workflow (`chore-update-contributors.yml`) to maintain the contributors section in CHANGELOG.md

### Changed

- **Provider Security & Verification**:
  - Replaced boolean "hard integrity check" with a three-tier mode (Soft/Hard/Strict) selectable in Providers settings; installs migrate automatically to the new modes.
  - SHA-256 hash verification now covers Windows and macOS with dual-runner CI hashes and DEBUG auto-switch to Soft Check for smoother local development.

- **UX & Components**:
  - Renamed `AIScriptGenerator` component to `AIScriptGenerate` for consistent AI script naming.
  - Tuned dialog sizing, text wrapping, and integrity-check descriptions for clearer messaging.

- **Tooling & Automation**:
  - Enhanced `Change-SolutionVersion.ps1` with explicit version parsing and help output for reliable version bumps.
  - Expanded `.gitignore` to exclude all local libraries (beyond Rhino).
  - Improved pre-commit hook with selective staging and safer password handling, added post-commit hook to auto-update `InternalsVisibleTo`, and added a GitHub workflow to anonymize the public key on protected branches.

### Fixed

- fix(ci): skip `Update-InternalsVisibleTo` step on macOS runners since `sn.exe` (Strong Name tool) is Windows-only; assemblies still strong-name signed with SNK file
- fix(infrastructure): reduce provider hash verification timeout from 10s to 5s for faster offline detection and improved Settings dialog responsiveness
- fix(infrastructure): add network availability check in `ProviderHashVerifier` to skip hash fetch attempts when offline, preventing unnecessary delays
- fix(infrastructure): implement 15-minute manifest caching in `ProviderHashVerifier` using `ConcurrentDictionary` for thread safety, and centralize cache operations in `ReadHashManifest` method
- fix(core): eliminate race condition in `ComponentStateManager.ProcessTransitionQueue()` where `isTransitioning` flag was cleared before event firing, potentially allowing concurrent queue processing on macOS. The flag now remains true until after all events are fired, preventing out-of-order event processing and concurrent event handler execution.
- fix(tools): set GhJSON component `Id = 1` when `InstanceGuid` is null in `script_generate` and `script_edit` to satisfy GhJSON.Core validation requiring at least one identifier
- fix(tools): add `ParseJsonObjectFromResponse` to handle AI responses wrapped in markdown code blocks or non-JSON formatting in `script_generate` and `script_edit`
- fix(infrastructure): improve `AIProvider.CallApi()` error messages for non-JSON API responses (e.g., HTML error pages from proxies)
- fix(infrastructure): GitHub Pages deployment now correctly places `latest.json` and `versions.json` in the `hashes/` subdirectory instead of site root, fixing 404 errors when ProviderHashVerifier and the web UI attempt to fetch manifest files
- fix(macOS): address mac compatibility issues (deadlock risk, GhJSON validation, and JSON parsing edge cases) tracked in [#389](https://github.com/architects-toolkit/SmartHopper/issues/389)
- fix: additional stability and compatibility fixes tracked in [#395](https://github.com/architects-toolkit/SmartHopper/issues/395) and [#393](https://github.com/architects-toolkit/SmartHopper/issues/393)

## [1.4.0-alpha] - 2026-02-15

Many thanks to the following contributors to this release:

- [nofcfy-fanqi](https://github.com/nofcfy-fanqi) **First contribution!**
- [nof2504](https://github.com/nof2504) **First contribution!**
- [marc-romu](https://github.com/marc-romu)

### Added

- **Provider Hash Verification**: Added SHA-256 hash verification system for AI provider DLLs to enhance security on all platforms
  - New "Verify Providers Hash" menu item in SmartHopper menu to manually verify provider integrity
  - Comprehensive verification dialog showing verification status, local vs expected hashes, and detailed results
  - Automatic hash generation during release workflow with public hash repository for verification
  - Multi-tier verification with graceful degradation when hashes are unavailable
- **Enhanced About Dialog**: Improved About dialog to automatically display current SmartHopper version and platform information using the new VersionHelper class

### Fixed

- **macOS Compatibility**: Improved cross-platform compatibility for macOS users
  - Provider loading now works on non-Windows platforms with appropriate security warnings (skip Authenticode signature verification where `X509Certificate.CreateFromSignedFile` is not supported)
  - URL handling fixed to prevent incorrect file:// URI generation by restricting `BuildFullUrl` absolute URI detection to HTTP/HTTPS schemes
  - Component state management updated to fire `ComponentStateManager` transition events outside `stateLock` to prevent deadlocks caused by re-entrant lock acquisition in event handlers
- **Settings**:
  - Fixed first initialization is created using EncryptationVersion 2 by default which stores a local hash for secrets encryptation

### Security

- Enhanced provider security with SHA-256 hash verification system to protect against tampered provider DLLs
- Platform-appropriate security measures: Authenticode + hash verification on Windows, only hash verification on macOS

### Known Issues

- bug(ui): `WebChatDialog` (CanvasButton chat window) crashes on macOS with `NSInvalidArgumentException` because Eto.Forms' `WKWebViewHandler.LoadHtml()` calls `WKWebView.LoadFileUrl()` with an `https://` base URI (`https://smarthopper.local/`), which only accepts `file://` URLs

## [1.3.0-alpha] - 2024-02-08

### Changed

- Added annual automation to update copyright years in `src/**/*.csproj` and normalize C# license headers (workflow: `chore-update-copyright-year.yml`, script: `tools/Update-CopyrightYear.ps1`).
- GhJSON API Simplification:
  - Refactored all AI tools to use organized ghjson-dotnet façade classes exclusively, removing deep namespace dependencies.
  - All SmartHopper code now imports only `GhJSON.Core` and `GhJSON.Grasshopper` (no `GhJSON.Core.Models.*`, `GhJSON.Grasshopper.Serialization.*`, etc.).
  - Removed legacy `ScriptParameterSettingsParser.cs` from SmartHopper (now in ghjson-dotnet façade).
  - **Serialization options** now use `GhJsonGrasshopper.Options.Standard()`, `.Optimized()`, and `.Lite()` factory methods.
  - **Script components** now use `GhJsonGrasshopper.Script.CreateGhJson()`, `.GetComponentInfo()`, `.DetectLanguageFromGuid()`, `.NormalizeLanguageKeyOrDefault()`.
  - **Document operations** now use `GhJson.CreateDocument()`, `GhJson.Merge()`, `GhJson.Parse()`, `GhJson.Fix()`, `GhJson.IsValid()`, `GhJson.Serialize()`.
  - **Runtime data** extraction now uses `GhJsonGrasshopper.ExtractRuntimeData()` instead of deep serializer access.
  - Tool-specific changes:
    - `gh_get`: Delegates connection depth expansion and connection trimming to `GhJsonGrasshopper.GetWithOptions()`; uses `GhJsonGrasshopper.Options.*()` factories and `GhJsonGrasshopper.ExtractRuntimeData()`.
    - `gh_put`: Delegates GhJSON placement to `GhJsonGrasshopper.Put()` and uses `PutOptions.PreserveExternalConnections` for edit-mode external wiring preservation; uses `GhJson.Parse()`, `GhJson.Fix()`, `GhJson.IsValid()`.
    - `gh_merge`: Uses `GhJson.Merge()` façade instead of direct `GhJsonMerger` access.
    - `gh_tidy_up`: Uses `GhJsonGrasshopper.Options.Standard()`.
    - `script_edit`, `script_generate`: Use `GhJsonGrasshopper.Script.*` façade methods.
    - `gh_connect`: Delegates canvas wiring to `GhJsonGrasshopper.ConnectComponents()`.
- Changed `ISelectingComponent` to use `IGH_DocumentObject` instead of `IGH_ActiveObject` to support scribble selection.

## [1.2.4] - 2024-02-08

### Changed

- Bump to stable version

## [1.2.4-alpha] - 2026-01-11

### Fixed

- AIStatefulAsyncComponentBase: Fixed issue where `context_usage_percent` was not being set correctly.

## [1.2.3-alpha] - 2026-01-11

### Added

- Context Management:
  - Added `ContextLimit` property to `AIModelCapabilities` for storing model context window sizes across all providers (Anthropic, DeepSeek, OpenAI, OpenRouter, MistralAI).
  - Added `SummarizeSpecialTurn` factory for creating conversation summarization special turns.
  - Added automatic context tracking in `ConversationSession` with percentage calculation.
  - Added pre-emptive summarization when context usage exceeds 80% of model limit.
  - Added context exceeded error detection and automatic summarization with retry.
  - Added graceful error handling when summarization fails to reduce context size.
  - Added context limits for all MistralAI models (128K for most, 40K for Magistral, 32K for Voxtral).
- Metrics:
  - Added `LastEffectiveTotalTokens` field to `AIMetrics` for accurate context usage percentage calculation.
- WebChat Debug:
  - Added debug Update button to refresh chat view from conversation history.
  - Added DOM synchronization functionality to compare cached HTML hashes and update only changed messages.
- GhJSON canvas tools:
  - Added `gh_get_start` / `gh_get_start_with_data` tools to retrieve start nodes (components with no incoming connections) with optional runtime data.
  - Added `gh_get_end` / `gh_get_end_with_data` tools to retrieve end nodes (components with no outgoing connections) with optional runtime data, providing a wide view of definition outputs.

### Changed

- Context Management:
  - Conversation summaries now use `AIAgent.Summary` instead of `AIAgent.Assistant`, preventing extra assistant messages in chat UI.
  - Providers automatically merge `Summary` interactions with the system prompt using format: `System prompt\n---\nThis is a summary of the previous conversation:\n\nSummary`.
  - WebChat UI renders `Summary` interactions as collapsible elements with distinct blue styling, similar to tool/system messages.
- GhJSON Serialization:
  - `PersistentData` and `VolatileData` properties are now excluded from serialization at the `GhJsonSerializer` level to prevent encrypted/binary strings from being included in GhJSON output, ensuring LLM-safe and token-efficient results by default.
  - Runtime data remains available via the separate `ExtractRuntimeData` method for `_with_data` tools (e.g., `gh_get_selected_with_data`).
- Debug Logging:
  - Updated `ConversationSession` debug history file to preserve previous conversations when summarization occurs.
  - Added `SUMMARIZED` marker in debug logs to clearly separate pre-summary and post-summary history.

### Fixed

- Context Management:
  - Fixed context usage percentage not appearing consistently in debug logs and WebChat UI metrics by caching it at the `ConversationSession` level and applying it to aggregated metrics.
- GhJSON canvas helpers and chat:
  - Standardized node classification terminology from `input/output/processing/isolated` to `startnodes/endnodes/middlenodes/isolatednodes` across `gh_get` filters and the `GhGetComponents` Grasshopper component UI.
  - Updated `instruction_get` canvas guidance to recommend `gh_get_start`/`gh_get_end` (and their `_with_data` variants) for obtaining a wide view of data sources and outputs.
  - Strengthened the `CanvasButton` default system prompt to always call `instruction_get` for canvas queries, improving tool selection for providers like `mistral-small`.
- GhJSON Serialization:
  - Fixed `gh_get` tool incorrectly using `Optimized` serialization context (which excludes `PersistentData`) instead of `Standard` context, breaking `gh_put` restoration of internalized parameter values. Now uses `Standard` format by default to preserve all data needed for restoration, only switching to `Optimized` when runtime data is explicitly included (where persistent values are redundant).

## [1.2.2-alpha] - 2025-12-27

### Added

- Chat:
  - Added `instruction_get` tool (category: `Instructions`) to provide detailed operational guidance to chat agents on demand.
  - Simplified the `CanvasButton` default assistant system prompt to reference instruction tools instead of embedding long tool usage guidelines.

### Changed

- Infrastructure:
  - Extracted duplicated streaming processing logic into shared `ProcessStreamingDeltasAsync` helper method in `ConversationSession`, reducing code duplication by ~80 lines.
  - Added `GetStreamingAdapter()` to `IAIProvider` interface with caching in `AIProvider` base class, replacing reflection-based adapter discovery.
  - Added `CreateStreamingAdapter()` virtual method for providers to override; updated OpenAI, DeepSeek, MistralAI, Anthropic, and OpenRouter providers.
  - Added `NormalizeDelta()` method to `IStreamingAdapter` interface for provider-agnostic delta normalization.
  - Simplified streaming validation flow in `WebChatDialog.ProcessAIInteraction()` - now always attempts streaming first, letting `ConversationSession` handle validation internally.
  - Added `TurnRenderState` and `SegmentState` classes to `WebChatObserver` for encapsulated per-turn state management.
  - Reduced idempotency cache size from 1000 to 100 entries to reduce memory footprint.
  - Promoted `StatefulComponentBaseV2` to the default stateful base by renaming it to `StatefulComponentBase`.
- Chat UI:
  - Optimized DOM updates with a keyed queue, conditional debug logging, and template-cached message rendering with LRU diffing to cut redundant work on large chats.
  - Refined streaming visuals by removing unused animations and switching to lighter wipe-in effects, improving responsiveness while messages stream.

### Fixed

- DeepSeek provider:
  - Fixed `deepseek-reasoner` model failing with HTTP 400 "Missing reasoning_content field" error during tool calling. The streaming adapter was not propagating `reasoning_content` to `AIInteractionToolCall` objects, causing the field to be missing when the conversation history was re-sent to the API.
  - Fixed duplicated reasoning display in UI when tool calls are present. Reasoning now only appears on tool call interactions (where it's needed for the API), not on empty assistant text interactions.

- Chat UI:
  - Fixed user messages not appearing in the chat UI. The `ConversationSession.AddInteraction(string)` method was not notifying the observer when user messages were added to the session history.

- Tool calling:
  - Improved `instruction_get` tool description to explicitly mention required `topic` argument. Some models (MistralAI, OpenAI) don't always respect JSON Schema `required` fields but do follow description text.

- Chat UI:
  - Reduced WebChat dialog UI freezes while dragging/resizing during streaming responses by throttling DOM upserts more aggressively and processing DOM updates in smaller batches.
  - Mitigated issue [#261](https://github.com/architects-toolkit/SmartHopper/issues/261) by batching WebView DOM operations (JS rAF/timer queue) and debouncing host-side script injection/drain scheduling.
  - Reduced redundant DOM work using idempotency caching and sampled diff checks; added lightweight JS render perf counters and slow-render logging.
  - Improved rendering performance using template cloning, capped message HTML length, and a transform/opacity wipe-in animation for streaming updates.
  - Further reduced freezes while dragging/resizing by shrinking update batches and eliminating heavy animation paths during active user interaction.

- Context providers:
  - Fixed `current-file_selected-count` sometimes returning `0` even when parameters were selected by reading selection on the Rhino UI thread and adding a robust `Attributes.Selected` fallback.
  - Added selection breakdown keys: `current-file_selected-component-count`, `current-file_selected-param-count`, and `current-file_selected-objects`.

## [1.2.1-alpha] - 2025-12-07

### Added

- Script tools:
  - Added `script_generate_and_place_on_canvas` wrapper tool that combines `script_generate` and `gh_put` in a single call, reducing token consumption by eliminating the need for the AI to call both tools separately.
  - Moved `script_generate` tool to `Hidden` category (only `script_generate_and_place_on_canvas` is now visible to chat agents).
- `gh_get` tools:
  - Added `gh_get_selected_with_data` tool that returns selected components with their runtime/volatile data (actual values flowing through outputs).
  - Added `gh_get_by_guid_with_data` tool that returns specific components by GUID with their runtime/volatile data.
  - Added `includeRuntimeData` parameter to the base `gh_get` tool for optional runtime data extraction.
  - Runtime data includes total item count, branch structure, and sample values for each parameter output.
  - Added `gh_get_errors_with_data` tool that returns only errored components with their runtime/volatile data, useful for debugging broken definitions.
- `gh_put` tool:
  - Added `instanceGuids` array to the tool result containing the actual GUIDs of placed components (useful for subsequent queries).

### Fixed

- Script tools:
  - Fixed `script_generate_and_place_on_canvas` returning incorrect `instanceGuid`. The tool was returning the in-memory GUID from `script_generate` instead of the actual GUID assigned by Grasshopper when the component was placed on canvas. Now returns the real `instanceGuid` from `gh_put` result.
- GhJSON validation:
  - Fixed `GHJsonAnalyzer.Validate` to treat missing `connections` property as an empty array instead of an error. Components without connections are now valid and won't trigger "'connections' property is missing or not an array" errors.
- Chat UI:
  - Fixed critical bug where two identical user messages were collapsed into a single message in the UI. The root cause was that user messages didn't have a unique `TurnId`, causing identical messages to generate the same dedup key and replace each other. Now each user message receives a unique `TurnId` via `InteractionUtility.GenerateTurnId()`.
- Conversation session:
  - Fixed TurnId inconsistency where `ToolResult` interactions were getting new TurnIds instead of inheriting from their originating `ToolCall`. The conditional check `if (string.IsNullOrWhiteSpace(toolInteraction.TurnId))` was never true because `AIBodyBuilder.EnsureTurnId()` had already assigned a new TurnId during tool execution. Changed to unconditional assignment to ensure correct turn-based metrics aggregation.

## [1.2.0-alpha] - 2025-12-06

### Added

- Dialog canvas link visualization:
  - Added `DialogCanvasLink` utility class that draws a visual connection line from a dialog to a linked Grasshopper component, similar to the script editor anchor.
  - `StyledMessageDialog` methods (`ShowInfo`, `ShowWarning`, `ShowError`, `ShowConfirmation`) now accept optional `linkedInstanceGuid` and `linkLineColor` parameters to enable canvas linking.
  - When a dialog is linked to a component, a bezier curve with an anchor dot is drawn from the component to the dialog window.
- Component replacement mode:
  - Added "Edit Mode" input parameter to `GhPutComponents` for component replacement functionality.
  - When Edit Mode is enabled and GhJSON contains valid instanceGuids that exist on canvas, users are prompted via `StyledMessageDialog` to choose between replacing existing components or creating new ones.
  - The `gh_put` AI tool now accepts an optional `editMode` parameter to support component replacement.
  - Replacement components preserve original `InstanceGuid` and exact canvas position.
  - Undo support included for component replacement operations.
- GhJSON helpers:
  - Added `GhJsonHelpers` utility class with methods for applying pivots and restoring InstanceGuids on deserialized components.
- GhJSON merge:
  - Added `GhJsonMerger` utility to merge two GhJSON `GrasshopperDocument` instances, with the target document taking priority on component GUID conflicts and automatic ID remapping for connections and groups.
  - Introduced `gh_merge` AI tool to merge two arbitrary GhJSON strings using `GhJsonMerger`, returning the merged GhJSON together with merge statistics (components/connections/groups added and deduplicated).
  - Added `GhMergeComponents` Grasshopper component ("Merge GhJSON") to merge two GhJSON documents directly on the canvas, exposing merged GhJSON and basic merge counters as outputs.
- Script tools:
  - Introduced GhJSON-based AI tools `script_generate` and `script_edit` for Grasshopper script components.
  - All script tools now validate GhJSON input/output via `GHJsonAnalyzer.Validate` and use `ScriptComponentFactory` for component construction.
  - Added `script_edit_and_replace_on_canvas` wrapper tool that combines `script_edit` and `gh_put` in a single call, reducing token consumption by eliminating the need for the AI to call both tools separately.
  - Enhanced `script_generate` and `script_edit` tools to support all parameter modifiers: `dataMapping` (Flatten/Graft), `reverse`, `simplify`, `invert`, `isPrincipal`, `required`, and `expression` for inputs; `dataMapping`, `reverse`, `simplify`, `invert` for outputs.
  - Added `ScriptCodeValidator` utility for detecting non-RhinoCommon geometry libraries in AI-generated scripts (e.g., System.Numerics, UnityEngine, numpy, shapely) with automatic self-correction prompts.
  - Enhanced `script_generate` and `script_edit` system prompts with explicit RhinoCommon geometry requirements and language-specific guidance (Python/IronPython/C#/VB.NET) including import templates and type mappings.
  - Added validation retry loop to `script_generate` and `script_edit` that detects invalid geometry patterns and re-prompts the AI with correction instructions (up to 2 retries).
  - Clarified that output parameters do NOT have 'access' settings and documented proper list output patterns per language (Python 3 requires .NET `List[T]`, IronPython can use Python lists, C# uses `List<T>`, VB.NET uses `List(Of T)`).
- `gh_get` tool:
  - Added `categoryFilter` parameter and extended category-based filtering from components to all document objects.
- Model compatibility badges:
  - Added "Not Recommended" badge (orange octagon with exclamation mark) displayed when the selected model is discouraged for the AI tools used by a component.
  - Added `DiscouragedForTools` property to `AIModelCapabilities` to specify tool names for which a model is not recommended.
  - Added `UsingAiTools` property to `AIStatefulAsyncComponentBase` allowing components to declare which AI tools they use.
  - Added `EffectiveRequiredCapability` property that merges component's required capability with capabilities required by its AI tools.
  - The "Not Recommended" badge suppresses the "Verified" badge when active (priority: Replaced > Invalid > NotRecommended > Verified).

### Changed

- Components UI:
  - AI-selecting stateful components now use combined attributes that show both the "Select" button and AI provider badges, with the button rendered above the provider strip.
  - Selecting components now use the dialog link line color for hover highlights and draw a connector line from the combined selection center to the "Select" button.
- Script components:
  - `AIScriptGenerateComponent` now orchestrates `script_generate` / `script_edit` together with `gh_get` / `gh_put` instead of the legacy `script_generator` tool, and exposes `GhJSON`, `Guid`, `Summary`, and `Message` outputs only.
  - `AIScriptGenerateComponent` and `AIScriptReviewComponent` no longer expose a `Guid` input; the target component is always provided via the selecting button.
  - Removed the monolithic `script_generator` AI tool in favor of smaller, focused tools that operate purely on GhJSON.
  - Updated `AIScriptGenerateComponent` and `AIScriptReviewComponent` to support processing multiple inputs in parallel.
  - Renamed `script_fix` tool to `script_review` to better reflect its review-focused behavior.
  - `script_generate` no longer includes a pre-placement `instanceGuid` in its tool result; instance GUIDs are only exposed via `script_generate_and_place_on_canvas` / `gh_put` using the real canvas instance GUIDs.
- `GhJsonDeserializer`:
  - Changed deserialization logic to default the UsingStandardOutputParam property to true when ShowStandardOutput is not present in the GhJSON ComponentState.
- Providers:
  - Added new Claude Opus 4.5 model to the Anthropic provider registry.
  - OpenRouter provider: added structured output support via `response_format: json_schema` / `structured_outputs` for JsonOutput requests and now populates `finish_reason` and `model` in metrics for chat completions.
- Script parameter modifier tools:
  - Moved all script parameter modifier AI tools to the `NotTested` category to clarify their experimental status.
- Component icons:
  - Updated McNeel forum and script component icons to outlined variants for better visual consistency.
  - Added `ghmerge` icon and refreshed `ghget` / `ghput` icons to align with the new GhJSON merge workflows.
- Canvas button:
  - Improved the default SmartHopper assistant prompt used by `CanvasButton` to guide users toward in-viewport scripting workflows and avoid unnecessary external code blocks or testing patterns, resulting in a smoother first-time UX.
- Script tools:
  - `script_review` now augments its system prompt with language-specific Grasshopper scripting guidance (Python/IronPython/C#/VB.NET) via `ScriptCodeValidator`, based on the detected script language.
  - Centralized script language normalization in `ScriptComponentFactory.NormalizeLanguageKeyOrDefault` and wired `script_generate` to use it when building prompts, ensuring consistent handling of language keys such as "python3" and "csharp".

### Fixed

- Chat UI metrics display:
  - Fixed metrics aggregation to show total token consumption per turn (from user message to next user message) instead of just the last message's metrics.
  - Added `GetTurnMetrics(turnId)` method to `ConversationSession` to aggregate metrics for all interactions in a turn.
  - Fixed tool results not inheriting TurnId from their corresponding tool calls, which caused incorrect turn grouping.
- Web chat dialog visibility:
  - `WebChatDialog` is now created as an owned tool window of the Rhino main Eto window and hidden from the taskbar, so it follows Rhino/Grasshopper focus and stays on top of Rhino while the application is active.
  - Confirmation dialogs shown via `StyledMessageDialog` (for example when replacing components with `gh_put` in edit mode) still appear above the chat dialog, but closing them no longer leaves the chat window hidden behind other Rhino/Grasshopper windows.
- `gh_put` tool:
  - Fixed infinite loop when using `GhPutComponents` with replacement mode. The `NewSolution` call inside the tool caused re-entrancy when the component blocked with `.GetAwaiter().GetResult()`, which pumps Windows messages and allows the new solution to start immediately.
  - Fixed "object expired during solution" error when replacing components. Removed the document disable/enable logic which was causing components to be in an invalid state. Uses `IsolateObject()` to properly clean up connections before component removal.
- Script tools:
  - Updated `script_generate` and `script_edit` tool schemas to require `nickname`, fixing crashes with OpenAI structured-output mode.

## [1.1.1-alpha] - 2025-11-24

### Changed

- Providers:
  - Added explicit Claude 3.x/4.x dated model identifiers to the Anthropic provider registry while keeping shorthand model names.
  - Switched the default Anthropic text/tool/json model to `claude-haiku-4-5`.
  - Added structured-output beta support for Anthropic Sonnet 4.5 / Opus 4.1 models and restricted Text2Json capability to those models.
  - Updated OpenAI provider models to include GPT-5.1 series models.

## [1.1.0-alpha] - 2025-11-23

### Added

- **VB Script support**: VB Script components now serialize and deserialize their three code sections, custom parameters, and settings correctly.
- **Parameter and script AI tools**: Added `gh_parameter_*` and `script_parameter_*` tools to flatten, graft, simplify, reverse, and manage script/component parameters via AI.
- **McNeel forum tools**: Enhanced `mcneel_forum_search`, `mcneel_forum_post_get`, and added `mcneel_forum_post_summarize` for AI-powered forum summaries.
- **Knowledge components**: New components for McNeel forum and web sources to search, retrieve, and summarize content directly in Grasshopper.
- **Web page reading**: `web_generic_page_read` now produces clean text for Wikipedia, Discourse, GitHub/GitLab files, and Stack Exchange.
- **Property management**: Introduced a new property-management system for more reliable GhJSON serialization of component parameters, values, and metadata.
- **GhJSON optimization**: Reduced irrelevant data in serialized Grasshopper documents, producing smaller, cleaner JSON.
- **GhJSON schema v2**: Improved component schema with consolidated `value` properties and extended parameter settings. This is now the default format for `gh_get` and `gh_put` and may affect saved GhJSON files.
- **Component generation and wiring**: New `gh_generate` and `gh_connect` AI tools to create component specifications and wire components automatically.
- **Script generator**: Added `script_generator` AI tool to create or edit Grasshopper script components from natural-language instructions.
- **Rhino 3DM analysis**: New `rhino_read_3dm` and `rhino_get_geometry` AI tools to analyze .3dm files and extract geometry from the active document.
- **Selection persistence**: Components based on `SelectingComponentBase` now remember their selected objects when saving and loading Grasshopper files.
- **CI/CD**: Added hotfix workflow system and release workflow documentation for emergency and regular releases.

### Changed

- **Renamed AI tools**: `gh_toggle_preview`, `gh_toggle_lock`, `gh_lock_selected`, `gh_unlock_selected`, `gh_hide_preview_selected`, and `gh_show_preview_selected` now use the `gh_component_*` prefix.
- **Script component output**: Script components no longer serialize the standard `out` parameter as a regular output; its visibility is controlled by a new `showStandardOutput` property to keep signatures stable.
- **JSON serialization**: Simple property values now serialize directly, empty strings are omitted, and document metadata includes schema version, Rhino version, and plugin dependencies.
- **Model validation**: Unregistered model names are now accepted, letting you use any provider model even if it is not yet in the built-in registry.
- **Error handling**: Errors from AI calls and tool execution are now surfaced more consistently in `AIReturn` and `ConversationSession`.
- **AI tool descriptions**: Improved descriptions and added specialized wrapper tools for common queries and actions.
- **CI/CD**: Extended PR validation and test workflows to cover `hotfix/**` and `release/**` branches; removed the obsolete `user-security-patch.yml` workflow.

### Removed

- **Legacy property manager**: Removed the old `PropertyManager` and migrated serialization/placement to the new `PropertyManagerV2`.
- **Legacy script tools**: Replaced `script_new` and `script_edit` with the unified `script_generator` tool; removed the corresponding `AIScriptNewComponent` and `AIScriptEditComponent` components.

### Fixed

- **Data tree processing**: Fixed `GetBranchFromTree` and `BranchFlatten` to handle single-path and flat trees correctly.
- **Model badges**: Invalid-model badge now appears correctly when a provider has no capable model instead of showing a replacement badge ([#332](https://github.com/architects-toolkit/SmartHopper/issues/332)).
- **Chat error display**: Provider errors such as HTTP 400 and token-limit exceeded now surface correctly in the WebChat UI ([#334](https://github.com/architects-toolkit/SmartHopper/issues/334)).
- **Rectangle serialization**: Rectangle3d round-trips correctly using a center-based format.
- **JSON round-trip fixes**: Improved serialization/deserialization for stand-alone parameters, persistent data, parameter modifiers, group InstanceGuids, and component connections (including index-based matching).
- **Script component stability**: Fixed null-reference errors in `gh_get` for script components and preserved parameter modifiers during round-trip serialization.
- **Smart placement**: `gh_put` now preserves relative pivots when present or auto-layouts components with `DependencyGraphUtils` when absent.
- **Type hints**: Default/generic "object" type hints are omitted, and generic type hints (`DataTree<Object>`, `List<Curve>`) are handled without exceptions.
- **Script editor freeze**: Fixed the script editor freezing issue when using the script edit tool ([#209](https://github.com/architects-toolkit/SmartHopper/issues/209)).

## [1.0.1-alpha] - 2025-10-13

### Changed

- Model capability validation now bypasses checks for unregistered models, allowing users to use any model name even if not explicitly listed in the provider's model registry.
- Centralized error handling in AIReturn and tool calls.
- Accurately aggregate metrics in Conversation Session. Cases with multiple tool calls, multiple interactions, etc. Calculate completion time per interaction.
- Improved AI Tool descriptions with better guided instructions. Also added specialized wrappers for targeted tool calls (gh_get_selected, gh_get_errors, gh_get_locked, gh_get_preview_off, gh_get_preview_on, gh_get_by_guid, gh_lock_selected, gh_unlock_selected, gh_hide_preview_selected, gh_show_preview_selected, gh_group_selected, gh_tidy_up_selected).
- Enhanced `list_filter` tool prompts to explicitly distinguish between indices (positions/keys) and values (item content), and expanded capabilities to support filtering, sorting, reordering, selecting, and other list manipulation operations based on natural language criteria.
- Added more predefined models in the provider's database.

### Fixed

- Fixed model badge display: show "invalid model" badge when provider has no capable model instead of "model replaced" ([#332](https://github.com/architects-toolkit/SmartHopper/issues/332)) ([#329](https://github.com/architects-toolkit/SmartHopper/issues/329)).
- Fixed provider errors (e.g., HTTP 400, token limit exceeded) not surfacing to WebChat UI: `ConversationSession` now surfaces `AIInteractionError` from error AIReturn bodies to observers before calling `OnError`, ensuring full error messages are displayed in the chat interface ([#334](https://github.com/architects-toolkit/SmartHopper/issues/334)).
- Fixed `list_filter` tool automatically sorting and deduplicating indices, which prevented reordering and expansion operations from working correctly. Now preserves both order and duplicates as returned by the AI ([#335](https://github.com/architects-toolkit/SmartHopper/issues/335)).

## [1.0.0-alpha] - 2025-10-11

### Added

- **Canvas assistant button**: Optional top-right canvas button to open the SmartHopper assistant, respecting the `EnableCanvasButton` setting.
- **Document context**: New `FileContextProvider` exposes document and selection metadata to AI conversations.
- **Conversation orchestration**: New `ConversationSession` service supports multi-turn flows, tool passes, special turns, and policy hooks.
- **Tool validation**: Tool calls are validated against registered tools, JSON schemas, and model capabilities before execution.
- **Streaming support**: Shared streaming adapter with idle-timeout SSE reading, live reasoning display, and provider-specific completion detection for OpenAI, Anthropic, MistralAI, and DeepSeek.
- **New providers**: Added Anthropic and OpenRouter providers, plus refreshed provider icons.
- **Model badges**: Components show badges for verified, deprecated, incompatible, or auto-replaced models before running.
- **Capability-based model selection**: `ModelManager` selects the best model per capability and checks streaming support.
- **WebChat UX**: Improved chat UI with auto-scroll, collapsible messages, better prompts, and default context (time, environment, selection).
- **Core models**: Introduced `AIRequest`, `AIReturn`, `AIBody`, and related models for clearer request/response handling.
- **Documentation**: Added summary documentation under `docs/`.
- **Tests**: Added tests for Context Manager, Model Manager, and data-tree processing.

### Changed

- **Settings UI**: Reorganized settings dialog into tabs, including SmartHopper Assistant and Trusted Providers sections.
- **Model selection**: Centralized capability-first model selection behind the provider interface.
- **Authentication**: Improved API key encryption and migrated to provider-internal key resolution so keys never appear in requests or logs.
- **About dialog**: Updated to list the currently supported AI providers.
- **Provider settings**: Disabled streaming controls for DeepSeek and OpenRouter until streaming support is available.
- **AIChat component**: Unified snapshot management so chat history and metrics stay consistent.
- **Toolbox**: Reorganized Grasshopper component categories and disabled experimental tools from the default build.
- **mcneel_forum_search**: Simplified to return raw results; use `mcneel_forum_post_summarize` for summaries.
- **Infrastructure**: Refactored `SmartHopper.Infrastructure` for clarity and standardized data-tree processing pipelines.

### Fixed

- **Chat rendering**: Fixed tool-call/result ordering, duplicate greetings, and reasoning-only message display in WebChat.
- **Reasoning metrics**: Fixed missing reasoning tokens for DeepSeek, OpenAI o-series, and GPT-5 models.
- **Image viewer**: Fixed image saving errors and image generation output not reaching the viewer.
- **Model selection**: Fixed "Invalid model" when the manager returned a wildcard and corrected `DataCount` in metrics.
- **Tools**: Fixed `list_generate` output and corrected script component GUIDs.
- **Streaming**: Fixed indefinite streaming hangs and ensured final metrics appear in the chat UI.
- **Persistence**: Prevented crashes when opening Grasshopper files with safe, versioned output persistence.
- **Providers**: Fixed DeepSeek array schema handling, MistralAI streaming status, and Anthropic system message placement.
- **AIChat component**: Prevented NullReferenceException when closing chat without responses and fixed intermittent missing metrics.

### Deprecated

- `CustomizeHttpClientHeaders` is deprecated for authentication; use request-scoped headers instead.

### Removed

- Removed legacy per-provider model retrieval methods (`RetrieveAvailable`, `RetrieveCapabilities`, `RetrieveDefault`); use `RetrieveModels()`.
- Removed the `TemplateProvider`.
- Replaced `ContextKeyFilter` and `ContextProviderFilter` with a single `ContextFilter`.
- Removed manual greeting placeholder logic from WebChat.

### Security

- Prevented API key leakage by centralizing API key handling inside providers and keeping reserved headers out of request logs.

## [0.5.3-alpha] - 2025-08-20

### Fixed

- Fix incorrect json schema required fields in `script_new` tool ([#304](https://github.com/architects-toolkit/SmartHopper/issues/304)).

## [0.5.2-alpha] - 2025-08-12

### Fixed

- StackOverflowException on first run due to recursive lazy defaults in provider settings (`SmartHopperSettings.GetSetting`, `AIProvider.GetSetting<T>`), guarded with thread-static recursion checks.
- Readiness guard in `SmartHopperSettings.RefreshProvidersLocalStorage` to avoid partial refresh before all providers register settings UI.
- OpenAI and MistralAI providers now fall back to static model lists/capabilities on API errors or empty API responses, preventing empty model selections.

## [0.5.1-alpha] - 2025-07-30

### Added

- Settings parameter to enable/disable AI generated greeting in chat.

### Fixed

- Greeting generation was using stored settings models instead of the provider's default model. To solve it, now if `AIUtils.GetResponse` doesn't get a model, it will use the provider's default model.
- Components triggered with a Boolean Toggle (permanent true value) weren't calculating when the toggle was turned to true.
- Lazy default values in `AI Provider Settings` to prevent race conditions at initialization.
- Fixed "List length in list_generate was not met for long requests" ([#277](https://github.com/architects-toolkit/SmartHopper/issues/277)).

## [0.5.0-alpha] - 2025-07-29

### Added

- **Model Capability Management System**
  - Introduced `AIModelCapabilities` and `AIModelCapabilityRegistry` for centralized, persistent model capability tracking.
  - Added capability checking and filtering methods for models (e.g., `GetCapabilities`, `SetCapabilities`, `FindModelsWithCapabilities`).
  - Tool-specific capability validation now prevents execution with incompatible models.
  - Default model is now managed by the `AIModelCapabilityRegistry`. Multiple models can be defined as Default for a set of capabilities.
  - `AIStatefulAsyncComponentBase` will now try to use the default model if the specified model is not compatible with the tool.
- **Provider-Specific Capability Management**
  - MistralAI:
    - Added `MistralModelManager` for dynamic API-based capability detection and registration.
    - Models now update their capabilities by querying the `/v1/models/{model_id}` endpoint.
    - Automatic mapping of Mistral model features (chat, function calling, vision) to internal capability flags.
  - OpenAI & DeepSeek:
    - Static mapping for capabilities, with support for function calling, structured output, and image generation.
- **Image Generation Support**: Comprehensive AI image generation capabilities using OpenAI DALL-E models.
  - New `DefaultImgModel` property in `IAIProvider` interface for provider capability detection.
  - New `img_generate` AI tool with support for prompt, size, quality, and style parameters.
  - Enhanced `AIUtils.GenerateImage()` method with provider-agnostic image generation.
  - New `AIImgGenerateComponent` UI component in SmartHopper > Img category.
- Improvements in `AITools`:
  - New `includeSubcategories` parameter to `gh_list_categories` tool.
  - New `nameFilter`, `includeDetails` and `maxResults` parameters to `gh_list_components` tool.
  - New `ImageViewer` component to visualize output images on canvas and save them to disk.
- Added component existence and connection type validation to `GHJsonLocal`.
- **Settings management in AI Providers**:
  - New `SetSetting` method in `AIProvider` that let's providers set custom settings within the provider key.
  - New `RefreshCachedSettings` method in `AIProvider` to refresh their cached settings.

### Changed

- Renamed `AIProvider.InitializeSettgins` to `AIProvider.ResetCachedSettings`. Set visibility to `private`.

### Fixed

- `gh_put` now automatically fixes GhJSON.
- OpenAI tool filter not being applied properly.
- Fixed "Parsing error when output contains { }" ([#276](https://github.com/architects-toolkit/SmartHopper/issues/276)).

## [0.4.1-alpha] - 2025-07-23

### Added

- New `ProgressInfo` class to `StatefulAsyncComponentBase` to provide progress information to the UI. It allows to display a dynamic progress reporting which branch is being processed.

### Fixed

- Multiple fixes to `StatefulAsyncComponentBase`:
  - Fixed issue: Components now transition to "Done" state when opening files with existing results instead of "Run me!" ([#113](https://github.com/architects-toolkit/SmartHopper/issues/113))
  - Calculate changed inputs based on actual values, not on object instances, to prevent false positives when connecting new sources with same values.
  - Fixed issue: Stuck components when using Boolean toggle ([#260](https://github.com/architects-toolkit/SmartHopper/issues/260)).
  - Fixed issue: Output metrics not being set when using Boolean toggle.
- Fixed issue ([#208](https://github.com/architects-toolkit/SmartHopper/issues/208)): enabled compatibility with params in `gh_toggle_preview` tool.
- Fixed WebChatDialog not automatically closing when Rhino is closed.

## [0.4.0-alpha] - 2025-07-22

### Added

- New `RemoveLastMessage` method to `WebChatDialog` to remove messages from the chat history.
- Added GitHub Actions workflow for automatic milestone management, moves open issues/PRs to next appropriate milestone when a milestone is closed
- JSON wrapper in `OpenAI provider` to prevent passing incorrect JSON schemas to the API.
- JSON cleaner in `DeepSeek provider` to extract data from malformed responses with `enum` property.

### Changed

- Enhanced chat greeting with loading animation and improved model handling ([#255](https://github.com/architects-toolkit/SmartHopper/issues/255)), including:
  - New loading message while generating the greeting in `InitializeNewConversation`, with spinning animation.
  - Update `chat-script.js` with new function to remove messages.
  - Modified `AddMessageToWebView` to automatically add the loading class when finish reason from responses is "loading".
  - Modified `AIUtils.GetResponse` to use the default model if none is specified.
  - Modified `InitializeNewConversation` to use the default model for greeting generation (a fast and cheap model).
- Modified `WebChatDialog` constructor to pass the provider name to the base class.
- Modified the construction of `WebChatDialog` in `WebChatUtils.ShowWebChatDialog` to pass the provider name.
- Modified `GetModel` in `AIStatefulAsyncComponentBase` to use the provider's global model defined in settings if none is specified.
- Updated release workflow to automatically assign PRs to milestones
- Enhanced new-branch workflow with versioning guidance
- Using the `StripThinkTags` in all `DataProcessing` tools to avoid including reasoning text in the processed data.

### Fixed

- Fix incorrect model handling in `AIStatefulAsyncComponentBase`.
- Fixed certificate creation tests to handle CI environment constraints
- Updated `GhRetrieveComponents` to use the correct ai tool `gh_list_components` instead of `gh_get_available_components`
- Fixes "Missing required parameter: ‘response_format.json_schema' in text-list-generate with OpenAI provider" ([#259](https://github.com/architects-toolkit/SmartHopper/issues/259)).
- Fixes "Check structured output compatibility with models" ([#273](https://github.com/architects-toolkit/SmartHopper/issues/273)).

## [0.3.6-alpha] - 2025-07-20

### Added

- Added icon to `AIModelsComponent`

## [0.3.5-alpha] - 2025-07-19

### Added

- New methods in AIProvider base class:
  - Add DefaultServerUrl property
  - Added CallApi method to AIProvider base class supporting GET/POST/DELETE/PATCH
  - Added RetrieveAvailableModels method to AIProvider base class with default to empty list
- Implemented RetrieveAvailableModels, CallApi and DefaultServerUrl to existing providers (MistralAIProvider, OpenAIProvider, and DeepSeekProvider).
- New AIModelsComponent component under SmartHopper > AI categories that uses provider's RetrieveAvailableModels() to fetch model list.

### Changed

- Update providersResources access modifiers from public to internal
- Clean up AboutDialog by removing MathJax attribution
- Moved provider selection logic from AIProviderComponentBase to AIProviderComponentBase
- Moved InputsChanged method with override for including HasProviderChanged from AIStatefulAsyncComponentBase to AIProviderComponentBase

### Removed

- Removed MathJax support from chat UI since it was not properly implemented and was generating security warnings on GitHub.

## [0.3.4-alpha] - 2025-07-11

### Added

- Added `Instructions` input to `AIChatComponent` ([#87](https://github.com/architects-toolkit/SmartHopper/issues/87))
- Added `systemPrompt` parameter to `WebChatUtils.ShowWebChatDialog`
- Context manager improvements:
  - Added support for "-*" to exclude all providers/context in one go
  - Added support for space as additional delimiters in filter strings
  - Explicitly handle "*" wildcard to include all providers/context by default
- Added `gh_group` AI tool for grouping components by GUID, with support to custom names and colors
- Added `list_generate` AI tool for generating a list of items from a prompt and count ([#6](https://github.com/architects-toolkit/SmartHopper/issues/6))
- New `AITextListGenerate` component implementing `list_generate` AI tool with type 'text' ([#6](https://github.com/architects-toolkit/SmartHopper/issues/6))
- Added `Category` property to `AITool` with default value "General"
- New `Filter` class for common include/exclude patterns processing

### Changed

- Several improvements to `AIChatComponent`:
  - Updated `WebChatDialog` to use provided system prompt or fall back to default
  - Improved default system prompt for AI Chat to focus on a Grasshopper assistant, including tool call examples
  - Added `gh_group` mention to default system prompt
- Modified manifest to reflect new instructions input feature in AI Chat Component
- Modified `AITextEvaluate`, `AITextGenerate`, `AIListEvaluate` and `AIListFilter` to exclude all context using the new "-*" filter
- Code reorganization:
  - Reorganized `AIProvider`, `AIContext` and `AITool` managers
  - Code cleanup in `AIChatComponent`, `WebChatDialog` and `WebChatUtils`
  - Renamed `SmartHopper.Config` to `SmartHopper.Infrastructure`
  - Renamed `SmartHopper.Config.Tests` to `SmartHopper.Infrastructure.Tests`
- Updated `StringConverter.StringToColor` to accept argb, rgb, html and known color names as input
- Change `GetResponse` parameter from `includeToolDefinitions` to `toolFilter`
- Updated AITool constructor to require category parameter
- Categorized existing tools with DataProcessing, Components, Knowledge and Scripting categories
- Updated unit tests to include category parameter
- Integrated the new `Filter` class in `GetFormattedTools` and `GetCurrentContext`

### Removed

- Removed unnecessary `GetModel` and `GetFormattedTools` methods in `OpenAIProvider`, `MistralAIProvider` and `TemplateProvider`
- Removed `GetResponse` method from `AIStatefulAsyncComponentBase` in favor of `CallAIToolAsync`

## [0.3.3-alpha] - 2025-06-23

### WIP

- Adding LaTeX support in chat UI with the MathJax library.

### Added

- Added DeepSeek provider ([#222](https://github.com/architects-toolkit/SmartHopper/issues/222)).
- Added temperature parameter support for MistralAI, OpenAI, and DeepSeek providers.
- Added slider UI control in settings dialog for numeric parameters.
- Added reasoning support:
  - Render reasoning panels for `<think>` tags in chat UI as collapsible `<details>` blocks.
  - Exclude reasoning from copy-paste (`mdContent`) and include in HTML display (`htmlContent`).
  - Added configurable `reasoning_effort` setting (low, medium, high) for OpenAI o-series models.
  - New `StripThinkTags` method in `Config.Utils.AI`.
  - Set up OpenAI and DeepSeek to return reasoning in the response.

### Changed

- Updated default OpenAI model to gpt-4.1-mini.
- Mention `DeepSeek` as available provider in the About dialog.
- Settings dialog improvements:
  - Added dropdown support for provider settings when a list of allowed values is provided.
  - Increased max tokens for OpenAI, MistralAI and DeepSeek providers to 100000.
  - Improved descriptions.
  - Setting values that are empty or whitespace will be removed from the settings file on `UpdateProviderSettings`.
- AI providers updates:
  - Updated deprecated OpenAI max_tokens parameter to max_completion_tokens.
  - Refactored OpenAI, MistralAI, DeepSeek and TemplateProvider settings validation to use centralized validation methods.
  - Renamed `OpenAI` to `OpenAIProvider`.
  - Renamed `OpenAISettings` to `OpenAIProviderSettings`.
  - Renamed `MistralAI` to `MistralAIProvider`.
  - Renamed `MistralAISettings` to `MistralAIProviderSettings`.
  - OpenAI, MisralAI and DeepSeek now remove `<think>` tags from messages before sending them to the API, using the `StripThinkTags` method from `Config.Utils.AI`.
  - Reorganized providers settings and moved them from `AIProvider` to `AIProviderSettings`.

### Removed

- Provider settings cleanup:
  - Removed `private LoadSettings` method from `OpenAISettings`.
  - Removed `private LoadSettings` method from `TemplateProviderSettings`.
  - Removed `CreateSettingsControl`, `GetSettings` and `LoadSettings` method from `IAIProviderSettings` and all implementations.
- Removed `ConcatenateItemsToJsonList` method from `ParsingTools` since it was not used.

### Fixed

- Fixed "AI List Filter might not be working as expected" ([#220](https://github.com/architects-toolkit/SmartHopper/issues/220)).
- Fixes "Accepted feature request: Add compatibility to Magistral thinking (by MistralAI)" ([#223](https://github.com/architects-toolkit/SmartHopper/issues/223)).
- Fixed limit to 4096 tokens for AI providers in settings dialog.
- Fixed errors in validation methods of providers.
- Fixed failing settings integrity check during initialization because providers were not loaded yet.
- Fixed settings dialog dropdown values not being saved and restored from storage.

## [0.3.2-alpha] - 2025-06-15

### Added

- Added undo support to `MoveInstance`, `SetComponentPreview`, and `SetComponentLock`.
- New `ScriptTools` class in `SmartHopper.Core.Grasshopper.Tools` for Grasshopper script components, including:
  - New `script_review` AI tool for reviewing Grasshopper scripts.
  - New `script_new` AI tool for generating Grasshopper scripts.
- Added support for script components in `GhPutTools`, enabling placement of script components with code from GhJSON.
- Enhanced `GetObjectsDetails` in `GHDocumentUtils` to serialize variable input and output parameters from script components to GhJSON.
- Extended `GhPutTools` to handle variable input and output parameters when placing script components from GhJSON.
- Added support for parameter modifiers (simplify, flatten, graft, reverse) in both input and output parameters for script components in `GhPutTools` and `GHDocumentUtils`.
- New `CallAiTool` method in `AIStatefulAsyncComponentBase` to handle provider and model selection, and metrics output.
- `AiTools` now define their own endpoint.
- New icons for all components.

### Changed

- Minimum Rhino version required increased to 8.19
- Updated SmartHopper logo
- Renamed `gh_retrieve_components` by `gh_get_available_components`
- Prevent `GHDocumentUtils.GetObjectsDetails` from generating humanReadable field if value is already human readable (numbers and strings)
- Renamed `evaluateList` and `filterList` AI tools to `list_evaluate` and `list_filter`
- Renamed `evaluateText` and `generateText` AI tools to `text_evaluate` and `text_generate`
- Migrated `GhPutTools` to `Utils` in `Core.Grasshopper`
- Split AI Tools into smaller files:
  - `TextTools` into `text_evaluate.cs` and `text_generate.cs`
  - `ListTools` into `list_evaluate.cs` and `list_filter.cs`
  - `GhObjTools` into `gh_tidy_up.cs`, `gh_toggle_preview.cs`, `gh_toggle_lock.cs`, `gh_move_obj.cs`
  - `GhPutTools` into `gh_put.cs`
  - `WebTools` into `web_generic_page_read.cs`, `web_rhino_forum_read_post.cs` and `web_rhino_forum_search.cs`
  - `GhGetTools` into `gh_get.cs`, `gh_list_components.cs` and `gh_list_categories.cs`
  - `ScriptTools` into `script_new.cs` and `script_review.cs`
- Now `Put` removes all default inputs and outputs from the component before adding a new script component.
- Improved OpenAI provider to support structured output.
- Improved `script_new` in several ways:
  - Now it creates component inputs and outputs.
  - It returns the instance GUID of the created component.
- Modified `AITextGenerate`, `AITextEvaluate`, `AIListEvaluate` and `AIListFilter` to use `AIToolManager` instead of calling the AI tool directly.
- Improved components descriptions.

### Deprecated

- `GetResponse` method in `AIStatefulAsyncComponentBase` is deprecated. Use `CallAiTool` instead.

### Removed

- Removed `Eto.Forms` reference from `SmartHopper.Config`.
- Removed the `GetEndpoint` method from `AIStatefulAsyncComponentBase`.

### Fixed

- Fixed MistralAI provider not working with structured output ([#112](https://github.com/architects-toolkit/SmartHopper/issues/112)).
- Fixed OpenAI error in API URI.
- Fixed CI Signature Tests in `SmartHopper.Config.Tests`.
- Fixed OpenAI logo quality.

## [0.3.1-alpha] - 2025-05-06

### Added

- Added the "Accepted feature request: Allow for copy-paste the chat in a good format when selecting the text" ([#86](https://github.com/architects-toolkit/SmartHopper/issues/86)).
- Added support for script components in `GhGet`.
- Allow `CreateComponentGrid` for fractional row positions for components, to create a more human-like layout.
- Added `gh_tidy_up` AI tool in `GhObjTools` for arranging selected components into a dependency-based grid layout.
- New `gh_tidy_up` component.
- New `SelectingComponentBase` for components that need the button to select other components.
- New `GHJsonAnalyzer` and `GHJsonFixer` classes for analyzing and fixing GHJSON formats.

### Changed

- Improved chat UI with timestamps for messages, collapsible tool messages, inline metrics per message, button to copy codeblocks to clipboard, and better formatting.
- Reorganization of JSON models for clearer structure.
- Migrated the `GhPut` tool from the `GhPutComponent` to the `GhPutTools` class, using the `AIToolManager`.
- `DeserializeJSON` now fixes invalid InstanceGuids in Grasshopper JSON documents when deserializing.
- Moved `DependencyGraphUtils` and `ConnectionGraphUtils` from `SmartHopper.Core.Graph` to `SmartHopper.Core.Grasshopper.Graph`.
- Improved `CreateComponentGrid` in `DependencyGraphUtils`:
  - Now returns original pivots relative to the most top-left component to ensure relative positioning
  - Uses a more human-like layout with column widths based on actual component widths
  - Uses horizontal margin of 50 and vertical spacing of 80
  - Centers Params from their actual center instead of the top-left position
  - Improved by detecting islands of components, ensure connected components stay together, and use barycenter heuristic algorithm for initial layer ordering
  - Minimizes connection length
  - Aligns parents with children
- Improved `MoveInstance`:
  - Added a nice animation so that components move smoothly to their new position.
  - Skip movement if initial and target positions are the same.
- Modified `GhGetComponents` to use the new `SelectingComponentBase`.
- Implemented the new `GHJsonAnalyzer` and `GHJsonFixer` in `GhPutTools` and `GhPutComponents`.

### Fixed

- Fixed issue with tool calls in chat messages. Now the code provides exactly the json structure expected by MistralAI and OpenAI.
- Fixed tooltip visibility at the bottom of the chat.
- Fixed component placement in `GhPut` tool was too separated.
- Fixed source components in `TopologicalSort` were not sorted in reverse order.
- Limited `ghget` connections to components within the result objects set.
- Fixed `gh_tidy_up` moving components on every execution.
- Fixed `CreateComponentGrid` joining last and last-1 column together.
- (automatically added) Fixes "Panels and params position is calculated from top-left, not from center" ([#184](https://github.com/architects-toolkit/SmartHopper/issues/184)).

## [0.3.0-alpha] - 2025-04-27

### Added

- Enabled the AIChat component to execute tools in Grasshopper.
- Added optional 'Filter' input to `GhGetComponents` component for filtering by errors, warnings, remarks, selected, unselected, enabled, disabled, previewon, previewoff, previewcapable, notpreviewcapable. Supports include/exclude syntax (+/-) provided as a list of tags, each tag in a separate line, comma-separated or space-separated.
- Added optional 'Type filter' input to `GhGetComponents` component to filter by component type (params, components, inputComponents, outputComponents and processingComponents).
- Added `ConnectionGraphUtils` class in `SmartHopper.Core.Graph` namespace with method `ExpandByDepth` to expand a set of component IDs by following connections up to the given depth.
- Added `GhRetrieveComponents` component and `ghretrievecomponents` AI tool for listing Grasshopper component types with descriptions, keywords, category filters, and list of inputs and outputs.
- Added `ghcategories` AI tool in `GhTools` to list Grasshopper component categories and subcategories with optional soft string filter.
- Added new `gh_toggle_preview` AI tool in `GhObjTools` for toggling Grasshopper component preview by GUID.
- Added new `gh_toggle_lock` AI tool in `GhObjTools` for toggling Grasshopper component lock state by GUID.
- Added new `gh_move_obj` AI tool in `GhObjTools` for moving Grasshopper component pivot by GUID with absolute or relative position.
- Added `MoveInstance` method in `GHCanvasUtils` to move existing instances by GUID with absolute or relative pivot positions.
- Improved security in Providers by accepting only signed assemblies.
- Added multiple CI Tests, for example, to ensure unsigned provider assemblies are rejected by `ProviderManager.VerifySignature`, to ensure only signed assemblies are loaded by `ProviderManager.LoadProviderAssembly`, and to ensure only enabled providers are registered by `ProviderManager.RegisterProviders`.
- Added `AIToolCall.cs`, a new model for AI tool call requests.
- Added `SmartHopperInitializer.cs`, a static class for safe startup and provider initialization.
- Added `StyledMessageDialog` class in `SmartHopper.Config.Dialogs` for consistent message dialog styling with the SmartHopper logo.
- Added `WebTools` to retrieve webpages from the Internet and provide them to the AI provider.
- Added `Search Rhino Forum` webtool to query posts in Rhino Forum.
- Added `Get Rhino Forum Post` webtool to retrieve full JSON of a Rhino Discourse forum post by ID.

### Changed

- Renamed the 'Branches Input' and 'Processed Branches' parameters to 'Data Count' and 'Iterations Count' in `DeconstructMetricsComponents`. Improved descriptions for both parameters.
- Modified `FilterListAsync` in `ListTools` to return indices instead of filtered list items, with `AIListFilter` component now handling the final list construction.
- Renamed `GhGetSelectedComponents` (GhGetSel) to `GhGetComponents`.
- Moved `GhGet` execution logic to external tools managed by `ToolManager`.
- Improved `ghget` tool's `typeFilter` input: supports include/exclude syntax (+/-) with multiple tokens (params, components, input, output, processing) and updated schema description with definitions and examples.
- Reorganized `SmartHopper.Core.Grasshopper` files in subfolders that match the namespace.
- Isolated settings so providers access them only via `ProviderManager`, not directly via `SmartHopperSettings`.
- SmartHopper icon is now used for all dialogs within SmartHopper (about, settings, messages and ai chat)

### Removed

- `GhGetComponent` was replaced by `GhGetSelectedComponents` (GhGetSel) and renamed back to `GhGetComponents`.
- Removed support for net48. From now on, Rhino 8 or later is required.
- Removed `ToolFunction` and `ToolArgument` in `AIResponse`, in favor of the more flexible `AIToolCall`.

### Fixed

- Fixed double‐encryption of sensitive settings in `SettingsDialog.SaveSettings()` causing unreadable API keys
- Fixed mismatch between in-memory and on-disk `TrustedProviders` when prompting in `ProviderManager.LoadProviderAssembly()`
- Fixed a bug in `DataProcessor` where results were being duplicated when multiple branches were grouped together to unsuccessfully prevent unnecessary API calls [#32](https://github.com/architects-toolkit/SmartHopper/issues/32)
- Fixed inconsistent list format handling between `AIListEvaluate` and `AIListFilter` components.
- Fixed `MistralAI` provider not loading `AI Tools`.
- Fixed `GhGetComponent` select functionality that was accidentally omitted in the new `GhTools`.

## [0.2.0-alpha] - 2025-04-06

### Added

- **Modular AI providers**: OpenAI and MistralAI providers are now separate, dynamically loaded plugins; a template provider is available for building custom providers.
- **AI Chat component**: New interactive chat component with a WebView-based chat dialog, message bubbles, and Markdown rendering.
- **Provider selection**: Added a "Default" provider option and a global default provider setting in the settings dialog.
- **SmartHopper tab**: Added a custom icon for the SmartHopper tab in Grasshopper.
- **Markdown support**: Chat messages are now rendered as Markdown with support for headings, code blocks, blockquotes, and inline formatting.
- **Context system**: Added context filtering by provider and key, plus automatic time and environment context in the AI Chat component.
- **Documentation**: Added a "Supported Data Types" section to README.md.

### Changed

- **AIChat execution**: The AI Chat component now always runs when the Run parameter is true, regardless of input changes.
- **Chat UI**: Improved chat dialog layout with responsive sizing, text wrapping, message selection and copying, and single-dialog-per-component behavior.
- **About dialog**: Restyled with smaller fonts, a minimum size, and an AI-generated content disclaimer.
- **Release packaging**: Release builds now produce separate Rhino8-Windows and Rhino8-Mac zip files.
- **Settings menu**: Rewrote the settings menu using Eto.Forms for cross-platform compatibility.
- **AI Context component**: Renamed to AI File Context.

### Fixed

- Fixed AI provider state not being saved or restored in Grasshopper files ([#41](https://github.com/architects-toolkit/SmartHopper/issues/41)).
- Fixed AI Chat using the default provider correctly when "Default" is selected.
- Fixed settings menu hiding on Windows ([#94](https://github.com/architects-toolkit/SmartHopper/issues/94)).
- Fixed AI Chat freezing Rhino ([#85](https://github.com/architects-toolkit/SmartHopper/issues/85)).
- Fixed settings menu incompatibility with Mac ([#12](https://github.com/architects-toolkit/SmartHopper/issues/12)).
- Added AI disclaimer to the chat and about dialogs ([#114](https://github.com/architects-toolkit/SmartHopper/issues/114)).
- Fixed chat dialog freezing and taskbar focus issues.

### Removed

- Removed OpenAI and MistralAI providers from the legacy SmartHopper.Config project.
- Removed the HtmlAgilityPack dependency.

## [0.1.2-alpha] - 2025-03-17

### Changed

- Updated pull-request-validation.yml workflow to use version-tools for version validation
- Improved PR title validation with more detailed error messages and support for additional conventional commit types
- Added "security" as a valid commit type in PR title validation
- Modified update-dev-version-date.yml workflow to create a PR instead of committing changes directly to the branch

### Removed

- Removed Test GitHub Actions workflow

### Fixed

- Fixed version badge update workflow to only modify the version badge and not affect other badges in README.md
- Fixed badge addition logic in version-tools action to properly handle cases when badges don't exist
- Fixed security-patch-release.yml workflow to create a PR instead of pushing directly to main, resolving repository rule violations
- Fixed version-calculator to always perform the requested increment type without conditional logic, ensuring consistent behavior
- Fixed security-patch-release.yml workflow to create a release draft only when no PR is created
- Added new security-release-after-merge.yml workflow to create a release draft when a security patch PR is merged
- Fixed GitHub release creation by removing invalid target_commitish parameter

### Security

- (automatically added) Security release to update all workflow actions to the latest version.
- Updated several github workflows to use the latest version of actions:
  - Updated tj-actions/changed-files from v45.0 to v46.0.1
  - Updated actions/checkout to v4 across all workflows
  - Updated actions/setup-dotnet to v4
  - Updated actions/upload-artifact to v4
  - Updated actions/github-script to v7
- Enhanced pull-request-validation.yml workflow with improved error logging for version and PR title checks
- Added new security-patch-release.yml workflow for creating security patch releases outside the milestone process
- Implemented GitHub Actions security best practices by pinning actions to full commit SHAs instead of version tags
- Updated security-patch-release.yml workflow to create a PR instead of pushing directly to main, resolving repository rule violations

## [0.1.1-alpha] - 2025-03-03

### Added

- Added the new GhGetSelectedComponents component.
- Added the new AiContext component ([#40](https://github.com/architects-toolkit/SmartHopper/issues/40)).
- Added the new ListTools class with methods:
  - `FilterListAsync` (migrated from `AIListFilter` component)
  - `EvaluateListAsync` (migrated from `AIListEvaluate` component)

### Changed

- Updated README.md to better emphasize the plugin's ability to enable AI to directly read and interact with Grasshopper files.
- New About menu item using Eto.Forms instead of WinForms.
- Refactored AI text evaluation tools to improve code organization and reusability:
  - Added generic `AIEvaluationResult<T>` for standardized tool-component communication
  - Created `ParsingTools` class for reusable AI response parsing
  - Created `TextTools` with method `EvaluateTextAsync` (replacement of `AiTextEvaluate` main function)
  - Added `GenerateTextAsync` methods to `TextTools` (migrated from `AITextGenerate` component)
  - Updated `AITextGenerate` component to use the new generic tools
  - Added regions in `TextTools` to improve code organization
- Refactored AI list processing tools to improve code organization and reusability:
  - Added `ParseIndicesFromResponse` method to `ParsingTools` for reusable response parsing
  - Added `ConcatenateItemsToJson` method to `ParsingTools` for formatting list data
  - Added `ConcatenateItemsToJsonList` method to `ParsingTools` for list-to-JSON conversion
  - Added regions in `ListTools` and `ParsingTools` to improve code organization
  - Updated `AIListFilter` component to use the new generic tools
  - Updated `AIListEvaluate` component to use the new generic tools
  - Fixed error handling in list processing components to use standardized error reporting
  - Improved list processing to ensure entire lists are processed as a unit

### Fixed

- Restored functionality to set Persistent Data with the GhPutComponents component.
- Restored functionality to generate pivot grid if missing in JSON input in GhPutComponents.
- AI messages will only include context if it is not null or empty.

## [0.1.0-alpha] - 2025-01-27

### Added

- Added the new AITextEvaluate component.

### Changed

- Renamed the AI List Check components to AI List Evaluate.
- Improved AI provider's icon visualization.

### Removed

- Removed components based on the old Component Base.
- Removed the code for the old Component Base.

### Fixed

- Fixed Feature request: Full rewrite of the Component Base ([#20](https://github.com/architects-toolkit/SmartHopper/issues/20))
- Fixed Feature request: AI Text Check Component ([#4](https://github.com/architects-toolkit/SmartHopper/issues/4))

## [0.0.0-dev.250126] - 2025-01-26

### Added

- Added a new Component Base for AI-Powered components, including these features:
  - Debouncing timer to prevent fast recalculations
  - Enhanced state management system with granular state tracking
  - Better stability in the state management, that prevents unwanted recalculations
  - Store outputs and prevent from recalculating on file open
  - Store outputs and prevent from recalculating on modifying Graft/Flatten/Simplify
  - Persistent error tracking through states
  - Compatibility with button and boolean toggle in the Run input
  - Compatibility with Data Tree processing (Input and Output)
  - Manual cancellation while processing
- Added a new library with testing components.

### Changed

- General clean up and refactoring, including the suppression of unnecessary comments, and the removal of deprecated features.
- Migrate AI Text Generate component to use the new Component Base.
- Refactor DataTree libraries in Core to unify and simplify functionality.

### Fixed

- Fixed lack of comprehensive error when API key is not correct ([#13](https://github.com/architects-toolkit/SmartHopper/issues/13))
- Fixed Changing Graft/Flatten from an output requires recomputing the component ([#7](https://github.com/architects-toolkit/SmartHopper/issues/7))
- Fixed Feature request: Store outputs and prevent from recalculating on file open ([#8](https://github.com/architects-toolkit/SmartHopper/issues/8))
- Fixed Bug: Multiple calls to SolveInstance cause multipe API calls (in dev branch) ([#24](https://github.com/architects-toolkit/SmartHopper/issues/24))

## [0.0.0-dev.250104] - 2025-01-04

### Added

- Added metrics for AI Provider and AI Model in AI-Powered components ([#11](https://github.com/architects-toolkit/SmartHopper/issues/11))

### Fixed

- Fixed bug with the Model input in AI-Powered components ([#3](https://github.com/architects-toolkit/SmartHopper/issues/3))
- Fixed model parameter handling in IAIProvider interface to ensure proper model selection across providers ([#3](https://github.com/architects-toolkit/SmartHopper/issues/3))
- Fixed issue with AI response metrics not returning the tokens used in all branches, but only the last one ([#2](https://github.com/architects-toolkit/SmartHopper/issues/2))

## [0.0.0-dev.250101] - 2025-01-01

### Added

- Initial release of SmartHopper
- Core plugin architecture for Grasshopper integration
- Base component framework for custom nodes
- GitHub Actions workflow for automated validation
  - Version format checking
  - Changelog updates verification
  - Conventional commit enforcement
- Comprehensive documentation and examples
  - README with setup instructions
  - CONTRIBUTING guidelines
