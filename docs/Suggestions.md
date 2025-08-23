# Architecture Suggestions (SmartHopper)

This document outlines an idealized refactoring to centralize concerns, reduce duplication, and improve reliability across SmartHopper. It complements existing docs under `docs/Providers/`, `docs/Components/`, `docs/Context/`, and `docs/Tools/`.

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

### Suggestion 1 — ConversationSession (central multi‑turn engine)

- New service in `src/SmartHopper.Infrastructure/AICall/ConversationSession/`.
- API: `Start(AIBody|AIBodyBuilder)`, `RunToStableResult(options)`, `Stream(observer)`, `ProcessPendingTools(policy)`.
- Handles assistant → tool_call → tool_result → assistant loops with proper linking and retries.
- Components call `ConversationSession.RunToStableResult()` instead of hand‑written loops.
- Stream‑first friendly: integrates `IStreamingAdapter` to emit partials and support backpressure; components can opt‑in to incremental UI updates.
- Cache‑aware hooks: exposes request/response fingerprints so Policies can leverage provider prompt caching (e.g., cache keys/ETags) and local memoization.
- Impacted areas: `src/SmartHopper.Infrastructure/AICall/ConversationSession/`, `src/SmartHopper.Core/ComponentBase/`, Providers under `src/SmartHopper.Providers.*`
- Phased migration plan:
  1. Introduce `ConversationSession` + basic Policies behind `AIRequestCall` (no component changes yet).
  2. Add streaming adapter; expose via `ConversationSession`.
- Remember to test... Contract tests for `ConversationSession` transcripts with fake providers/tools.
- Remember to update documentation in... `docs/Providers/ConversationSession.md`, `docs/Providers/AICall/Policies.md`, `docs/Providers/AICall/index.md`

### Suggestion 3 — Request/Response policy pipeline

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

### Suggestion 4 — JsonSchemaService

- Centralize schema wrapping/unwrapping and validation for all providers.
- Helpers for common shapes: `List<T>`, `ImageResult`, `GhJsonSpec`.
- Removes provider divergence for non‑object schemas.
- GHJSON pipeline helpers: generation/validation utilities for GH definitions and adapters to support saving Grasshopper files in GHJSON format.
- Impacted areas: `src/SmartHopper.Infrastructure/AICall/JsonSchemaService/`, Providers under `src/SmartHopper.Providers.*`
- Phased migration plan:
  1. Add `JsonSchemaService`; remove provider-specific schema quirks gradually.
- Remember to test... Policy pipeline unit tests (schema attach/validate).
- Remember to update documentation in... `docs/Providers/JsonSchemaService.md`, `docs/Providers/AICall/index.md`

### Suggestion 5 — Tool Runtime decoupling + ToolResultNormalizer

- Move generic tool contracts/execution to `src/SmartHopper.Infrastructure/AITools/`.
- Keep GH adapters in `src/SmartHopper.Core.Grasshopper/AITools/`.
- `ToolResultNormalizer.Normalize(toolName, JObject)` guarantees consistent top‑level keys and error envelope.
- Define AI tool(s) for GH authoring: e.g., `gh_generate_definition` producing a `GhJsonSpec`, plus adapters/utilities to save/export GH in GHJSON safely.
- Impacted areas: `src/SmartHopper.Infrastructure/AITools/`, `src/SmartHopper.Core.Grasshopper/AITools/`
- Phased migration plan:
  1. Extract Tool Runtime core; add `ToolResultNormalizer`; keep GH adapters.
- Remember to test... ToolResultNormalizer shape tests.
- Remember to update documentation in... `docs/Tools/Runtime.md`, `docs/Components/index.md`

### Suggestion 6 — AIBody immutability via builder

- Replace mutable `AIBody` with immutable value + `AIBodyBuilder`.
- Context injection handled by policies, not `Interactions` getter side‑effects.
- Impacted areas: `src/SmartHopper.Infrastructure/AICall/`, `src/SmartHopper.Core/ComponentBase/`
- Phased migration plan:
  1. Switch to `AIBodyBuilder` and immutable `AIBody`; provide shims for old code.
- Remember to test... Policy pipeline unit tests (schema attach/validate).
- Remember to update documentation in... `docs/Providers/AICall/index.md`, `docs/Providers/AICall/body-metrics-status.md`

### Suggestion 7 — Metrics & progress services

- `AIMetricsService` and `ProgressService` aggregate per‑call and session metrics.
- Debounce/progress UX unified; components subscribe for updates.
- Streaming/caching metrics: time‑to‑first‑token, partial throughput, cache hit/miss rates, and provider cache usage diagnostics.
- Impacted areas: `src/SmartHopper.Core/ComponentBase/`
- Phased migration plan:
  1. Centralize metrics/progress; eliminate bespoke component logic.
- Remember to test... Policy pipeline unit tests.
- Remember to update documentation in... `docs/Providers/AICall/body-metrics-status.md`, `docs/Components/index.md`

### Suggestion 8 — Validation pipeline with typed validators

- `IValidator<T>` chains for request, response, and tools.
- GH‑specific validators (e.g., GhJSON) live in `SmartHopper.Core.Grasshopper` and plug into the pipeline.
- Impacted areas: `src/SmartHopper.Core.Grasshopper/AITools/`, `src/SmartHopper.Infrastructure/AICall/Policies/`
- Phased migration plan:
  1. Introduce `IValidator<T>` chains; move GH validations into validators.
- Remember to test... Policy pipeline unit tests (validate).
- Remember to update documentation in... `docs/Providers/AICall/Policies.md`, `docs/Components/index.md`

### Suggestion 9 — Streaming adapter

- `IStreamingAdapter` unifies stream events (partials, tool proposals, final) across providers.
- Exposed via `ConversationSession.Stream()`.
- Support backpressure and cancellation propagation; optional buffering policies for UI friendliness.
- Impacted areas: `src/SmartHopper.Infrastructure/AICall/ConversationSession/`, Providers under `src/SmartHopper.Providers.*`
- Phased migration plan:
  1. Add streaming adapter; expose via `ConversationSession`.
- Remember to test... Contract tests for `ConversationSession` transcripts with fake providers/tools.
- Remember to update documentation in... `docs/Providers/ConversationSession.md`

### Suggestion 10 — Security & data egress controls

- Redaction policies for requests/responses; tool egress allowlists.
- Schema whitelisting/catalog for `JsonOutputSchema`.
- Per‑tool quotas, timeouts, and cancellation.
- Impacted areas: `src/SmartHopper.Infrastructure/AICall/Policies/`, Providers under `src/SmartHopper.Providers.*`
- Phased migration plan:
  1. Enable default security policies (least privilege), opt‑in broader access.
- Remember to test... Policy pipeline unit tests (redaction).
- Remember to update documentation in... `docs/Providers/AICall/Policies.md`, `docs/Architecture.md`

## Future thoughts (suggestions should consider future conditions and try to be flexible to accept them in the future)

- [ ] AI Provider streaming of responses
- [ ] AI Provider-side prompt caching
- [ ] AI Call cancellation token 
- [ ] AI Call local caching of responses to avoid recomputation
- [ ] AI Call compatibility with parallel tool calling
- [ ] AI tool to generate Grasshopper definitions in GHJSON format
- [ ] New way to save Grasshopper files in GHJSON format
- [ ] Improve WebChat UI with full html environment, including dynamic loading messages, supporting streaming, different interaction type (image, audio, text, toolcall, toolresult...)
