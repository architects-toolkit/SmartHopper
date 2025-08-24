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
