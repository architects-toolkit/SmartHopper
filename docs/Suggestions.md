# Architecture Suggestions (SmartHopper)

This document outlines an idealized refactoring to centralize concerns, reduce duplication, and improve reliability across SmartHopper. It complements existing docs under `docs/Providers/`, `docs/Components/`, `docs/Context/`, `docs/Tools/`...

Breaking changes are acceptable; this is a forward-looking plan.

## Guiding principles

- Single responsibility per module; move cross-cutting logic to reusable services.
- Provider-agnostic orchestration in Infrastructure; provider specifics isolated in `SmartHopper.Providers.*`.
- Immutable core data structures with builders for composition.
- Explicit, composable policies for validation, security, and retries.

## Current strengths (to keep)

- Template-method provider pipeline (`PreCall → FormatRequestBody → CallApi → PostCall`) in `AIProvider`.
- Central capability registry and model resolution (`ModelManager`, `AIModelCapabilityRegistry`).
- Context injection at request level (`AIBody`), component base classes, and secure provider loading.

## Pain points to address

- Multi-turn/tool orchestration repeated in components.
- Tool result shapes normalized by convention, not centrally enforced.
- Schema wrapping/unwrapping differs across providers.
- Metrics/progress logic scattered between bases and components.
- Model selection fallback logic appears in multiple places.
- GH-specific tool code mixed with generic tool runtime concerns.
- Validation split across tools/components without a uniform pipeline.

## Suggestions (independently applicable)

### Suggestion 5 — Tool Runtime decoupling + ToolResultNormalizer (S5)

- Move generic tool contracts/execution to `src/SmartHopper.Infrastructure/AITools/`.
- Keep GH adapters in `src/SmartHopper.Core.Grasshopper/AITools/`.
- `ToolResultNormalizer.Normalize(toolName, JObject)` guarantees consistent top‑level keys and error envelope.
- Define AI tool(s) for GH authoring: e.g., `gh_generate_definition` producing a `GhJsonSpec`, plus adapters/utilities to save/export GH in GHJSON safely.
- Define Validators for input/output of tool calls and results. Use Validators before parsing the result in Components to avoid exceptions.
- Impacted areas: `src/SmartHopper.Infrastructure/AITools/`, `src/SmartHopper.Core.Grasshopper/AITools/`
- Phased migration plan:
  1. Extract Tool Runtime core; add `ToolResultNormalizer`; keep GH adapters.
- Remember to test... ToolResultNormalizer shape tests.
- Remember to update documentation in... `docs/Tools/Runtime.md`, `docs/Components/index.md`

#### Objectives (S5)

- Decouple tool definitions and execution from Grasshopper specifics; make tools first-class and testable.
- Standardize tool result shapes and error envelopes across all tools.
- Support cancellation, timeouts, quotas, and safe egress policies.

#### Contracts (S5)

- `ITool` (proposed): id, name, description, JSON schema for input/output.
- `IToolExecutor` (proposed): `Task<ToolOutcome> ExecuteAsync(ITool tool, JObject input, ToolContext ctx, CancellationToken ct)`.
- `ToolContext` (proposed): correlation ids, `AIContext`, temp storage, security policies, and logger/metrics.
- `ToolOutcome` (proposed): normalized `JObject` result + error envelope when applicable.

#### Execution model (S5)

- Queueing and dispatch: single-threaded or limited parallelism, configurable per tool.
- Cancellation: tokens propagated; best-effort cleanup.
- Timeouts/quotas: per-tool limits; metrics emitted.
- Egress allowlists and sandboxing: limit file/network access per policy (see Suggestion 10).

#### ToolResultNormalizer (S5)

- Enforce top-level shape: `{ ok: bool, result?: {...}, error?: { code, message } }`.
- Utilities for common result patterns (e.g., list outputs, binary payload refs).

#### Metrics (S5)

- Emit `ToolStart`/`ToolEnd` events with timings, result size, and error flag via `AIMetricsService` (Suggestion 7).
- Surface in `ProgressService` as `ToolExecuting` state with optional percent for batches.

#### Parallel tools (S5)

- Support optional parallel dispatch where the provider proposes multiple tool calls.
- Deterministic aggregation order (by tool index) for transcript consistency.

#### Integration points (S5)

- `ConversationSession` dispatches tools via `IToolExecutor` and appends `AIInteractionToolResult`.
- GH adapters live in `SmartHopper.Core.Grasshopper/AITools/` and wrap native types.

#### Security (S5)

- Enforce egress allowlists; redact sensitive tool inputs/outputs in logs/metrics.
- Graceful failures with error envelopes instead of throwing where possible.

#### Performance (S5)

- Avoid large object graphs in results; use references for big payloads; stream where possible.
- Bounded concurrency and backpressure when tool results are consumed by UI.

#### Testing (S5)

- Unit: normalizer shape tests; error envelopes; timeout/cancellation behavior.
- Integration: fake tools + session loop; parallel tool dispatch determinism.
- Regression: tool metrics do not alter AI response behavior.

#### Migration plan (S5)

- Phase 1: Introduce `ITool`/`IToolExecutor` contracts and normalizer; wrap existing GH tools.
- Phase 2: Replace direct component tool loops with session-managed dispatch.
- Phase 3: Enable optional parallel tool execution and quotas.

#### Acceptance criteria (S5)

- Tools execute via a common runtime; normalized results; cancellations/timeouts enforced; metrics emitted.

#### Impacted areas (S5)

- `src/SmartHopper.Infrastructure/AITools/`, `src/SmartHopper.Core.Grasshopper/AITools/`, `src/SmartHopper.Infrastructure/AICall/ConversationSession/`

#### Alignment with “Future thoughts” (S5)

- Parallel tool calling: first-class support with deterministic aggregation.
- WebChat UI improvements: tool badges and progress can be rendered from normalized events.
- Local caching: tool results can be memoized when inputs are pure and within size limits.
  
### Suggestion 10 — Security & data egress controls (S10)

- Redaction policies for requests/responses; tool egress allowlists.
- Schema whitelisting/catalog for `JsonOutputSchema`.
- Per‑tool quotas, timeouts, and cancellation.
- Impacted areas: `src/SmartHopper.Infrastructure/AICall/Policies/`, Providers under `src/SmartHopper.Providers.*`
- Phased migration plan:
  1. Enable default security policies (least privilege), opt‑in broader access.
- Remember to test... Policy pipeline unit tests (redaction).
- Remember to update documentation in... `docs/Providers/AICall/Policies.md`, `docs/Architecture.md`

#### Objectives (S10)

- Enforce least-privilege data handling and controlled egress for providers and tools.
- Centralize redaction, quotas, and schema whitelisting with auditable defaults.

#### Policy types and scope (S10)

- `RedactionPolicy`: strips PII from logs/metrics; configurable redaction rules; hashes where necessary.
- `EgressPolicy`: per-tool and per-provider allowlists for network/file access; deny by default.
- `QuotaPolicy`: per-tool timeouts, concurrency limits, and size caps.
- `SchemaWhitelistPolicy`: restricts `JsonOutputSchema` to approved catalogs to prevent injection/overreach.

#### Contracts (S10)

- Policies conform to the pipeline contract (S3) with `PolicyContext` containing identity and correlation fields.
- Security events emitted as `AIMetricsEvent` with `AIMetricsEventType.Security` or mapped to `Failed` with category.

#### Integration points (S10)

- `AIProvider.Call` wrapped by `RetryPolicy` and `RedactionPolicy` to ensure no sensitive data.
- `IToolExecutor` consults `EgressPolicy` before performing file/network ops.
- `JsonSchemaService` consults `SchemaWhitelistPolicy` before attaching schemas.

#### Threat model & references (S10)

- Follow OWASP recommendations; assess threats for prompt injection, exfiltration via tools, and schema abuse.
- Respect secure key storage via `SmartHopperSettings` OS secure store (see existing implementation) and provider trust model.

#### Required refactors beyond S10 (S10)

- Ensure ProviderManager signature verification remains default-on; keep test bypass via env vars only (see `SMARTHOPPER_SKIP_SIGNATURE_VERIFY`).
- Add sandbox helpers in Tool Runtime for safe temp file paths and path normalization.
- Harden HTTP timeouts/retries/cancellation in `AIProvider.CallAsync` as per planned resilience work.

#### Side-effects and mitigations (S10)

- Stricter defaults may break previously permissive flows.
  - Mitigation: staged rollout with logging-only mode; clear error messages with remediation hints.
- Performance cost for redaction and policy checks.
  - Mitigation: compile redaction regexes; avoid scanning large payloads; sample where acceptable.

#### Performance (S10)

- Zero-copy where possible; redact before serialization; avoid duplicate buffers.

#### Testing (S10)

- Unit: redaction coverage; egress allow/deny decisions; quota enforcement.
- Integration: end-to-end with tools performing file/network operations; ensure denial paths are safe and informative.

#### Migration plan (S10)

1) Implement `RedactionPolicy` with logging-only mode; instrument metrics.
2) Introduce `EgressPolicy` with deny-by-default for network and filesystem; allowlist critical tools.
3) Add `QuotaPolicy` and `SchemaWhitelistPolicy`; document catalog management.
4) Enable strict mode by default after stabilization.

#### Acceptance criteria (S10)

- No PII in logs/metrics; egress controlled and auditable; quotas enforced; schema usage constrained.

### Suggestion 11 — Settings storage and UI simplification (S11)

- __Pain__: Settings are defined across multiple places (load/save, JSON schema, model, UI), plus lazy-loading complexity.
- __Direction__: Create a single source of truth via a `SettingDescriptor` catalog (name, type, default, validation, secure, scope), from which we generate:
  - Load/save binding, JSON schema, and UI widgets.
  - Versioned migrations and validation.
  - Reduce/retire lazy loader in favor of explicit async initialization for provider‑specific settings.
- __First steps__: Inventory all settings; design `SettingDescriptor`; wire `SettingsDialog` to auto‑bind; add migration scaffolding.
- __Risks__: Breaking persisted settings; UI perf when many settings; cross‑provider differences.
- __Impacted areas__: `src/SmartHopper.Infrastructure/Settings/`, `SettingsDialog.cs`, Providers `GetSettingDescriptors()`.

### Suggestion 12 — WebChat UI in full HTML with streaming and multimodal (S12)

- __Goal__: Modern HTML/CSS/JS chat with dynamic message loading, token streaming, multimodal items (image, audio, text, toolcall/result), and live provider.model selector in UI (model can be chosen by the user in a dropdown so that every time the user sends a message they can also decide which model will process that request).
- __Direction__: Host a WebView and serve local assets. Render transcript diffs; support streaming deltas; show tool badges and progress; attachments for media; theming and accessibility.
- __First steps__: Spike WebView transcript renderer; integrate provider streaming adapter (OpenAI first); define event bus between `ConversationSession` and UI.
- __Risks__: UI threading (invoke on Rhino UI thread), sandbox/security of embedded web, performance with long transcripts.
- __Impacted areas__: `src/SmartHopper.Core/UI/Chat/WebChatDialog.cs`, `WebChatUtils.cs`, `src/SmartHopper.Infrastructure/AICall/ConversationSession/`, Providers streaming adapters.

### Suggestion 13 — End‑to‑end cancellation tokens (S13)

- __Goal__: Cancellation tokens propagated everywhere, user‑callable in components (with a Cancelled state), and auto‑cancel on WebChat/Rhino close.
- __Direction__: Thread tokens through `AIRequestCall.Exec`, provider HTTP calls, tool runtime, and `ConversationSession`. Add cancel controls in components and WebChat; ensure graceful partial result handling and cleanup.
- __First steps__: Audit APIs to add `CancellationToken` overloads; wire WebChat close → cancel; add component cancel button/state; provider HTTP cancellation compliance.
- __Risks__: Deadlocks or hangs if sync‑over‑async remains; inconsistent partial results; provider SDK limitations.
- __Impacted areas__: `src/SmartHopper.Infrastructure/AICall/`, Providers `CallApi`, `AIStatefulAsyncComponentBase`, WebChat.

### Suggestion 14 — GHJSON encode/decode service for native GH types (S14)

- __Goal__: Persist and restore more Grasshopper native types (Color, Point3d, Vector3d, Line, Plane, Circle, …) safely and loss‑aware.
- __Direction__: Introduce `GhTypeCodec` registry with converters (to/from canonical JSON/string) using culture‑invariant formatting and versioned schemas; integrate with persistent storage and validators.
- __First steps__: Prioritize a core set of types; implement codecs + round‑trip tests; update `GHJsonLocal.Validate()` to consult the registry; document shapes.
- __Risks__: Precision/units, culture differences, performance on large graphs, backward compatibility.
- __Impacted areas__: `src/SmartHopper.Core.Grasshopper/Utils/GHJsonLocal.cs`, persistence/serialization helpers, GH tools that consume/emit GHJSON.

---

## Future thoughts (suggestions should consider future conditions and try to be flexible to accept them in the future)

- [ ] AI Provider-side prompt caching
- [ ] AI Call local caching of responses to avoid recomputation
- [ ] AI Call compatibility with parallel tool calling
- [ ] AI tool to generate Grasshopper definitions in GHJSON format
- [ ] New way to save Grasshopper files in GHJSON format to disk
- [ ] Support new Responses API from OpenAI
