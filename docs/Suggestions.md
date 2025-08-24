# Architecture Suggestions (SmartHopper)

This document outlines an idealized refactoring to centralize concerns, reduce duplication, and improve reliability across SmartHopper. It complements existing docs under `docs/Providers/`, `docs/Components/`, `docs/Context/`, `docs/Tools/`...

Breaking changes are acceptable; this is a forward-looking plan.

## Guiding principles

- Single responsibility per module; move cross-cutting logic to reusable services.
- Provider-agnostic orchestration in Infrastructure; provider specifics isolated in `SmartHopper.Providers.*`.
- Immutable core data structures with builders for composition.
- Explicit, composable policies for validation, security, retries, and telemetry.

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

### Suggestion 1 — ConversationSession (central multi‑turn engine) (S1)

- New service in `src/SmartHopper.Infrastructure/AICall/ConversationSession/` that owns multi‑turn orchestration and streaming.

- Core contracts:
  - `IConversationSession`: `Start(AIBody|AIBodyBuilder)`, `RunToStableResult(SessionOptions)`, `Stream(IConversationObserver)`, `Cancel()`.
  - `IConversationObserver`: `OnStart(AIRequestCall)`, `OnPartial(AIReturn delta)`, `OnToolCall(AIInteractionToolCall)`, `OnToolResult(AIInteractionToolResult)`, `OnFinal(AIReturn final)`, `OnError(Exception|AIReturn error)`, `OnMetrics(AIMetrics m)`.
  - `SessionOptions`: `ProcessTools`, `MaxTurns`, `MaxToolPasses`, `AllowParallelTools`, `RetryPolicyRef`, `CancellationToken`.

- Engine design (state machine):
  - States: `Idle → CallingProvider → HandlingToolCalls → CallingProvider (next turn) → Completed|Failed|Cancelled`.
  - Maintains transcript as authoritative source: `List<IAIInteraction>`; each turn appends user/assistant/tool messages.
  - Tool loop: collect `AIInteractionToolCall` from provider result → dispatch to Tool Runtime → append `AIInteractionToolResult` → continue.
  - Emits streaming deltas via `IStreamingAdapter` where providers support it; buffers to coherent `AIReturn` frames for observers.

- Integrations:
  - Providers: reuse existing template method (`PreCall → FormatRequestBody → CallApi → PostCall`). `ConversationSession` wraps `AIRequestCall.Exec()` and the provider‑specific streaming adapter.
  - Policies: request/response middleware (validation, redaction, caching, retries) applied per turn (see Suggestion 3).
  - Json schemas: rely on `JsonSchemaService` to attach/wrap/unwrap (see Suggestion 4).
  - Metrics: publish per‑turn and per‑session metrics to `AIMetricsService` (see Suggestion 7).

- Streaming & backpressure:
  - Standardize stream events across providers via `IStreamingAdapter` (see Suggestion 9). The session surfaces `OnPartial(AIReturn delta)` as soon as text/tool deltas arrive.
  - Backpressure: buffer to N chars or T ms windows; observers can signal slower consumption. Cancellation propagates to provider HTTP call.

- Caching hooks:
  - Expose request/response fingerprints (body hash, tool inputs, external resource hashes) so policies can enable provider‑side prompt caching tags/ETags and local memorization.

- Cancellation:
  - Accept external `CancellationToken`; wire to provider HTTP and tool tasks. Provide `Cancel()` for UI (WebChat button) and auto‑cancel on dialog close/Rhino closing.

- API surface for components:
  - Replace bespoke loops with: `var session = ConversationSession.Start(body); await session.RunToStableResult(opts);` or use `await foreach (var delta in session.Stream(opts, token)) { /* UI update */ }`.
  - Base components (`StatefulAsyncComponentBase`, `AIStatefulAsyncComponentBase`) get helpers to stream updates and progress automatically.

- Phased migration plan:
  1. Introduce `ConversationSession` behind `AIRequestCall` (non‑streaming path), feature‑flagged. Keep existing component code.
  2. Implement `IStreamingAdapter` for OpenAI/Mistral/DeepSeek; expose `Stream()`; add cancellation plumbing end‑to‑end.
  3. Move tool handling into the session (single source of truth). Components simply build `AIBody` and observe.
  4. Switch components to use session helpers from base classes; delete duplicated orchestration code.

- Testing:
  - Contract tests with fake provider/tool to verify transcripts, retries, cancellation, and schema handling.
  - Stream tests: order, coalescing logic, backpressure, finalization integrity (final equals fold(partials)).

- Documentation:
  - Add `docs/Providers/ConversationSession.md` and update `docs/Providers/AICall/index.md` and `docs/Providers/AICall/Policies.md`.

- Out‑of‑scope refactors referenced (to be handled by other suggestions):
  - Policy pipeline (Suggestion 3), JsonSchemaService (Suggestion 4), Tool Runtime decoupling + ToolResultNormalizer (Suggestion 5), AIBody immutability (Suggestion 6), Metrics/Progress services (Suggestion 7), Streaming adapter (Suggestion 9), Security policies (Suggestion 10).

- Side‑effects and mitigations:
  - Breaking changes: Components that directly loop over tool calls will become obsolete.
    - Mitigation: Introduce shims in base classes; keep old methods marked [Obsolete] for one release.
  - Provider streaming differences can surface new bugs.
    - Mitigation: Implement provider‑specific `IStreamingAdapter` with contract tests; add fallback to non‑streaming.
  - Potential performance regressions from buffering/observers.
    - Mitigation: Make buffering policy‑driven and tunable; measure with metrics service.
  - Cancellation races (UI closes while tool runs).
    - Mitigation: Link token sources; ensure tool runtime honors cancellation with best‑effort cleanup.

- UI integration — WebChat as AIReturn renderer (live updates):
  - Replace imperative UI updates with an observer that renders `AIReturn` deltas.
  - WebChat subscribes to `ConversationSession.Stream()` and only concerns itself with rendering interactions/partials/metrics; no provider/tool logic inside UI.
  - Supports: live assistant text, live tool_call/tool_result blocks, status/metrics, cancel button wiring to `Cancel()`.
  - Files impacted: `src/SmartHopper.Core/UI/Chat/WebChatDialog.cs`, `src/SmartHopper.Core/UI/Chat/WebChatUtils.cs` (subscribe/unsubscribe, pass CancellationToken, remove `Exec()`/`OverrideInteractions` usage).
  - This aligns with “Future thoughts” on streaming, cancellation, local caching, and richer WebChat UI.

### Suggestion 3 — Request/Response policy pipeline (S3)

- Policies under `src/SmartHopper.Infrastructure/AICall/Policies/`:
  - Request: context injection, prompt templating, capability validation, schema attach, moderation, retries/backoff, provider prompt‑cache tags/ETags when available.
  - Response: decoding, schema validation, post‑parse validators, redaction, telemetry, local response caching with TTL and invalidation keyed by `AIBody` fingerprint and external resource hashes.
- Middleware‑style composition for uniform cross‑cutting behavior.
- Impacted areas: `src/SmartHopper.Infrastructure/AICall/Policies/`
- Phased migration plan:
  1. Introduce `ConversationSession` + basic Policies behind `AIRequestCall` (no component changes yet).
  2. Introduce `IValidator<T>` chains; move GH validations into validators.
- Remember to test... Policy pipeline unit tests (retry, redaction, schema attach/validate).
- Remember to update documentation in... `docs/Providers/AICall/Policies.md`, `docs/Providers/AICall/index.md`

  #### Objectives (S3)
  
  - Centralize cross-cutting request/response behavior behind a composable, testable middleware pipeline.
  - Remove ad-hoc logic from providers and components; make ordering explicit and deterministic.
  - Enable retries, redaction, validation, caching, and telemetry uniformly across providers.
  
  #### Contracts (S3)
  
  - `IRequestPolicy` and `IResponsePolicy` (proposed): `Task InvokeAsync(PolicyContext ctx, Func<Task> next, CancellationToken ct)`.
  - `PolicyContext` (proposed): carries `AIBody`, `EffectiveInteractions`, `AIContextSnapshot`, `BodyFingerprint`, `ProviderOptions`, `HttpRequestInfo`, `HttpResponseInfo`, and a `Bag` for policy data.
  - `PolicyResult` (proposed): immutable snapshot after execution for telemetry and debugging.
  
  #### Policy taxonomy and ordering (S3)
  
  - Request policies (typical order):
    1) ContextInjectionPolicy → 2) PromptTemplatingPolicy → 3) CapabilityValidationPolicy → 4) SchemaAttachPolicy → 5) RedactionPolicy (preflight logs) → 6) RetryPolicy (wrapping provider call) → 7) CacheTaggingPolicy (provider prompt-caching hints).
  - Response policies: DecodePolicy → SchemaValidatePolicy → PostParseValidators → RedactionPolicy (persist/log) → TelemetryPolicy → LocalCachePolicy.
  - Each policy should be pure (no hidden global state) and respect cancellation.
  
  #### Error handling & retries (S3)
  
  - Exponential backoff with jitter and a max cap; idempotency keyed by `BodyFingerprint`.
  - Retry only on transient categories (HTTP 5xx, timeouts, rate limits); propagate final failure reason consistently.
  - Emit metrics for attempts, delay, and terminal error category.
  
  #### Integration points (S3)
  
  - `AIRequestCall` uses the pipeline to build `EffectiveInteractions` and wrap the provider call.
  - `ConversationSession` invokes the pipeline each turn; surfaces cancellation and tool loops.
  - Emits `AIMetricsEvent` to `AIMetricsService` for cache/validation/retry metrics (see Suggestion 7).
  - Calls into `JsonSchemaService` when attaching/validating schemas (see Suggestion 4).
  
  #### Security & PII (S3)
  
  - Redaction policy removes sensitive text from logs/metrics; only lengths and hashes.
  - Egress controls cooperate with Security policies (see Suggestion 10) to block disallowed tool/provider egress.
  
  #### Performance (S3)
  
  - Minimize allocations; reuse contexts; avoid reflection in hot paths.
  - Short-circuit policies when inputs are absent (e.g., no schema set, skip schema policies).
  
  #### Testing (S3)
  
  - Unit: policy ordering determinism, retry math, redaction correctness, idempotency by fingerprint.
  - Integration: fake providers for transient failures; ensure final equals fold(partials) is preserved.
  - Regression: pipeline changes do not alter provider request formatting unexpectedly.
  
  #### Migration plan (S3)
  
  - Phase 1: Introduce minimal pipeline with ContextInjectionPolicy + SchemaAttachPolicy; behind a feature flag.
  - Phase 2: Add RetryPolicy, RedactionPolicy, TelemetryPolicy; enable for select components.
  - Phase 3: Add LocalCachePolicy and CapabilityValidationPolicy; remove legacy logic from providers/components.
  
  #### Acceptance criteria (S3)
  
  - Deterministic policy ordering; cancellation honored; retries bounded and observable.
  - Redaction applied to all logs/metrics; schemas attached/validated when provided.
  
  #### Impacted areas (S3)
  
  - `src/SmartHopper.Infrastructure/AICall/Policies/`, `src/SmartHopper.Infrastructure/AICall/AIRequestCall.cs`, `src/SmartHopper.Infrastructure/AICall/ConversationSession/`
  
  #### Alignment with “Future thoughts” (S3)
  
  - Provider prompt caching: CacheTaggingPolicy emits tags/ETags keyed by `BodyFingerprint`.
  - Cancellation: unified cancellation through the pipeline.
  - Local response caching: LocalCachePolicy reads/writes by fingerprint + context snapshot hash.
  - Parallel tool calling: policies are re-entrant and per-request scoped.
  
  ### Suggestion 4 — JsonSchemaService (S4)
  
  - Centralize schema wrapping/unwrapping and validation for all providers.
  - Helpers for common shapes: `List<T>`, `ImageResult`, `GhJsonSpec`.
  - Removes provider divergence for non‑object schemas.
  - GHJSON pipeline helpers: generation/validation utilities for GH definitions and adapters to support saving Grasshopper files in GHJSON format.
  - Impacted areas: `src/SmartHopper.Infrastructure/AICall/JsonSchemaService/`, Providers under `src/SmartHopper.Providers.*`
  - Phased migration plan:
    1. Add `JsonSchemaService`; remove provider-specific schema quirks gradually.
  - Remember to test... Policy pipeline unit tests (schema attach/validate).
  - Remember to update documentation in... `docs/Providers/JsonSchemaService.md`, `docs/Providers/AICall/index.md`

  #### Objectives (S4)
  
  - Provide a single, provider-agnostic service to define, attach, and validate JSON Schemas.
  - Ensure consistent request/response shapes across providers, including arrays and primitive top-level types.
  - Improve developer ergonomics with helpers for common shapes and GHJSON-specific utilities.
  
  #### Contracts (S4)
  
  - `IJsonSchemaService` (proposed):
    - `bool TryAttach(AIBodyBuilder builder, string schemaJson, out string error)`
    - `ValidationResult Validate(string schemaJson, string json)`
    - `string WrapForProvider(string schemaJson, string provider)` / `string UnwrapFromProvider(...)`
    - Caches compiled schemas; thread-safe.
  - `ValidationResult` (proposed): counts of errors/warnings/info and messages; no PII payloads.
  
  #### Supported shapes & helpers (S4)
  
  - Helpers to produce schemas for `List<T>`, `ImageResult`, and `GhJsonSpec`.
  - Strict mode vs lax mode options for validators.
  
  #### Provider adapters (S4)
  
  - Normalize provider-specific quirks for function calling/tool schemas and response schema hints.
  - Maintain adapter mappings under provider projects; keep contracts in Infrastructure.
  
  #### Integration points (S4)
  
  - Used by `SchemaAttachPolicy` and `SchemaValidatePolicy` in the pipeline (Suggestion 3).
  - Referenced by components/tools that need to generate schemas at authoring time.
  - Document behavior in `docs/Providers/JsonSchemaService.md` with examples per provider.
  
  #### Security & PII (S4)
  
  - Strip/avoid embedding raw examples that could leak PII; keep examples synthetic.
  - Validation messages return paths and categories, not payload excerpts.
  
  #### Performance (S4)
  
  - Compile and cache schemas; reuse across requests; avoid reflection-heavy validation in hot paths.
  - Provide fast-path for trivial shapes (arrays of primitives).
  
  #### Testing (S4)
  
  - Unit: wrap/unwrap fidelity per provider; validation errors/warnings categorization; concurrency safety.
  - Integration: end-to-end schema attach + validate through the pipeline.
  
  #### Migration plan (S4)
  
  - Phase 1: Introduce service and use in `SchemaAttachPolicy` for new components.
  - Phase 2: Remove provider-specific schema code and consolidate on adapters.
  - Phase 3: Add GHJSON helpers; update docs and samples.
  
  #### Acceptance criteria (S4)
  
  - One place to attach/validate schemas; consistent behavior across providers; helpful, non-PII validation errors.
  
  #### Impacted areas (S4)
  
  - `src/SmartHopper.Infrastructure/AICall/JsonSchemaService/`, `src/SmartHopper.Infrastructure/AICall/Policies/`, Providers under `src/SmartHopper.Providers.*`
  
  #### Alignment with “Future thoughts” (S4)
  
  - Streaming: schema hints can shape partial decoding where supported.
  - Provider prompt caching: schemas contribute to stable request fingerprints.
  - GHJSON storage: helpers ensure definitions are safe to persist and validate.
  
  ### Suggestion 5 — Tool Runtime decoupling + ToolResultNormalizer (S5)
  
  - Move generic tool contracts/execution to `src/SmartHopper.Infrastructure/AITools/`.
  - Keep GH adapters in `src/SmartHopper.Core.Grasshopper/AITools/`.
  - `ToolResultNormalizer.Normalize(toolName, JObject)` guarantees consistent top‑level keys and error envelope.
  - Define AI tool(s) for GH authoring: e.g., `gh_generate_definition` producing a `GhJsonSpec`, plus adapters/utilities to save/export GH in GHJSON safely.
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
  
  ### Suggestion 6 — AIBody immutability via builder (S6)

This suggestion removes hidden side‑effects from `AIBody` (notably context injection during `Interactions` access) and makes requests reproducible, hashable, and policy‑driven.

#### Objectives

- Eliminate implicit mutation and side‑effects when reading `AIBody.Interactions`.
- Make `AIBody` a small immutable value object suitable for hashing/fingerprinting and caching.
- Move dynamic enrichment (context injection, schema attach) to explicit Policies.
- Provide an ergonomic `AIBodyBuilder` for authoring.

#### Current state and pain

- `AIBody` is mutable; `Interactions` getter injects context based on `ContextFilter` at read time (`src/SmartHopper.Infrastructure/AICall/AIBody.cs`).
- Counts and queries (`InteractionsCount()`, pending tool calls) depend on when/where `Interactions` is read, making behavior timing‑dependent.
- Hard to compute stable fingerprints for caching/retries; discourages clean policy composition.

#### Proposed types (immutable core + builder)

- `AIBody` (immutable record)
  - Properties: `IReadOnlyList<IAIInteraction> Interactions`, `string ToolFilter`, `string ContextFilter`, `string JsonOutputSchema`.
  - No logic that changes returned values based on external state.
  - Methods: `With(...)` to copy with modifications, `Fingerprint()` to compute stable hash of content (ordered interactions + filters + schema).

- `AIBodyBuilder`
  - Fluent API to construct/modify bodies:
    - `AddUser(string)`, `AddAssistant(string)`, `AddToolCall(...)`, `AddToolResult(JObject, messages)`, `SetToolFilter(string)`, `SetContextFilter(string)`, `SetJsonSchema(string)`.
    - `Build()` returns immutable `AIBody`.
  - Optional: `From(AIBody)` static factory to fork from an existing body.

- `EffectiveInteractions` concept (outside AIBody)
  - A Policy computes a concrete list used for encoding: `IReadOnlyList<IAIInteraction> EffectiveInteractions(AIBody body, IContextSnapshot ctx)`.
  - Replaces today’s implicit injection performed by `AIBody.Interactions` getter.

#### Policy changes (out of AIBody)

- Introduce `ContextInjectionPolicy` (Request policy):
  - Reads `AIBody.ContextFilter`, pulls values via `AIContextManager`, and prepends a `Context` interaction if non‑empty.
  - Emits diagnostics (messages) if context keys are missing/empty.
  - Produces `EffectiveInteractions` for the pipeline.

- `SchemaAttachPolicy` (existing or new):
  - Attaches `JsonOutputSchema` if provided and validated by `JsonSchemaService` (see Suggestion 4).

#### Backward compatibility plan

- Temporary shim `AIBodyMutable` or `AIBodyFacade`:
  - Exposes the current mutable API and redirects to `AIBodyBuilder` under the hood.
  - `Interactions` returns the plain stored list (no injection), and a new helper `GetEffectiveInteractions()` bridges to the policy result.

- API compatibility helpers:
  - Keep existing static helpers like `AddInteraction(...)` as builder extensions.
  - Provide `AIBodyExtensions.ToBuilder()` to ease gradual migration.

#### Required refactors beyond Suggestion 6 (touched but implemented separately)

- `AIRequestBase`/`AIRequestCall`:
  - Consume `EffectiveInteractions` provided by the policy pipeline instead of reading `AIBody.Interactions` directly.
  - Use `AIBody.Fingerprint()` for retries, caching, and metrics aggregation keys.

- Provider encoders (`OpenAIProvider`, `MistralAIProvider`, `DeepSeekProvider`):
  - Expect already‑prepared interactions (`EffectiveInteractions`). Remove any ad‑hoc context insertions.

- Components and tools:
  - Switch to `AIBodyBuilder` in `list_generate.cs`, `script_new.cs`, `gh_generate.cs`, `AIImgGenerateComponent.cs`, etc.
  - Replace uses of `InteractionsCount()` with `EffectiveInteractionsCount()` from the request context.

These refactors are adjacent to S6 and may be executed in follow‑up PRs to minimize change size.

#### Migration steps (incremental and low‑risk)

1) Introduce new types behind the scenes
   - Add `AIBody` (immutable) and `AIBodyBuilder`. Keep old `AIBody` type name by aliasing or temporary rename: `AIBodyV2` to avoid conflicts, then swap when all call sites migrate.
   - Add `ContextInjectionPolicy` and wire it into `AIRequestCall` before provider encoding.

2) Add shims and compile clean
   - Add `AIBodyFacade` that implements the old API but delegates to the builder.
   - Ensure tests and components compile with zero behavior changes.

3) Switch pipeline to effective interactions
   - Change `AIRequestCall` to obtain `EffectiveInteractions` from policies; keep a feature flag to fall back to old path if needed.

4) Migrate priority tools/components
   - Update high‑traffic tools/components first: `list_generate`, `script_new`, `gh_generate`, `AIImgGenerate`.
   - Replace `InteractionsCount()` with a request‑time count exposed by the policy pipeline.

5) Remove legacy behavior
   - Drop side‑effects from any remaining getters; delete compatibility shim after all consumers move.

#### Risks and mitigations

- Hidden dependency on context side‑effects
  - Mitigation: Search for all `.Interactions` and `.InteractionsCount()` usages and switch to effective variants. Provide analyzer or debug Assert when legacy members are accessed in the new pipeline.

- Fingerprint instability
  - Mitigation: Define strict ordering and serialization for interactions; exclude volatile metrics from the hash; add deterministic tests.

- Provider behavior changes (slightly different placement of context)
  - Mitigation: ContextInjectionPolicy mirrors current injection format and position; include tests asserting the first message role and content.

- Temporary duplication of types/APIs
  - Mitigation: Short‑lived shim with TODOs and warnings; track with a deprecation plan in CHANGELOG.

#### Test plan

- Unit tests
  - Immutability: ensure `AIBody` cannot be mutated after build.
  - Context injection: given a `ContextFilter`, verify effective list prepends a single context interaction with expected content.
  - Pending tool calls and counts computed over effective interactions.
  - Fingerprint: same logical body yields same hash; different order/content yields different hash.

- Integration tests
  - Provider encoding receives the expected roles and message order for OpenAI/Mistral/DeepSeek.
  - Tools (`list_generate`, `script_new`) produce identical outputs pre/post migration.

#### Documentation updates

- Update `docs/Providers/AICall/index.md` with new `AIBody`/`AIBodyBuilder` usage and examples.
- Update `docs/Providers/AICall/body-metrics-status.md` to clarify effective vs stored interactions and where metrics are aggregated.
- Expand this suggestion’s section once merged with code references and examples.

#### Alignment with “Future thoughts”

- Streaming: immutable bodies and stable fingerprints simplify stream resumption and idempotent retries.
- Provider‑side prompt caching: fingerprints provide consistent cache keys; policies can add cache tags/ETags.
- Cancellation: policies maintain effective state outside the model; cancellation remains orthogonal.
- Local response caching: `AIBody.Fingerprint()` becomes the primary lookup key; can include context snapshot hash.
- Parallel tool calling: immutable interaction history avoids race conditions; policies can merge tool results deterministically.
- GHJSON generation/saving: clearer separation enables schema policies (Suggestion 4) without mutating request bodies.

#### Impacted areas

- `src/SmartHopper.Infrastructure/AICall/`, `src/SmartHopper.Core/ComponentBase/`, Providers under `src/SmartHopper.Providers.*`

#### Phased migration plan (condensed)

1. Land immutable `AIBody` + builder; add `ContextInjectionPolicy`.
2. Switch `AIRequestCall` to effective interactions behind a feature flag.
3. Migrate key tools/components; then providers.
4. Remove legacy/shims; update docs and CHANGELOG.

Remember to test... policy pipeline (schema attach/validate), effective interactions, and fingerprint stability.
Remember to update documentation in... `docs/Providers/AICall/index.md`, `docs/Providers/AICall/body-metrics-status.md`.

### Suggestion 7 — Metrics & progress services (S7)

This suggestion centralizes metrics and progress reporting across provider calls, tool execution, and streaming to deliver a consistent UX and robust diagnostics while preserving privacy.

#### Objectives (S7)

- Consolidate telemetry into dedicated services with clear contracts and minimal overhead.
- Provide standardized progress semantics for components and WebChat UI.
- Surface streaming, caching, and tool execution signals without leaking PII.

#### Assumptions (built on earlier suggestions) (S7)

- `ConversationSession` orchestrates multi‑turn/tool loops and streaming (`src/SmartHopper.Infrastructure/AICall/ConversationSession/`).
- `IStreamingAdapter` normalizes provider streaming events.
- Policy pipeline exists (validation, retries, caching, redaction).
- `AIBody` is immutable with `Fingerprint()` for correlation and caching keys.
- Tool Runtime is decoupled and can emit execution events.

#### Metrics taxonomy and identifiers (S7)

- **Identity & correlation**: `SessionId`, `TurnId`, `RequestId`, `ProviderRequestId` (if available), `BodyFingerprint`, `ToolInvocationId`.
- **Per‑call**: start/end timestamps, duration, HTTP status, retries, error category, bytes sent/received, token usage (prompt/completion), model, finish reason.
- **Per‑stream**: time‑to‑first‑token (TTFB), delta count, tokens/sec, bytes/sec, last delta size.
- **Per‑turn**: encoding time, schema wrap/unwrap time, provider call time, validation time.
- **Tools**: queueing, dispatch, execution time, result size, errors.
- **Caching**: local cache hit/miss, provider cache tags/ETags used, revalidation vs fresh.
- **Validation**: counts of errors/warnings/info emitted by validators.
- **Progress state**: coarse state transitions and optional percent if determinable.

#### Domain model (proposed types) (S7)

- Add under `src/SmartHopper.Infrastructure/AICall/Metrics/`:
  - `AIMetrics` (snapshot aggregator), `CallMetrics`, `TurnMetrics`, `StreamMetrics`, `ToolMetrics`, `CacheMetrics`, `ValidationMetrics`.
  - `AIMetricsEvent` with `AIMetricsEventType` (StartCall, StreamDelta, EndCall, ToolStart, ToolEnd, CacheEval, ValidationRun, Cancelled, Failed, Completed).
- All records immutable and include correlation keys.

#### Services and APIs (S7)

- `AIMetricsService` (pub‑sub, thread‑safe, in‑memory):
  - `Subscribe(IObserver<AIMetricsEvent> observer, MetricsFilter filter?)`
  - `Unsubscribe(token)`
  - `Publish(AIMetricsEvent evt)` (internal usage from orchestrators)
  - Optional: ring buffer for recent events with configurable retention.
- `ProgressService` (maps metrics → user progress):
  - State enum: `Idle`, `Working`, `Streaming`, `ToolExecuting`, `Waiting`, `Cancelling`, `Completed`, `Failed`.
  - Debounce UI updates (100–200ms). Provide simple percent when inferable (e.g., tool batches).

#### Integration points (S7)

- **ConversationSession** (`src/SmartHopper.Infrastructure/AICall/ConversationSession/`): publish `StartCall`/`EndCall`/`Cancelled`/`Failed`; per‑turn `Start`/`End`; tool loop `ToolStart`/`ToolEnd`.
- **AIRequestCall**: publish HTTP timings, payload sizes, status, provider token usage and finish reason.
- **IStreamingAdapter**: publish `StreamDelta`, compute TTFB and throughput.
- **Policy pipeline**: emit `CacheMetrics` (hit/miss/etag) and `ValidationMetrics`.
- **Components & UI**:
  - Base classes in `src/SmartHopper.Core/ComponentBase/` subscribe to `ProgressService`.
  - `WebChatDialog` (`src/SmartHopper.Core/UI/Chat/WebChatDialog.cs`) renders live metrics (TTFB, tokens/sec, tool badges) and uses `RhinoApp.InvokeOnUiThread` for UI safety.
- **Docs & reporting**: update `docs/Providers/AICall/body-metrics-status.md` with event fields and progress mapping.

#### Security, privacy, and retention (S7)

- Default‑on redaction: never store raw prompts/tool inputs; only lengths and hashes where helpful.
- Opt‑out switch in `SmartHopperSettings` for metrics collection; local only, no network export.
- Configurable retention window; align with OWASP guidance.

#### Performance considerations (S7)

- Lightweight immutable events; avoid allocations in hot streaming paths; batch publish when needed.
- Debounce UI updates; zero reflection on critical paths.

#### Testing strategy (S7)

- Unit: TTFB/throughput math, ordering guarantees, debouncing, state transitions (Working → Streaming → Completed; Working → Cancelling → Cancelled).
- Integration: fake provider/tools with deterministic streaming; fold(partials) = final; UI observers on main thread.
- Regression: metrics overlay does not alter `AIReturn` behavior.

#### Phased migration plan (S7)

1) Introduce `AIMetricsService` and `ProgressService` skeletons and wire basic start/end events.
2) Instrument `AIRequestCall` and `ConversationSession` for per‑turn and tool events.
3) Integrate `IStreamingAdapter` for TTFB/throughput + cancellation surfaced via metrics.
4) Update base components to subscribe; enhance `WebChatDialog` to render live metrics and cancellation state.
5) Emit cache/validation metrics from the policy pipeline; document event schemas.

#### Acceptance criteria (S7)

- Consistent progress across components without bespoke logic.
- Live TTFB and tokens/sec visible during streaming for OpenAI/Mistral/DeepSeek.
- Tool execution timing/errors surfaced in UI.
- Cancellation reflected immediately in progress; opt‑out respected; no PII leakage.

#### Impacted areas (S7)

- `src/SmartHopper.Infrastructure/AICall/Metrics/`, `src/SmartHopper.Infrastructure/AICall/ConversationSession/`, `src/SmartHopper.Infrastructure/AICall/Streaming/`, `src/SmartHopper.Core/ComponentBase/`, `src/SmartHopper.Core/UI/Chat/`

#### Alignment with “Future thoughts” (S7)

- **Provider streaming**: standardized streaming metrics and TTFB via `IStreamingAdapter`.
- **Provider‑side prompt caching**: `BodyFingerprint` enables cache‑key correlation; metrics show hit/miss and etag usage.
- **Cancellation**: progress states include `Cancelling`/`Cancelled`; tokens propagate to provider/tool tasks.
- **Local caching**: publish cache events for transparency and debugging.
- **Parallel tool calling**: tool metrics are per‑invocation with correlation IDs; aggregation remains deterministic.
- **WebChat UI improvements**: live metrics badges and throughput indicators enhance streaming UX.

- Remember to test... metrics ordering, debouncing, cancellation propagation, and streaming folding.
- Remember to update documentation in... `docs/Providers/AICall/body-metrics-status.md`, `docs/Components/index.md`.

### Suggestion 8 — Validation pipeline with typed validators (S8)

- `IValidator<T>` chains for request, response, and tools.
- GH‑specific validators (e.g., GhJSON) live in `SmartHopper.Core.Grasshopper` and plug into the pipeline.
- Impacted areas: `src/SmartHopper.Core.Grasshopper/AITools/`, `src/SmartHopper.Infrastructure/AICall/Policies/`
- Phased migration plan:
  1. Introduce `IValidator<T>` chains; move GH validations into validators.
- Remember to test... Policy pipeline unit tests (validate).
- Remember to update documentation in... `docs/Providers/AICall/Policies.md`, `docs/Components/index.md`

#### Objectives (S8)

- Establish a uniform validation layer for requests, provider responses, and tool inputs/outputs.
- Move scattered validation logic (e.g., GH component existence, type compatibility) into typed, composable validators.
- Make validations observable (metrics), reproducible, and non-PII.

#### Contracts (S8)

- `IValidator<T>`: `Task<ValidationResult> ValidateAsync(T target, ValidationContext ctx, CancellationToken ct)`.
- `ValidationContext`: `BodyFingerprint`, provider/model, selected policies, external resource hashes, feature flags.
- `ValidationResult`: counts and categorized messages: `Errors`, `Warnings`, `Info` with paths and codes; no payload excerpts.

#### Validator taxonomy and ordering (S8)

- Request validators: `AIBodyShapeValidator`, `SchemaDefinedValidator` (requires schema for certain tools), `CapabilityMatchValidator`.
- Response validators: `JsonSchemaResponseValidator`, `ToolCallShapeValidator`, `AssistantContentValidator`.
- Tool validators: `GhComponentExistsValidator`, `GhTypeCompatibilityValidator`, `FilePathSafetyValidator`.
- Ordering: cheap structural checks → capability/schema checks → domain-specific validators.

#### Integration points (S8)

- Plugged via the Policy pipeline: `SchemaValidatePolicy` invokes response validators; request/tool validators run pre-dispatch.
- `ConversationSession` aggregates `ValidationMetrics` per turn and publishes via `AIMetricsService`.
- GH-specific validators live under `src/SmartHopper.Core.Grasshopper/Validators/` and are registered conditionally.

#### Required refactors beyond S8 (S8)

- Extract current GH validation from `GHJsonAnalyzer.cs` and tools like `gh_generate.cs` into `IValidator<GhJsonSpec>` implementations.
- Replace ad-hoc checks in components with validator registration calls.
- Ensure providers no longer perform schema checks; defer to `JsonSchemaService` and validators (S4, S3).

#### Side-effects and mitigations (S8)

- Behavior changes where legacy permissive checks become strict.
  - Mitigation: start with Warning-only mode; gate strict mode by feature flag per tool.
- Performance overhead from multiple validators.
  - Mitigation: memoize compiled schemas; short-circuit on first fatal error; parallelize independent validators where safe.

#### Security & PII (S8)

- Validators must avoid echoing raw content; include only paths, codes, and summaries.
- File and network validators must respect Security policies (S10) and egress allowlists.

#### Performance (S8)

- Cache compiled schemas and reusable metadata; avoid reflection in hot loops.
- Batch validation messages and debounce UI emissions.

#### Testing (S8)

- Unit: validator-specific rule coverage (component existence, type compatibility, schema errors).
- Integration: pipeline invokes validators in order; metrics reflect counts; warnings vs errors behavior.
- Regression: outputs of `gh_generate` unchanged when errors absent; warnings surfaced but non-blocking.

#### Migration plan (S8)

1) Introduce `IValidator<T>` and baseline validators (`AIBodyShapeValidator`, `JsonSchemaResponseValidator`).
2) Port GH validations from `GHJsonAnalyzer.cs` to `GhComponentExistsValidator` and `GhTypeCompatibilityValidator`.
3) Wire validators through `SchemaValidatePolicy` and tool execution prechecks.
4) Enable strict mode opt-in per component/tool; later make strict-by-default.

#### Acceptance criteria (S8)

- Deterministic validator ordering and consistent results across providers.
- Non-PII error messages with clear paths and codes.
- Metrics emitted for validation steps; ability to toggle strictness per feature.

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

- `AIProvider.Call` wrapped by `RetryPolicy` and `RedactionPolicy` to ensure no sensitive data in telemetry.
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

---

## Future thoughts (suggestions should consider future conditions and try to be flexible to accept them in the future)

- [ ] AI Provider-side prompt caching
- [ ] AI Call cancellation token, callable by the user in components (cancelled state), and automatically called on closing webchat dialog or rhino
- [ ] AI Call local caching of responses to avoid recomputation
- [ ] AI Call compatibility with parallel tool calling
- [ ] AI tool to generate Grasshopper definitions in GHJSON format
- [ ] New way to save Grasshopper files in GHJSON format to disk
- [ ] Improve WebChat UI with full html environment, including dynamic loading messages, supporting streaming, different interaction type (image, audio, text, toolcall, toolresult...)
- [ ] Add compatibility in persistant data storage and GhJson to more Grasshopper native data types (colors, points, vectors, lines, plane, circle...) <- they require a way to safely store them as string and restore them to GH_Types
- [ ] Support new Responses API from OpenAI
