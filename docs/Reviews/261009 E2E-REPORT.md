<!-- docs-validation: ignore -->

# SmartHopper `main` — End-to-End Test Report

**Date**: 2026-10-09
**Branch**: `main` @ `779f47fc`
**Runtime**: Rhino 8.35 / Grasshopper 1.0.0008, .NET 8.0.31, SmartHopper 2.0.0-dev (Debug build, VS-attached)
**MCP harness**: SmartHopper MCP Server on canvas (`Allow Editing`), Visual Studio MCP
**Default provider**: `MistralAI` / `mistral-small-latest`. Configured: Anthropic, DeepSeek, LocalAI, MistralAI, Ollama, OpenAI, OpenRouter. **Not configured**: Gemini.
**Persisted settings**: `ModalityFallback=0` (Disabled), HTTP timeout 600s, batch timeout 300s, debounce 1000ms.

Issue coverage: A1 (#528), A4 (#530), B1 (#533), B2 (#534), B3 (#535), B4 (#536), B5 (#537), B6 (#538), B7 (#539), B9 (#541), B11 (#543), B13 (#545), B14 (#546), B15 (#547), B16 (#548), B17 (#549).

Screenshots: `shots/00-initial-canvas.png`, `02-b4-input-adapters.png`, `03-b2-b7-b13-area.png`, `04-b7-batch.png`, `05-a1-tidyup.png`, `06/07/08-b14-imageviewer*.png`, `15-script-clicked.png`, `22-review-captured.png`, `23-script-review-success.png`, `24-batch-preparing.png`, `25/26-islands-*.png`, `27/28-large-*.png`, `30-params-after.png`, `32c-patch-verified.png`, `32d-patch-panel.png`. Saved document: `test-assets/e2e-canvas.gh` (73 KB). Retest assets: `test-assets/vision-big.png`, `test-assets/tone.wav`, `test-assets/tone-saved.wav`, `test-assets/click.py`, `dclick.py`, `rclick.py`, `move.py` (synthetic mouse helpers used because `gh_select` is broken — F11).

---

## 1. Executive summary

The component pipeline (input adapters → output adapters → providers) is fundamentally healthy: text, scalar, list, JSON, file and web inputs all produce correct `AIInputPayload`s, output adapters parse correctly, metrics are rich and accurate on success, script generation produces placeable GhJSON, batch submission + polling works, and the modality-fallback resolver demonstrably resolves capable models.

However, **all dedicated audio endpoints are broken end-to-end**, the **MCP AI-tools path cannot reach any provider**, `gh_select` **cannot select anything** (cross-thread defect), **Python script generation emits a degenerate empty component**, and the **`any_provider` modality fallback is dead code**. A cluster of quality issues around error reporting, message persistence, and GhJSON harness semantics completes the picture. Fifteen confirmed product defects, several minor issues, and three test-harness limitations are detailed below.

### Severity overview

| # | Severity | Area | Finding |
|---|----------|------|---------|
| F1 | **High** | TTS (B2) | `AI to Speech` can never succeed: SDK has no binary-response path; MistralAI sends chat-shaped body to `/audio/speech`; Gemini decodes audio into unusable text |
| F2 | **High** | MCP | All MCP AI tools (`text2text`, `script_review`, `speech_generate`, `script_generate`…) fail request validation — `Provider=''` (no settings resolution) |
| F3 | **High** | STT (B2) | Audio payloads route to endpoint by *request capability*, not payload content → OpenAI audio input hit `/responses` → HTTP 400 "Audio input is not available"; MistralAI encoder has zero `AIInteractionAudio` handling |
| F4 | Medium | Parsing (B5) | `AIResponseParser.ParseBooleanFromResponse` checks `Contains("TRUE")` before `Contains("FALSE")` on the whole response — verbose answers resolve wrongly |
| F5 | Medium | Errors | Every provider error is reported twice (`[Provider]` + `[Return]`), and failed calls emit misleading `finish_reason:"stop"` metrics with null provider/model |
| F6 | Medium | Registry | Stale models registered (`mistral-large-2411` → API `invalid_model`); Settings temperature doc claims 0–2.0 while Mistral rejects >1.5 |
| F7 | Medium | Validation | Capability gate keys on the tool's `RequiredCapability`, not payload modalities — image payload to a text-only model passes validation (B17 gap) |
| F8 | Low | GhJSON/MCP | `gh_put` reports `acceptedChanges` > 0 even when all component placements fail; `gh_put` "update" by `instanceGuid` creates a duplicate instead of merging |
| F9 | Low | UX | `Extra Settings` defaults emit `{"reasoning_effort":"none"}` which is force-sent to providers that don't expect it; Web2AI emits all-zero Metrics for non-AI operations |
| F10 | Low | Messages | Runtime error messages persist across re-solves (stale error shown after cause removed) |
| F11 | **High** | MCP/canvas | `gh_select` deterministically throws `Cross-thread operation not valid` — it touches `Instances.ActiveCanvas`/`document.SelectedObjects()`/`canvas.Refresh()` on the tool thread instead of marshalling to the UI thread |
| F12 | Medium | MCP | `gh_list_components` NREs when a component proxy can't be instantiated (`CreateInstance()` → null → `instance.CreateAttributes()` throws). Froze Rhino until the VS debugger was continued |
| F13 | Medium | Fallback | `modality_fallback=any_provider` is structurally unreachable: only `AIOutputAdapterBase` calls `ComponentCapabilityValidator`, no output adapter's `RequiredCapability` includes `ImageInput`/`AudioInput`, and the single `ImageInput` tool (`img2text`) runs via `AIImg2TextComponent` which skips validation entirely |
| F14 | Low | GhJSON lib | `gh_disconnect` reports `status:"disconnected"` when no matching wire exists — `CanvasConnector.DisconnectOnUiThread` returns `true` for "wire absent" (ghjson-dotnet `CanvasConnector.cs:151-154`, idempotent by design) and unresolvable param names fall back to first in/output |
| F15 | Medium | Script gen (B13) | Python script generation produces a degenerate component: `gh.python` extension with empty `code`, no inputs/outputs (1836 output tokens consumed). MCP `script_generate` fails outright with `AI script generation failed` |
| T1 | Harness | VS MCP | `output_read` fails on every pane (`An error occurred invoking 'output_read'`) — debug-log inspection impossible |
| T2 | Harness | GhJSON | Panel `multiline:true` doesn't produce multi-item output; multiline text arrives as one item with `\n`; `internalizedData` on Panel output ignored (emits `text`) |
| T3 | Harness | GhJSON | `gh_put` connections can't reference pre-existing canvas objects by `id` — only by new-doc `id` or via `gh_connect` (documented behavior, but the error text was a surprise) |

---

## 2. Confirmed findings (detail)

### F1 — TTS is broken on every provider (B2, B14-adjacent)

`AI to Speech` calls the `speech_generate` tool, which issues a nested `AIRequestCall` with capability `TextInput|SpeechOutput`. Three independent defects each make it fail:

1. **MistralAI — wrong request body.** `MistralAIProvider.PreCall` routes `SpeechOutput` to `/audio/speech` (`MistralAIProvider.cs:103-117`), but `Encode` (`:290-327`) unconditionally builds `{"model","messages","max_tokens","temperature"}` — the chat shape. Live result: `HTTP 422 {"detail":[{"type":"missing","loc":["body","input"],"msg":"Field required", "input":{"model":"voxtral-mini-tts-2603","messages":[…]}}]}`. The TTS endpoint requires `input`, not `messages`. Same defect hits `/audio/transcriptions` (STT also routes but gets a JSON chat body instead of multipart).
2. **OpenAI/SDK — binary response can't be decoded.** `AIProvider.Call` (`AIProvider.cs:1023-1069`) always `ReadAsStringAsync` + `JObject.Parse`. `/audio/speech` returns raw MP3 bytes → `"The OpenAI API returned invalid JSON. Response preview: <binary>"`. No code path handles non-JSON success responses.
3. **Gemini — unusable decode shape.** `GeminiProvider.Decode` (`GeminiProvider.Decode.cs:122-158`) converts `inlineData` audio into `AIInteractionText` containing `{"audioData","mimeType"}` JSON, and a second `[Audio: mime/X]` stub. Downstream, `speech_generate` does `VersatileAudio.FromString(response)` which accepts path/URL/base64/data-URI — not that JSON envelope → guaranteed "Failed to create VersatileAudio" (code-evident; Gemini unconfigured so not run live).
4. **Component extractor is text-only.** `AI2SpeechComponent`'s Audio extractor reads `Body.GetLastAssistantText()` (`AI2SpeechComponent.cs:85-101`) — a real `AIInteractionAudio` output would be ignored entirely.

Net effect: **no provider can produce audio through `AI to Speech`** in this build. The Audio Viewer correctly shows its "No audio loaded" empty state; cascading `Audio` input produces `Input parameter A failed to collect data`.

*Positive*: `set_ai_provider_and_model` (MCP) correctly sets per-component provider (persisted as `smarthopper.state.selectedProviderName` — verified in serialized GhJSON).

### F2 — MCP AI tools path is non-functional

`text2text`, `speech_generate`, `script_review` all fail with `Request validation failed` / `AI review request failed`. Debug evidence shows `Provider=''` — the MCP tool path does not resolve the default provider from `SmartHopperSettings` (contrast: the same functionality through canvas components works). This blocks B13's `script_review` and all AI convenience tools for MCP/assistant users. The non-AI MCP tools (`gh_put`, `gh_get`, `gh_diff`, `gh_patch_apply`, `gh_tidy_up`, `canvas_*`, `set_ai_provider_and_model`, `gh_document_save`, `gh_connect`/`gh_disconnect`, `gh_report`, `gh_get_errors`) all work.

### F3 — STT/audio input path is broken (B2)

`Audio2AI` correctly wraps `tone.wav` into `AIInputPayload (Audio, 1 interaction)`. Downstream:

- **MistralAI**: `MistralAIProvider.EncodeToJToken` has no `AIInteractionAudio` branch — audio interactions are silently dropped; `/audio/transcriptions` route additionally needs multipart, not JSON.
- **OpenAI**: `gpt-4o-mini-audio-preview` + audio payload → routed to `/responses` (default for `TextOutput` capability) → `HTTP 400 "Audio input is not available", param:"input"`. Endpoint routing keys on the *request's capability flag*, not the payload's actual modalities, so `input_audio` content lands on an audio-incompatible endpoint. (The `/chat/completions` path that supports `input_audio` parts exists at `OpenAIProvider.cs:142-148, 396-406` but is never reached for a text-capability request.)

`/audio/transcriptions` is only reachable via `SpeechInput` capability (e.g. modality-fallback `AudioToTextFallback`), which was not reachable live because the fallback is keyed to required capability, not payload (F7).

### F4 — Fragile boolean parsing (B5)

`AIResponseParser.ParseBooleanFromResponse` uppercases the entire response and checks `Contains("TRUE")` then `Contains("FALSE")`. Any verbose answer containing either word wins by order — e.g. "…the statement is FALSE because water is wet — TRUE that it's wet…" resolves arbitrarily, and explanation text ("It is not true that…") pollutes the result. Observed: `AI to Boolean` on "Is water wet?" returned `false` after a 110-token answer. The other adapters (Number `42.5`, Integer, lists `[1,2,3]`/`[4,5,6]`/`[true,false,true]`) parsed cleanly — the defect is specific to boolean substring matching.

### F5 — Error reporting quality (cross-cutting)

- Every provider failure surfaces **twice**: `[Provider] Provider error: …` and `[Return] …` with identical payload.
- Failed calls still emit a Metrics object with `finish_reason:"stop"`, `ai_provider:null`, `ai_model:null`, 0 tokens — `[Return] Finish reason missing; defaulted to 'stop'` in warnings explains it. A "stop" finish reason on a hard-failed call is actively misleading.
- `context_usage_percent` was null on failures (expected) but the metrics object exists at all is the oddity.

### F6 — Model registry / docs drift (B3, B11)

- `mistral-large-2411` is registered (no `ImageInput`) but the Mistral API answers `invalid_model` — registry lists retired models. (Also `mistral-small-latest`→`mistral-small-2603` alias works correctly.)
- `Settings` Temperature tooltip/docs imply a generic 0–2.0 range; Mistral rejects `>1.5` (live `HTTP 422 less_than_equal`), so provider-specific limits aren't surfaced before the call.
- Nice touch observed: unknown model emits a *remark* `Requested model '…' is not registered for 'MistralAI'. Proceeding with the model, but capability validation cannot be performed.` — good UX, though it means capability checks silently no-op on unknown models.

### F7 — Capability validation ignores payload modalities (B17-adjacent)

`ComponentCapabilityValidator.ValidateSync(RequiredCapability, mode)` only checks the tool's required flags (`AIOutputAdapterBase.cs:369-378`). An image payload + text-only model passed validation and went to the API (where it failed on an unrelated stale-model error). The modality-fallback resolver therefore only engages when the *component's required* capability is missing — content-driven gaps (image/audio in payload) are never gated nor fallen back. This is also why the earlier Img2AI test was inconclusive (52 input tokens, model "didn't see" the 4-px test image — can't rule out silent drop vs weak perception; not a confirmed bug).

### F8 — gh_put accounting/semantics (A4-adjacent, harness)

- `gh_put` with three unresolvable component names returned `acceptedChanges:3` with zero placements — misleading success accounting.
- `gh_put` given an existing `instanceGuid` creates a **duplicate** component (new GUID, pivot 0,8719) rather than updating `componentState` — so "edit panel text" is impossible via gh_put; work-around is delete+recreate or a second panel + rewiring.
- `gh_put` rejects components missing `name`/`componentGuid`/`id` even when `instanceGuid` identifies an existing object.

### F9 — Settings/Extras quirks (B3)

- `Extra Settings` component emits `{"reasoning_effort":"none"}` at defaults; when wired, it's force-sent to Mistral (`reasoning_effort` isn't a Mistral param — harmless here, but the "defaults" aren't neutral).
- `Web2AI` (non-AI fetch path, skip/link modes) emits an all-zero Metrics object — `ai_provider:null`, `finish_reason:"stop"` — for an operation that never calls AI. Same misleading-metrics pattern as F5.

### F10 — Runtime message persistence

After removing the `modality_fallback` extras wire, `AI to Speech` continued to display the previous run's `[Provider] HTTP 422` errors even though the fresh run took the capability-gate path (verified on a clean twin component which shows `[Capability] Provider 'MistralAI' / model 'mistral-small-latest' does not support Text2Speech.`). Stale persistent messages outlive the re-solve that removed their cause — risk of users debugging already-fixed problems.

### F11 — `gh_select` accesses the canvas off the UI thread (deterministic)

Every `gh_select` call throws `Cross-thread operation not valid: Control 'Canvas' accessed from a thread other than the thread it was created on.` The tool (`src/SmartHopper.Core.Grasshopper/AITools/gh_select.cs`) directly uses `Instances.ActiveCanvas`, `canvas.Document`, `document.FindObject`, `document.SelectedObjects()`, selection mutation and `canvas.Refresh()` on the MCP tool thread. Sibling canvas tools (`gh_connect`, `gh_disconnect`, `button_click`) marshal through `ComponentManipulation.InvokeOnUiThreadAndWait`; `gh_select` doesn't. Reproduced 3× — deterministic, not timing-dependent.

### F12 — `gh_list_components` NRE on uninstantiable proxies

`gh_list_components` throws `NullReferenceException` inside `p.CreateInstance()`/attribute creation for at least one registered proxy whose `CreateInstance()` returns null — no null-guard before `instance.CreateAttributes()`. Under VS debugging the break paused Rhino's UI thread (the whole MCP server appeared "hung" — socket accepted, HTTP never returned — until `debugger_continue`). A release build would surface it as a tool error + possibly a crashed solve.

### F13 — `any_provider` modality fallback is unreachable dead code

Live test: `Img2Text-FB` component (Ollama — no vision-capable models — + extras `{"modality_fallback":"any_provider"}` + image payload) failed with `[Return] Request validation failed` / `[Validation] No capable model found for provider 'Ollama' with capability Image2Text`. No `[Fallback]` message, no cross-provider attempt.

Source confirms it's structural, not provider-specific: the sole entry point is `AIOutputAdapterBase.PrepareInputs` → `ComponentCapabilityValidator.ValidateSync(RequiredCapability, mode)` → `ModalityFallbackResolver.Resolve`. The two registered handlers (`ImageToTextFallback`, `AudioToTextFallback`) engage only when the component's `RequiredCapability` contains `ImageInput`/`AudioInput` leaf flags — and **no shipped output adapter declares either**. The only tool requiring `ImageInput` (`img2text`) runs through `AIImg2TextComponent` (`AIStatefulAsyncComponentBase`), which never invokes the validator. `AudioInput` is required by nothing. So the Settings UI page, extras key, resolver and both fallback chains are unreachable by any shipped component on this build (F7 is the payload-side twin of this gap).

### F15 — Python script generation emits a degenerate component (B13)

`AI Script Generate` works for C# (produced a valid typed `z = x + y` script), but a Python prompt — "Create a Grasshopper Python 3 script component with input x (float) that outputs y = x squared" — returned a structurally empty component after consuming 1836 output tokens:

```json
{"name":"Python","nickName":"AI Script","componentGuid":"719467e6-…",
 "inputSettings":[],"outputSettings":[],
 "componentState":{"extensions":{"gh.python":{"code":""}}}}
```

No `inputSettings`, no `outputSettings`, empty `code`. The MCP `script_generate` tool additionally fails outright (`AI script generation failed`) on two corrected attempts — though that path is already degraded by F2 (MCP tools can't resolve a provider). Likely cause: the generation prompt/schema is C#-centric (the C# path emits full `gh.csharp` code + typed params; the Python path produced only the component shell). B13's Python item fails.

### F14 — `gh_disconnect` silent no-op on unmatched wires (upstream, ghjson-dotnet)

Observed live: `gh_disconnect` with index-style param args (`"targetParam": "2"`) reported `{"status":"disconnected","successCount":1}` while the wire list stayed unchanged; repeating with the param *name* actually disconnected. Root cause is in the GhJSON library (sibling repo `ghjson-dotnet`): `CanvasConnector.DisconnectOnUiThread` (`src/GhJSON.Grasshopper/ConnectionOperations/CanvasConnector.cs:124-160`) returns `true` when `targetParam.Sources` doesn't contain the resolved source — documented as "true if the connection did not exist or was removed" — and `ConnectionHelper.ResolveInput/Output` falls back to the first parameter when the name doesn't resolve, so a typo'd param silently targets the wrong endpoint and reports success. `ConnectOnUiThread` has the same "already connected → true" shape (`:90-93`). Idempotent semantics are defensible, but the tool layer surfaces no `wasConnected`/`noop` flag, so callers cannot distinguish a real removal from a no-match. Suggested fix belongs in ghjson-dotnet (tri-state result or a `matched` flag in the SmartHopper result).

---

## 3. Verified working

| Area | Result | Evidence |
|------|--------|----------|
| Basic text chain | `Text→AI → AI→Text` via MistralAI → `"4"`, full metrics (tokens, cost, time) | live |
| B9 JSON suite | Schema→JSON Schema, validate (pass), Save (19 KB file), Open (Valid=true), Diff identical → 0-op patch | live |
| B4 input adapters | Number `3.14159`, Integer `7`, Boolean `true`, JSON, AI Prompt, Text List (3 real items → `["alpha","beta","gamma"]`) | live |
| B4 error paths | empty text→`Text cannot be empty.`, invalid JSON→`Invalid JSON: …`, missing file→`File not found: …`, bad URL→`Invalid HTTP(S) URL: …`, list type mismatches→`Data conversion failed from Text to X` | live |
| File2AI/Web2AI | md content+format; example.com fetch OK; clean error paths | live |
| B5 output adapters | Boolean/Number/Integer/TextList/NumberList/IntList/BoolList all parse; `Used Fallback=false` on success | live |
| B3 Settings | Model override propagates; invalid model→`invalid_model` 400 + informative remark; MaxTokens=1→`cut off by max token limit` error; Temperature 3.0→provider 422; per-component provider override persists in GhJSON | live |
| B17 modality fallback | `disabled`→`[Capability] … does not support Text2Speech` (no HTTP); `configured_provider`→resolved `mistral-small-latest`→`voxtral-mini-tts-2603` (model swapped in the request body, then hit F1) | live |
| B16 timeout | `Timeout=1`→`HTTP Request Timeout … exceeded 1 seconds. Try increasing…` | live |
| B1 Gemini (unconfigured) | `Gemini provider is not configured. Please set the required provider settings…` | live |
| B7 batch | `Batch=true`→submission succeeded; component shows `Processing batch (0/1)…` and polls (Mistral job still in-flight at report time); persistence/resume path source-verified (`BatchId`/`CustomIds`/`SentinelTrees` in `AIStatefulAsyncComponentBase.Persistence.cs`) | live (partial) |
| B13 script generate | `AI Script Generate`→valid GhJSON for a C# `z=x+y` script (2547 in/682 out tokens, $0.0008, 4.4 s); **the generated GhJSON was `gh_put`-placed on canvas** (round-trip works); Summary/Information outputs populated | live |
| B6 rename compat | `AIText2TextComponent` kept GUID `EB073C7A-…` → old `.gh` files resolve by GUID; old display names don't resolve by name (expected breaking change) | source + live |
| A4 diff/patch | `gh_diff` produced correct `.ghpatch` (modify + remove ops, sha256 base checksum); `gh_patch_apply` applied cleanly with conflict reporting; **`Apply GhPatch to Canvas` end-to-end**: live patch → `Success=true`, `C+=1`, new Panel `f0c15497` at exactly (350,300), canvas count 242→243; stale-checksum refusal (`BaseChecksumMismatch`) exercised | live |
| A1 tidy-up | `gh_tidy_up` left-to-right dataflow on all sub-cases: connected chain, branching, wide script components, **two disconnected islands → independent clusters**, **22-object/5-island definition → clean groups**, **Number params align to input ports** | live (one minor overlap nit) |
| B3 plain-model back-compat | Panel with raw `mistral-small-latest` wired directly into `Settings` → accepted, ran, metrics report the model | live |
| `AI Text To JSON` | prompt + JSON Schema → `{"name":"Alice","age":30}` — valid, schema-conforming, full metrics | live |
| B9 JSON ops | `JSON Array` builds `["alpha","bravo","charlie"]`; `JSON Array To Text List` extracts `1,2,3`; `JSON Object` → `{"mykey":"myvalue"}`; `JSON Sanitizer` parses text→object; `JSON To Text` pretty-prints (round-trip via Sanitizer) | live |
| B14 viewers | `Canvas To Image`→`VersatileImage (Base64)` 1580×629 → `Image Viewer` renders it; `Save?`+`File Path` wrote `saved-view.png` (106 KB); Audio Viewer shows proper empty state + Play/Save buttons | live |
| B15 persistence | `gh_document_save` wrote `e2e-canvas.gh` (73 KB); per-component provider + state extensions round-trip through GhJSON; batch state persistence source-verified | live (partial) |
| Img2AI vision (retest) | `mistral-small-latest` + 800×600 `vision-big.png` → `tokens_input:710` (vs 52 text-only — image bytes provably encoded) → output `"Red, blue, black"` matches the image (red circle, blue square, black caption) | live |
| `script_review` via canvas | `AIScriptReviewComponent`: context-menu "Select Components" captured the selected C# script; input change → `NeedsRun` → real `script_review` call → `Success=true`, structured `AI Review` (flags missing types/null-checks, emits corrected code), 2 metrics entries (1756/629 + 1738/958 tokens — second call 1664 cached) | live |
| Batch resubmission | Button on `Run?` pulsed → fresh `Processing batch (0/1)…` → later `Preparing 1/1…` (reconstruction phase reached); submit/poll/progress machinery verified live; provider-side completion still pending at report time | live (partial) |

---

## 4. Retest results (previously inconclusive)

All six previously-inconclusive items were retested live:

- **Img2AI vision path** → **RESOLVED, works.** `mistral-small-latest` + 800×600 `vision-big.png` → `tokens_input:710` (vs 52 for text-only — the image bytes provably reach the API) → output `"Red, blue"` then `"Red, blue, black"` once the caption was added — correctly describing the image. The earlier "didn't see it" was a tiny-image perception issue, not image loss. Bonus observations: failed calls append phantom zero-metrics (`finish_reason` defaulted to `"stop"` — a hole in evidence-gating), and configured `mistral-large-2411` silently ran as `mistral-small-latest` in metrics (deprecated-model substitution).
- **Batch completion** → **partially verified.** A Button pulse on `Run?` resubmitted the batch (`Processing batch (0/1)…` → `Preparing 1/1…` reconstruction phase). Submission, progress messaging, and polling verified live; provider-side completion still pending at report time.
- **`any_provider` fallback** → **RESOLVED as F13** — structurally unreachable dead code (empirically confirmed: Ollama + `any_provider` extras → `[Validation] No capable model found`, zero fallback attempt).
- **`.gh` close/reopen persistence** → still deferred — no `gh_document_open`/`close` tool exists; reloading would kill the in-canvas MCP server. GhJSON-level persistence verified; `Write`/`Read` + deferred selection restoration (`DocumentAdded` subscription) source-verified.
- **`script_review` via canvas** → **RESOLVED, works** (see Verified Working). Mechanics worth documenting: `EnableSelectionMode` only *captures* the current selection and expires the solution; in `Completed` state the re-solve just re-emits outputs (observed "Done — 64 ms" with no worker run). An input change or `Run?` pulse is required to reach `NeedsRun`→`Processing`. Also: `SelectedObjects` is not visible in `smarthopper.state` unless `includeInternalizedData` is set, and the `AI Review` output includes the model's `thinking:` reasoning preamble inline.
- **`---` anomaly** → **RESOLVED, not a defect.** Metrics accumulate per solve (the component held 15+ entries from many solves), and the same component later produced `"Red, blue, black"` — `---` was a genuine (if odd) model output on the tiny image, not a pipeline defect.

### New findings from the retest session

F11 (`gh_select` cross-thread), F12 (`gh_list_components` NRE — froze Rhino under the debugger), F13 (`any_provider` dead path), F14 (`gh_disconnect` silent no-op) — detailed in §2. Test-rig notes (not product bugs): GH Boolean Toggle ignores synthetic single/double clicks (needs real UI interaction); `canvas_view` `setCenter`/`frameGuids` return mid-animation viewport values — reads must wait for GH's eased pan to settle.

---

## 5. Harness/environment findings (not product bugs)

- **VS MCP `output_read`** fails on all panes — prevented reading raw Debug output; forced reliance on `gh_report`/runtime messages.
- **GhJSON panel semantics**: `gh.panel.multiline:true` doesn't make a panel emit multiple items — `red\ngreen\nblue` arrives as one item; `internalizedData` on Panel outputs is ignored (panels emit `text`). The three "list conversion" errors are a fixture artifact; real multi-item sources (3 panels) work fine.
- **Repeated provider warning**: `[Validation] Provider 'X' could not be verified - hash check unavailable due to network issues.` appears once *per component* (18+ copies). Environment-side (verification endpoint unreachable), but the per-component duplication is noisy — could be deduplicated (F5-adjacent).
- **Tidy-up quality**: works, but produced overlaps with objects not in the tidy set and stacked two mid-column components — serviceable, not clean.
- **`gh_get_by_guid` param name inconsistency**: `guidFilter` here vs `instanceGuids` on other tools — minor API inconsistency.

---

## 6. Recommendations

1. **Audio (highest impact)**: add a binary-response path to `AIProvider.Call` (or route speech through base64-in-JSON endpoints); implement `/audio/speech` + `/audio/transcriptions` bodies in MistralAI; make endpoint routing consider payload modalities, not just request capability; emit `AIInteractionAudio` from Gemini decode; have `AI2Speech` read audio interactions, not `GetLastAssistantText`.
2. **MCP AI tools**: resolve `Provider` from `SmartHopperSettings` (or surface a clear "no provider configured" error instead of bare validation failure).
3. **Boolean parser**: extract the answer token (first/last line, regex `\b(true|false)\b`, or prefer tool/JSON mode over substring scan).
4. **Error UX**: deduplicate `[Provider]`/`[Return]` pairs; don't emit `finish_reason:"stop"` on failed calls; clear stale persistent messages on re-solve; dedupe provider-integrity warnings per provider.
5. **Validation**: extend capability check to payload modalities; surface per-provider parameter limits (e.g. temperature ≤1.5) at Settings/validate time; prune or flag retired registry models.
6. **GhJSON tooling**: report `acceptedChanges` vs actual placements; support true in-place update by `instanceGuid`; document that `connections` in `gh_put` are document-scoped (use `gh_connect` for existing objects).

## 7. GitHub issue triage

Evidence-based checklist updates + comments were posted to all 16 tracked testing issues. Three were closed as fully verified:

| Issue | Outcome |
|-------|---------|
| #528 A1 TidyUp | **Closed** — all 6 functional items pass (islands, 22-object large def, param alignment verified this session). Dev-only Sugiyama determinism noted. |
| #530 A4 Diff/Patch | **Closed** — all 8 functional items pass incl. live `Apply GhPatch to Canvas` (Panel `f0c15497` at 350,300). Checksum-mismatch refusal exercised live. |
| #541 B9 JSON | **Closed** — all items pass on the shipped component set (Get/Set/Sanitize/Array/ToTextList/Object/ToText/Schema). Issue's "Get/Add/Remove Item" names don't exist in this build; shipped equivalents cover the operations. |
| #534 B2 Audio | Open — Audio Viewer + icons verified; **TTS and STT confirmed broken** (F1, F3); Gemini blocked; persistence untested. |
| #535 B3 Settings | Open — 5/6 items verified (incl. plain-model back-compat); 1.4.x file opening unavailable. |
| #536 B4 Input | Open — File/Image/URL verified; Audio wraps but no provider transcribes (F3); multi-adapter chaining untested. |
| #537 B5 Output | Open — JSON/chained/metrics verified; AI to Speech fails (F1); AI to Image + fan-out untested. |
| #538 B6 Renames | Open — categories + functionality preserved verified (GUID-stable for old files); no 1.4.x file to test migration. |
| #539 B7 Batch | Open — submit/poll/progress verified; provider-side completion never arrived; Gemini/data-tree/cancel/fallback untested. |
| #533 B1 Gemini | Open — **blocked**: provider not configured. |
| #543 B11 Providers | Open — provider switching verified; per-provider model tests not exercised. |
| #545 B13 Scripts | Open — C# generate/execute/review all pass; **Python generation produces empty component** (F15). |
| #546 B14 Viewers | Open — both viewers + icons verified; multi-image + persistence untested. |
| #547 B15 Persistence | Open — save/reload needs doc reopen (kills in-canvas MCP); GhJSON-level round-trip verified only. |
| #548 B16 Timeouts | Open — default/short(1s)/long(600s) verified; cancel/network/rate-limit untested. |
| #549 B17 Fallback | Open — `disabled`/`configured_provider` modes verified; **`any_provider` is dead code** (F13) so automatic fallback can never engage. |

## 8. Reproduction notes

All findings reproducible on the saved canvas `test-assets/e2e-canvas.gh` + assets in `test-assets/` (`tone.wav`, `test.png`, `vision-big.png`, `sample.md`, `sample.txt`, `saved-view.png`). Key instance GUIDs for the notable chains are recorded inline in each finding (`AI→Speech` `89ee5f91`, fallback twin `851842f8`, batch `37892686`, STT `8667b8d4`, Gemini `338cd9d6`, timeout `f88a8e93`, ScriptGen `b2f146bc`, placed script `4bd4b579`, ImgView `9ae355ad`, Canvas2Img `531c91de`, ScriptRev `873d4959`, Img2Text-FB `1e44f63f`, vision AI→Text `ab6bdb2c`).
