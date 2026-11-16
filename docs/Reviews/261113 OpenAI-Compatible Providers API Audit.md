# OpenAI-Compatible Providers — API Audit & Deduplication Review

**Date:** 2026-11-13
**Scope:** `SmartHopper.Providers.Ollama`, `SmartHopper.Providers.LocalAI`, plus the shared chat-completions pipeline across all OpenAI-compatible providers
**Method:** Source review against official API contracts — Ollama `openai/openai.go` (`main`), LocalAI `core/schema/openai.go`, `docs.ollama.com/openai`. **No live provider testing** (no local Ollama/LocalAI instances available); all findings are contract-level, not runtime-verified.
**Baseline commits:** `5d8a18a1` (provider extras), `c66c5b02` (image-gen extras).

---

## 1. Executive summary

- The local providers are structurally sound (derive from `OpenAICompatibleProvider<T>`, dedicated SSE adapters, configurable URLs) but carry **11 confirmed defects**, most in the "sent but silently ignored" class that produces no error.
- Commit `5d8a18a1` added `keep_alive` (correct) and `num_ctx` (**dead — see §2.3**).
- The duplication is wider than the two local providers: **six providers** each maintain their own `EncodeToJToken` + response `Decode` + ~300–400-line SSE adapter for the same OpenAI chat-completions wire format. Divergence between the copies has already produced real bugs (see §4).
- Recommended direction: a shared `OpenAICompatibleChatCodec` + concrete shared SSE adapter in ProviderSdk, migrated provider-by-provider (§5).

## 2. Confirmed defects — Ollama / LocalAI

Severity: **High** = feature silently broken or wrong output; **Med** = degraded behavior/metrics; **Low** = hygiene.

| # | Sev | File | Defect |
|---|-----|------|--------|
| 2.1 | High | `OllamaProvider.cs:416`, `LocalAIProvider.cs:522` | **Reasoning text dropped.** Both read `message["reasoning_content"]`; Ollama emits `message.reasoning` (and `delta.reasoning` streaming) — verified in `openai.go`. `reasoning_content` is only an inbound alias on LocalAI. Streaming adapters read neither field. `AppendReasoning` exists and is used by OpenAI's adapter — never called here. |
| 2.2 | High | `OllamaProvider.Streaming.cs:95`, `LocalAIProvider.Streaming.cs:95` | **No `stream_options.include_usage`.** Both set `stream=true` but never request usage → streamed calls always report 0 prompt/completion tokens. Both APIs support `StreamOptions.IncludeUsage`; OpenAI's adapter does this correctly. |
| 2.3 | High | `OllamaProvider.cs:227-232` | **Resolved — extra removed.** **`num_ctx` was dead (new in `5d8a18a1`).** Sends `options.num_ctx`, but Ollama's `ChatCompletionRequest` has **no `options` field** — the server builds options internally from mapped OpenAI fields (`max_tokens`→`num_predict`, etc.) and discards the client-supplied object. There is no `num_ctx` path on the OpenAI-compat endpoint; only native `/api/chat` accepts it. Extra gives false confidence while Ollama keeps truncating at its default context — the exact failure it targets. `keep_alive` on the same commit is **correct** (`keep_alive` is a real top-level field; `api.Duration` accepts int ns and duration strings). |
| 2.4 | Med | `OllamaProvider.cs:256` | **`tool_choice` silently dropped.** `ApplyOpenAICompatibleToolChoice` emits the field, but Ollama's request struct doesn't expose it → `ForceToolCall`/`ForceToolName` are no-ops on Ollama while appearing to work. |
| 2.5 | Med | `OllamaProvider.cs:202-205` | **Resolved — extra removed.** **`top_k` extra was dead** on Ollama's compat endpoint (native `/api` supports it; the compat layer doesn't map it). Was advertised in `GetExtraDescriptors`, silently discarded. |
| 2.6 | High | `OllamaProvider.cs:338+`, `LocalAIProvider.cs:425+` | **Vision/audio inputs degraded.** `AIInteractionImage` → `content = OriginalPrompt` (bytes never sent); `AIInteractionAudio` → `content:""`. Ollama accepts base64 `image_url` content parts (no remote URLs); LocalAI accepts `image_url`/`audio_url`/`video_url`/`input_audio`. `OpenAICompatibleImageCodec` exists for tool-result images — unused here. |
| 2.7 | Med | `OllamaProvider.cs:243-263`, LocalAI equivalent | **JSON schema prompt-injected, not enforced.** Sends `{"type":"json_object"}` + system message describing the schema. Both APIs support `{"type":"json_schema",...}` (grammar-constrained decoding). Weakest guarantee exactly where small local models need it most. |
| 2.8 | Med | `LocalAIProvider.cs:296-319` | **Resolved.** LocalAI image-gen extras fixed: `step` now emits the real `"step"` field (was `"steps"`); `cfg_scale`/`width`/`height` removed (not `OpenAIRequest` fields; size via `size:"WxH"` only — already sent). `seed`, `negative_prompt` were correct. |
| 2.9 | High | `AIProvider.cs:614` (shared, all providers) | **Locale bug in `GetSetting<double>`.** `double.TryParse(value.ToString())` uses current culture; on comma-decimal locales `"0.5"` parses as `5.0`. `ValidateTemperature` uses `InvariantCulture` so the value validates, then `Encode` sends `temperature: 5.0` — severe silent degradation. Affects every provider reading double settings. |
| 2.10 | Med | Both providers | **Resolved.** Both providers now read typed `Parameters.TopP`/`Parameters.Seed` first with extras as fallback (DeepSeek precedence chain); previously only `p.Extras` keys were read so typed overrides were ignored. |
| 2.11 | Low | Both providers | `RetrieveModels` returns empty — deliberate, but `/v1/models` exists on both servers and LocalAI exposes a capabilities endpoint; enabling it would let the registry validate capabilities and surface installed models. |

## 3. Non-blocking observations

- `response_format:{"type":"text"}`: verified harmless (Ollama ignores unknown types; LocalAI only acts on json types).
- `IsConfigured` inconsistency: cleared `ServerUrl` → `IsConfigured=false` while `DefaultServerUrl` still falls back to localhost; invalid non-absolute URL silently falls back too.
- `Logger.Error` used for non-error diagnostics ("Schema wrapper info stored") in both `Encode` methods — pollutes error logs.
- Unsupported-but-useful API surface not exposed: `reasoning`/`reasoning_effort` (both), `stop`, `min_p` (LocalAI), `timings` metrics (Ollama), `ref_images` img2img (LocalAI).
- No `docs/Providers/Ollama.md` / `LocalAI.md` while other providers have pages.
- Both providers compile cleanly with `SignAssembly=false`; no compile defects.

## 4. Duplication is a six-provider problem, not a two-provider one

Six providers each implement the same three pieces for the OpenAI chat-completions wire format:

| Provider | `EncodeToJToken` | SSE adapter | Distinct quirks |
|----------|:---:|---|---|
| Ollama | ✔ | `*.Streaming.cs` (387 ln) | extras, `keep_alive` |
| LocalAI | ✔ | `*.Streaming.cs` (387 ln, verbatim copy) | `/images/generations` leg, image extras |
| DeepSeek | ✔ | embedded @ `DeepSeekProvider.cs:801` | merges consecutive assistant msgs, `reasoning_content` replay |
| MistralAI | ✔ | embedded @ `MistralAIProvider.cs:872` | `thinking` content-parts, usage in last chunk |
| OpenRouter | ✔ | embedded @ `OpenRouterProvider.cs:819` | `provider{}` prefs, `reasoning` objects |

Not candidates: OpenAI (dual Responses/ChatCompletions format + model gating), Gemini (native API), Anthropic (native API).

**Divergence already produced bugs:** `stream_options.include_usage` is sent by **exactly one provider — OpenAI** — yet MistralAI/OpenRouter adapters *read* `parsed["usage"]` without requesting it, so streamed metrics are very probably zero there too (same defect class as §2.2, likely 4 more providers affected). Reasoning handling is learned differently per copy: DeepSeek one spelling, Mistral thinking-parts, Ollama/LocalAI neither in streaming.

## 5. Refactor options

- **A — intermediate `OpenAICompatibleChatProvider<T>`:** owns ServerUrl/auth/settings skeleton + chat `Encode`/`EncodeToJToken`/`Decode` + shared SSE adapter; derived keeps name, URL default, extras, endpoint routing. Kills ~90% of duplication.
- **B — composition:** concrete `OpenAICompatibleChatCodec` + `OpenAICompatibleChatSseAdapter` in ProviderSdk + shared local-settings base; providers keep thin shells. Lowest risk, matches `OpenAICompatibleRoleMapper`/`ImageCodec` precedent.
- **C — hoist into `OpenAICompatibleProvider<T>`:** rejected — silently changes contract surface for OpenAI/Mistral/DeepSeek/OpenRouter.

**Recommendation:** A's semantics via B's mechanics — land a concrete shared codec + SSE adapter in ProviderSdk (extension points: reasoning-field spelling, usage chunk policy, tool_call delta accumulation, message-merge policy, per-provider extra request fields, endpoint selection), pilot on Ollama + LocalAI with the defect fixes baked in, then migrate DeepSeek → Mistral → OpenRouter one provider per commit so quirks stay visible. A new OpenAI-compatible provider (LM Studio, vLLM, Groq…) becomes ~50 lines.

Bundle into the shared implementation: `reasoning ?? reasoning_content` + streaming reasoning accumulation, `include_usage`, `image_url`/`input_audio` content parts via existing codecs, `json_schema` response format, and a **known-unsupported-field warning** (per-provider request allowlist) so `tool_choice`/`top_k`/`options` get logged instead of silently dropped.

## 6. Suggested fix priority

1. `reasoning`/`reasoning_content` dual-read + streaming reasoning accumulation (all six).
2. `stream_options.include_usage` (all six — verify per-API support first).
3. Image/audio content-part encoding via existing shared codecs (Ollama/LocalAI, check others).
4. ~~Remove or reroute dead params~~: **done** — `num_ctx`, `top_k` on Ollama and `cfg_scale`/`width`/`height` on LocalAI removed; LocalAI `steps` renamed `step`. `tool_choice` on Ollama remains unencoded (tool schemas are still sent).
5. `json_schema` response format on both local providers.
6. Culture-safe `double` parse in `GetSetting<T>` (fixes every provider).
7. Typed `TopP`/`Seed` propagation.

## 7. Unverified live behavior

All findings derive from reading server-side request structs/schemas, not live calls. Confirm at first opportunity: Ollama `options` passthrough (expected ignored), `keep_alive` string vs int acceptance, `json_schema` enforcement quality on small models, LocalAI `input_audio` part shape, streamed `usage` chunk shape on each of the six providers.
