# Body, Metrics, Status, Return

Covers `AIBody`, `AIMetrics`, `AICallStatus`, and `AIReturn`.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/AICall/Core/BodyMetricsStatus.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This page documents the core data types that carry conversation state, performance metrics, execution status, and provider results through the AI call pipeline. Understanding these types is essential for both consuming AI responses and building custom orchestration logic.

**You should read this if you:**

- Need to construct or inspect conversation bodies (`AIBody`) and their interactions
- Want to track provider performance via `AIMetrics` and the metrics domain
- Need to interpret `AIReturn` results and `AICallStatus` states
- Are building custom request/response policies or tools

---

## End-User Guide

### AIBody

- File: `AIBody.cs`
- **Immutable record** — once constructed, cannot be modified directly
- Holds interactions and optional controls:
  - `ToolFilter` (default `-*` = disable all)
  - `ContextFilter` (default `-*` = disable)
  - `JsonOutputSchema` (enables JSON output inference)
  - `InteractionsNew` — list of indices marking newly-added interactions for UI rendering
- Metrics aggregation: `Metrics` sums per-interaction metrics
- Validation: `IsValid()` requires at least one interaction
- Tool utilities: `PendingToolCallsCount()`, `PendingToolCallsList()` match `AIInteractionToolCall.Id` to `AIInteractionToolResult.Id`

### AIMetrics

- File: `AIMetrics.cs`
- Fields: `Provider`, `Model`, token counters (input/output), `CompletionTime`, `FinishReason`
- Validation: non-empty `Provider`, `Model`, `FinishReason`; non-negative counters and time
- `Combine(other)` merges providers/models/reason and adds counters/time

### AICallStatus

- File: `AICallStatus.cs`
- States: `Idle`, `Processing`, `Streaming`, `CallingTools`, `Finished`
- Helpers: `.ToString()`, `.ToDescription()`, `FromString(string)`

### AIReturn and IAIReturn

- Files: `AIReturn.cs`, `IAIReturn.cs`
- `AIReturn` carries `Body`, `Request`, aggregated `Metrics`, `Status`, `Messages`, and computed `Success`
- `Success` is computed: returns `true` when no error messages exist in `Messages` collection
- `Messages` aggregates structured runtime messages from:
  - Private messages added via `AddRuntimeMessage()` or `Create*Error()` methods
  - `Request.Messages` (validation/capability notes)
  - `Body.Messages` (interaction and body validation messages)
  - Deduplicates and sorts by severity (Error > Warning > Info)
- Validity: checks `Request.IsValid()`, `Metrics.IsValid()`, and that either `Body` or `Messages` is present

---

## Developer Reference

### AIBodyBuilder (Mutation Path)

- File: `AIBodyBuilder.cs`
- **Fluent builder** for constructing and mutating `AIBody` instances
- All mutations go through the builder; the immutable `AIBody` record is never modified in-place
- **The only intended construction path for immutable AIBody** — all interaction additions and filter configurations must go through this builder

#### Core Factory Methods

- `Create()` — static factory for a new empty builder
- `FromImmutable(AIBody body)` — copy constructor from an existing body; preserves interactions, filters, and 'new' markers

#### Interaction Management

- `Add(IAIInteraction interaction)` — append an interaction using the builder's default newness flag
- `Add(IAIInteraction interaction, bool markAsNew)` — append an interaction and explicitly mark it as new or historical
- `AddInteraction(IAIInteraction interaction, ...)` — alias for `Add()`
- `AddToolResult(JObject result, string id, string name, AIMetrics metrics, List<SHRuntimeMessage> messages)` — append a tool result interaction with structured metadata
- `ReplaceInteraction(int index, IAIInteraction interaction)` — replace an interaction at a specific index
- `RemoveInteraction(int index)` — remove an interaction at a specific index
- `RemoveLastInteraction()` — remove the most recently added interaction
- `Clear()` — remove all interactions while preserving filters and settings

#### Filter and Schema Configuration

- `WithToolFilter(string filter)` — set the tool filter expression (e.g., `-*` = disable all, `+gh_*` = enable Grasshopper tools)
- `WithContextFilter(string filter)` — set the context filter expression for provider injection
- `WithJsonOutputSchema(string jsonSchema)` — set the JSON Schema to instruct providers to produce structured output

#### Turn and Newness Management

- `WithTurnId(string turnId)` — set the default TurnId applied to interactions lacking one; interactions in a logical turn share the same TurnId
- `WithDefaultNewness(bool markAsNew)` — set the default newness flag for subsequent Add/Replace operations
- `AsHistory()` — convenience method; subsequent Add/Replace operations will be marked as historical (not new)
- `AsNew()` — convenience method; subsequent Add/Replace operations will be marked as new

#### Finalization

- `Build()` — produce the immutable `AIBody` record with all accumulated interactions and settings
- `BuildAndClear()` — produce the immutable `AIBody` and reset the builder to empty state

#### Query Methods

- `InteractionsCount` — get the current number of interactions in the builder
- `GetInteraction(int index)` — retrieve an interaction by index
- `GetLastInteraction()` — retrieve the most recently added interaction
- `GetLastInteraction(AIAgent agent)` — retrieve the most recently added interaction matching a specific agent role

#### Workflow

```csharp
// Create builder
var builder = AIBodyBuilder.Create();

// Add interactions
builder.Add(interaction1).Add(interaction2);

// Configure filters
builder.WithToolFilter(...).WithContextFilter(...);

// Set schema if needed
builder.WithJsonOutputSchema(...);

// Finalize
var immutableBody = builder.Build();

```

All builder mutations return the builder itself for chaining; the final `Build()` call produces the immutable `AIBody`.

### AIReturn Builders

```csharp
// Create a successful return with a body
var result = AIReturn.CreateSuccess(body, request);

// Create an error return
var errorResult = AIReturn.CreateError("Something went wrong", request);

// Create a provider-specific error
var providerError = AIReturn.CreateProviderError("Provider timeout", request);

```

- Builders:
  - `CreateSuccess(AIBody body, IAIRequest? request = null)`
  - `CreateSuccess(List<IAIInteraction> interactions, IAIRequest? request = null, AIMetrics? metrics = null)`
  - `CreateSuccess(JObject raw, IAIRequest? request = null)` stores raw provider JSON; decoding to interactions is handled by response policies
  - `CreateError(string message, IAIRequest? request = null)` adds structured error message with Return origin
  - `CreateProviderError(string rawMessage, IAIRequest? request = null)` adds structured error with Provider origin
  - `CreateNetworkError(string rawMessage, IAIRequest? request = null)` adds structured error with Network origin
  - `CreateToolError(string rawMessage, IAIRequest? request = null)` adds structured error with Tool origin
- `SetBody(...)` overloads update `Body` directly
- `AddRuntimeMessage(severity, origin, text)` adds structured messages without affecting Success flag directly

Policy notes:

- Always-on `PolicyPipeline` applies request policies before provider execution and response policies after execution. Response policies decode/normalize raw JSON into interactions and add diagnostics via `AIReturn.AddRuntimeMessage(...)`.

### AIMetricsDomain

Comprehensive metrics domain for tracking AI call performance, streaming, tool execution, and validation across session/turn/request/tool boundaries.

- File: `src/SmartHopper.Infrastructure/AICall/Metrics/AIMetricsDomain.cs`
- **Purpose**: Provides structured types for metrics collection, filtering, and correlation across distributed AI operations

#### Core Types

##### MetricsCorrelation

Record struct for associating metrics across boundaries:

- `SessionId` (string, nullable) — session identifier for grouping related calls
- `TurnIndex` (int, nullable) — logical turn number within a session
- `RequestId` (string, nullable) — unique request identifier
- `ProviderRequestId` (string, nullable) — provider-specific request ID (for tracing)
- `BodyFingerprint` (string, nullable) — fingerprint of the request body for caching
- `ToolInvocationId` (string, nullable) — identifier for tool invocation tracking

##### AIMetricsEventType Enum

Types of metrics events emitted by orchestrators/providers/tools:

- `StartCall` — AI call initiated
- `StreamDelta` — streaming delta received
- `EndCall` — AI call completed
- `ToolStart` — tool execution started
- `ToolEnd` — tool execution completed
- `CacheEval` — cache evaluation performed
- `ValidationRun` — validation executed
- `Cancelled` — operation cancelled
- `Failed` — operation failed
- `Completed` — operation completed successfully

##### MetricsFilter

Record for subscribing to specific subsets of metrics:

- `SessionId` (string, nullable) — filter by session
- `RequestId` (string, nullable) — filter by request
- `ToolInvocationId` (string, nullable) — filter by tool invocation
- `EventType` (AIMetricsEventType, nullable) — filter by event type

##### AIMetricsEvent

Envelope for all metrics events:

- `Timestamp` (DateTimeOffset) — when the event occurred
- `EventType` (AIMetricsEventType) — type of event
- `Correlation` (MetricsCorrelation) — correlation keys for tracing
- `Payload` (object) — event-specific data (one of the metric records below)

#### Metric Records

##### CallMetrics

Metrics for a single provider call (per-turn):

- `StartTime` (DateTimeOffset, nullable) — when the call started
- `EndTime` (DateTimeOffset, nullable) — when the call ended
- `Duration` (TimeSpan, nullable) — total call duration
- `HttpStatusCode` (int, nullable) — HTTP status code from provider
- `Retries` (int, default: 0) — number of retries
- `BytesSent` (long, nullable) — bytes sent to provider
- `BytesReceived` (long, nullable) — bytes received from provider
- `PromptTokens` (int, nullable) — input tokens consumed
- `CompletionTokens` (int, nullable) — output tokens generated
- `Model` (string, nullable) — model used
- `Provider` (string, nullable) — provider name
- `FinishReason` (string, nullable) — reason for completion (stop, length, tool_calls, etc.)

##### TurnMetrics

Metrics aggregated at the logical "turn" boundary:

- `EncodingTime` (TimeSpan, nullable) — time to encode request
- `SchemaWrapTime` (TimeSpan, nullable) — time to wrap with JSON schema
- `ProviderCallTime` (TimeSpan, nullable) — time for provider call
- `ValidationTime` (TimeSpan, nullable) — time for validation

##### StreamMetrics

Streaming-related metrics for live responses:

- `TimeToFirstToken` (TimeSpan, nullable) — latency to first token
- `DeltaCount` (int, default: 0) — number of streaming deltas received
- `TokensPerSecond` (double, nullable) — throughput in tokens/sec
- `BytesPerSecond` (double, nullable) — throughput in bytes/sec
- `LastDeltaSize` (int, nullable) — size of last delta received

##### ToolMetrics

Metrics for a single tool invocation:

- `ToolName` (string, nullable) — name of the tool
- `QueueTime` (TimeSpan, nullable) — time waiting in queue
- `DispatchTime` (TimeSpan, nullable) — time to dispatch
- `ExecutionTime` (TimeSpan, nullable) — actual execution time
- `ResultBytes` (long, nullable) — size of tool result
- `IsError` (bool, nullable) — whether tool execution failed

##### CacheMetrics

Cache evaluation metrics (local and provider-side prompt caching hints):

- `LocalHit` (bool, nullable) — whether local cache was hit
- `ProviderCacheUsed` (bool, nullable) — whether provider-side cache was used
- `ETag` (string, nullable) — cache entity tag
- `Strategy` (string, nullable) — caching strategy used

##### ValidationMetrics

Output of validators (counts only; do not include PII content):

- `Errors` (int, default: 0) — count of validation errors
- `Warnings` (int, default: 0) — count of validation warnings
- `Infos` (int, default: 0) — count of informational messages

#### Usage

Metrics are emitted as `AIMetricsEvent` records through a metrics pipeline. Consumers can:

1. **Subscribe to all events**: Listen to all `AIMetricsEvent` without filtering
2. **Filter by correlation**: Use `MetricsFilter` to subscribe to specific sessions, requests, or tools
3. **Process payloads**: Extract metric records from `AIMetricsEvent.Payload` based on `EventType`
4. **Aggregate**: Combine metrics across multiple events for reporting and analysis

Example:

```csharp
var filter = new MetricsFilter(SessionId: "session-123", EventType: AIMetricsEventType.EndCall);
// Subscribe to filter and process CallMetrics payloads

```

---

## Architecture & Design

### Safety and Performance

- Tools: filter with `AIBody.ToolFilter` to avoid unintended tool loading; validate tool args before execution
- JSON schema: ensure `JsonOutputSchema` is trusted/user-controlled (avoid TOCTOU injections)
- Context: avoid leaking sensitive data when enabling `ContextFilter`
