# Architecture Suggestions (SmartHopper)

This document outlines an idealized refactoring to centralize concerns, reduce duplication, and improve reliability across SmartHopper. It complements existing docs under `docs/Providers/`, `docs/Components/`, `docs/Context/`, `docs/Tools/`...

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `N/A` |
| **Since Version** | ? |
| **Last Updated** | 2026-11-12 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This is a forward-looking plan for SmartHopper architecture. It identifies current pain points and proposes concrete, independently applicable suggestions to improve modularity, security, and reliability.

**You should read this if you:**

- Want to understand the roadmap for SmartHopper architectural improvements
- Are contributing to the core infrastructure or designing new subsystems
- Need to evaluate how proposed changes might affect your provider or component

---

## End-User Guide

Breaking changes are acceptable; this is a forward-looking plan.

### Guiding Principles

- Single responsibility per module; move cross-cutting logic to reusable services.
- Provider-agnostic orchestration in Infrastructure; provider specifics isolated in `SmartHopper.Providers.*`.
- Immutable core data structures with builders for composition.
- Explicit, composable policies for validation, security, and retries.

### Current Strengths (to Keep)

- Template-method provider pipeline (`PreCallAsync → FormatRequestBody → CallApi → PostCall`) in `AIProvider`.
- Central capability registry and model resolution (`AIModelCapabilityRegistry`).
- Context injection at request level (`AIBody`), component base classes, and secure provider loading.

### Pain Points to Address

- Parallel tool dispatch is declared (`SessionOptions.AllowParallelTools`) but execution remains sequential.
- No `SchemaWhitelistPolicy` catalog for `JsonOutputSchema` yet; egress control lives at the network/consent layer, not inside the tool executor.
- GHJSON codecs cover scalar/media types only; native geometry types (Point3d, Color, ...) are not yet encoded.

---

## Architecture & Design

### Suggestion 5 — Tool Runtime Decoupling + ToolResultNormalizer (S5)

> **Status (2026-11):** Mostly implemented. Shipped: generic tool contracts/registry in `SmartHopper.Infrastructure/AITools/`, GH adapters in `SmartHopper.Core.Grasshopper/AITools/`, normalized results via `ToolResultEnvelope`/`ToolCallResult`, pre-execution validators (`AICall/Validation/` + `AIToolValidationRequestPolicy`), session-managed dispatch (`ConversationSession.ProcessPendingToolsAsync`), per-call timeouts, `gh_generate` for GhJSON authoring, and tool metrics via `AIMetrics`/`TelemetryTraceScope`. Remaining: parallel tool dispatch (`SessionOptions.AllowParallelTools` is declared but execution is sequential) and egress-allowlist checks inside the tool runtime (currently covered by `ConsentGate`/`ExternalSourceAllowlist`).

- Move generic tool contracts/execution to `src/SmartHopper.Infrastructure/AITools/`.
- Keep GH adapters in `src/SmartHopper.Core.Grasshopper/AITools/`.
- `ToolResultNormalizer.Normalize(toolName, JObject)` guarantees consistent top-level keys and error envelope.
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

#### Execution Model (S5)

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

#### Parallel Tools (S5)

- Support optional parallel dispatch where the provider proposes multiple tool calls.
- Deterministic aggregation order (by tool index) for transcript consistency.

#### Integration Points (S5)

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

#### Migration Plan (S5)

- Phase 1: Introduce `ITool`/`IToolExecutor` contracts and normalizer; wrap existing GH tools.
- Phase 2: Replace direct component tool loops with session-managed dispatch.
- Phase 3: Enable optional parallel tool execution and quotas.

#### Acceptance Criteria (S5)

- Tools execute via a common runtime; normalized results; cancellations/timeouts enforced; metrics emitted.

#### Impacted Areas (S5)

- `src/SmartHopper.Infrastructure/AITools/`, `src/SmartHopper.Core.Grasshopper/AITools/`, `src/SmartHopper.Infrastructure/AICall/ConversationSession/`

#### Alignment with "Future Thoughts" (S5)

- Parallel tool calling: first-class support with deterministic aggregation.
- WebChat UI improvements: tool badges and progress can be rendered from normalized events.
- Local caching: tool results can be memoized when inputs are pure and within size limits.

---

### Suggestion 10 — Security & Data Egress Controls (S10)

> **Status (2026-11):** Mostly implemented. Shipped: request/response policy pipeline (`AICall/Policies/`), `SmartHopperLogScrubber` PII/credential redaction applied to all log sinks and diagnostics, `ExternalSourceAllowlist`/`ExternalSourceAccess`/`ExternalDataHttpClient` network egress consent with a Security settings page, `ConsentGate` + `AIToolSurface` mutation consent, `RequestTimeoutPolicy`, autonomous time/token budgets, and signature-verification default-on (`SMARTHOPPER_SKIP_SIGNATURE_VERIFY` test bypass only). Remaining: `SchemaWhitelistPolicy` catalog for `JsonOutputSchema` and a per-tool `EgressPolicy` consulted inside the executor (current egress control lives at the network layer and consent gate, not per tool).

- Redaction policies for requests/responses; tool egress allowlists.
- Schema whitelisting/catalog for `JsonOutputSchema`.
- Per-tool quotas, timeouts, and cancellation.
- Impacted areas: `src/SmartHopper.Infrastructure/AICall/Policies/`, Providers under `src/SmartHopper.Providers.*`
- Phased migration plan:
  1. Enable default security policies (least privilege), opt-in broader access.
- Remember to test... Policy pipeline unit tests (redaction).
- Remember to update documentation in... `docs/Providers/AICall/Policies.md`, `docs/Architecture.md`

#### Objectives (S10)

- Enforce least-privilege data handling and controlled egress for providers and tools.
- Centralize redaction, quotas, and schema whitelisting with auditable defaults.

#### Policy Types and Scope (S10)

- `RedactionPolicy`: strips PII from logs/metrics; configurable redaction rules; hashes where necessary.
- `EgressPolicy`: per-tool and per-provider allowlists for network/file access; deny by default.
- `QuotaPolicy`: per-tool timeouts, concurrency limits, and size caps.
- `SchemaWhitelistPolicy`: restricts `JsonOutputSchema` to approved catalogs to prevent injection/overreach.

#### Contracts (S10)

- Policies conform to the pipeline contract (S3) with `PolicyContext` containing identity and correlation fields.
- Security events emitted as `AIMetricsEvent` with `AIMetricsEventType.Security` or mapped to `Failed` with category.

#### Integration Points (S10)

- `AIProvider.Call` wrapped by `RetryPolicy` and `RedactionPolicy` to ensure no sensitive data.
- `IToolExecutor` consults `EgressPolicy` before performing file/network ops.
- `JsonSchemaService` consults `SchemaWhitelistPolicy` before attaching schemas.

#### Threat Model & References (S10)

- Follow OWASP recommendations; assess threats for prompt injection, exfiltration via tools, and schema abuse.
- Respect secure key storage via `SmartHopperSettings` OS secure store (see existing implementation) and provider trust model.

#### Required Refactors Beyond S10 (S10)

- Ensure ProviderManager signature verification remains default-on; keep test bypass via env vars only (see `SMARTHOPPER_SKIP_SIGNATURE_VERIFY`).
- Add sandbox helpers in Tool Runtime for safe temp file paths and path normalization.
- Harden HTTP timeouts/retries/cancellation in `AIProvider.CallAsync` as per planned resilience work.

#### Side-Effects and Mitigations (S10)

- Stricter defaults may break previously permissive flows.
  - Mitigation: staged rollout with logging-only mode; clear error messages with remediation hints.
- Performance cost for redaction and policy checks.
  - Mitigation: compile redaction regexes; avoid scanning large payloads; sample where acceptable.

#### Performance (S10)

- Zero-copy where possible; redact before serialization; avoid duplicate buffers.

#### Testing (S10)

- Unit: redaction coverage; egress allow/deny decisions; quota enforcement.
- Integration: end-to-end with tools performing file/network operations; ensure denial paths are safe and informative.

#### Migration Plan (S10)

1) Implement `RedactionPolicy` with logging-only mode; instrument metrics.
2) Introduce `EgressPolicy` with deny-by-default for network and filesystem; allowlist critical tools.
3) Add `QuotaPolicy` and `SchemaWhitelistPolicy`; document catalog management.
4) Enable strict mode by default after stabilization.

#### Acceptance Criteria (S10)

- No PII in logs/metrics; egress controlled and auditable; quotas enforced; schema usage constrained.

---

### Suggestion 14 — GHJSON Encode/Decode Service for Native GH Types (S14)

> **Status (2026-11):** Partially implemented. `GooCodecRegistry`/`IGooCodec` (`SmartHopper.Core/IO/Codecs/`) provide the suggested versioned, loss-aware codec registry — unknown payloads never throw and decode with warnings. Built-in codecs cover `String`, `Number`, `Integer`, `Boolean`, `VersatileImage`, `VersatileAudio` and `AIInputPayload`. Remaining: codecs for native geometry/types (`Point3d`, `Vector3d`, `Line`, `Plane`, `Circle`, `Color`, ...) and wiring the registry into `GHJsonLocal.Validate()`/GhJSON persistence paths.

- **Goal**: Persist and restore more Grasshopper native types (Color, Point3d, Vector3d, Line, Plane, Circle, ...) safely and loss-aware.
- **Direction**: Introduce `GhTypeCodec` registry with converters (to/from canonical JSON/string) using culture-invariant formatting and versioned schemas; integrate with persistent storage and validators.
- **First steps**: Prioritize a core set of types; implement codecs + round-trip tests; update `GHJsonLocal.Validate()` to consult the registry; document shapes.
- **Risks**: Precision/units, culture differences, performance on large graphs, backward compatibility.
- **Impacted areas**: `src/SmartHopper.Core.Grasshopper/Utils/GHJsonLocal.cs`, persistence/serialization helpers, GH tools that consume/emit GHJSON.

---

## Future Thoughts (Suggestions Should Consider Future Conditions and Try to Be Flexible to Accept Them in the Future)

- [ ] AI Call local caching of responses to avoid recomputation (only per-request/per-run memoization exists; no persistent response cache)
- [ ] AI Call compatibility with parallel tool calling (`SessionOptions.AllowParallelTools` declared but dispatch is sequential)

---

## Open Items Carried Over from Retired Reviews

Residual findings preserved from review documents that were superseded or removed on 2026-11-12. Each was verified unfixed or unverifiable at the time of transfer.

### From the macOS compatibility review (2026-02-15)

- Provider icons depend on GDI+: `IAIProvider.Icon` returns `System.Drawing.Image`, and `System.Drawing.Common` is unsupported off Windows (.NET 7+) unless Rhino's bundled runtime covers it. Consider returning PNG `byte[]` and letting each UI host convert.
- Provider trust prompt is sync-over-async (`Task.Result` on a `TaskCompletionSource`): if provider loading is ever invoked on the UI thread it can deadlock. Refactor to an async flow or guarantee it never runs on the UI thread.
- `ComponentStateManager` event firing under `stateLock`: the review flagged `ForceState`, `TransitionRejected`, and debounce events firing inside the lock (re-entrant deadlock risk on macOS). The state machine has since been refactored — re-verify that no event is raised while holding the lock.

### From the SmartHopper 2.0.0 community test report (2026-07-21)

- `Text to JSON` component reported missing (only `JSON Schema` was found).
- `AI to JSON` returns `null` without fallback when the response cannot be parsed.
- `file2md` returns raw binary for PNG inputs — no format detection or image handling.

### From the P1 regression retest (2026-11-04)

- N1 (provider-scoped TTS voice defaults), N2 (`any_provider` cross-provider fallback), and F14 (`gh_disconnect` noop status) are implemented in code but were marked "MCP retest pending" — a live end-to-end retest was never recorded.
