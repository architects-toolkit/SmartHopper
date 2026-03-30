# AIStatefulAsyncComponentBase

Combines provider selection with the stateful async execution model for AI‑powered components.

## Purpose

Offer a turnkey base to build AI components: choose provider/model, build a request, execute, and surface metrics/messages while following the component state machine.

## Key features

- Builds on [AIProviderComponentBase](./AIProviderComponentBase.md) and [StatefulComponentBase](./StatefulComponentBase.md) to add a `Settings` input and a `Metrics` output.
- Capability‑aware model selection via `RequiredCapability` and `UsingAiTools`, delegating to provider `SelectModel()` / `ModelManager.SelectBestModel`.
- `CallAiToolAsync` helper that injects provider/model into AI Tools, executes them, and stores the last `AIReturn` snapshot.
- Centralized output finalization via `FinishResults<T>` — persists primary + additional outputs atomically and emits metrics.
- Virtual hooks `PrepareInputs` and `TransformOutputs` enabling pre/post processing without touching the core pipeline.
- Surfaces structured provider/tool diagnostics from `AIReturn.Messages` as persistent Grasshopper runtime messages.
- Integrates with `ComponentBadgesAttributes` by maintaining a cached badge state (Verified/Deprecated/Invalid/Replaced/Not‑recommended models).

---

## Unified Execution Flow

Both the non-batch and batch paths converge at `FinishResults<T>`. The two virtual hooks fire at the same logical positions in both paths.

### Non-batch path

```text
DoWorkAsync()
  └── RunProcessingAsync()
        └── DataTreeProcessor.RunAsync()
              └── function(inputs)          [once per branch/item]
                    1. PrepareInputs(inputs, context)    ← virtual hook
                    2. CallAiToolAsync(...)
                       → real result returned immediately
                    3. (return outputs dict)
        └── FinishResults(primary, ...extras)
              → SetPersistentOutput for each output
              → stamp CompletionTime from _batchCompletionTime (if any)
              → SetMetricsOutput(null)
  └── Worker.SetOutput() → no-op
        |
[Completed State] → RestorePersistentOutputs() → canvas updated
```

### Batch path

```text
DoWorkAsync()
  └── RunProcessingAsync()
        └── DataTreeProcessor.RunAsync()
              └── function(inputs)          [once per branch/item]
                    1. PrepareInputs(inputs, context)    ← virtual hook
                    2. CallAiToolAsync(...)
                       → returns ##SH_BATCH:{customId}## sentinel
        └── TrySubmitBatchAsync()           → submits queue to provider
  └── Worker.SetOutput() → no-op
        |
[Stay in Processing State]
        |
PollBatchStatusAsync() [background timer]
        |
OnBatchCompleted(results, messages)
  └── ProcessBatchResults<T>(decode, messages)
        └── for each sentinel in tree:
              item = decode(customId, resultBody)
              TransformOutputs({primary→item}, context)  ← virtual hook
              extras accumulated per sentinel
        └── SetAIReturnSnapshot(aggregatedMetrics)
        └── FinishResults(primary, ...extras)
              → SetPersistentOutput for each output
              → stamp CompletionTime from _batchCompletionTime
              → SetMetricsOutput(null)
        |
Transition to [Completed State]
ExpireSolution() → RestorePersistentOutputs() → canvas updated
```

---

## Virtual hooks

### `PrepareInputs(Dictionary<string, object> inputs, ProcessingUnitContext context)`

Called **before** `CallAiToolAsync` inside `DoWorkAsync`, after inputs are read from DA. Fires at the same point in both paths — inside `function(inputs)` during `DoWorkAsync`.

Override to:

- Inject computed fields (e.g. derived prompt from multiple inputs)
- Normalize or sanitize input values
- Add context metadata (file path → format hint, image → MIME type)
- Validate and throw early if inputs are invalid

> **Note:** Inputs are not available to `TransformOutputs` in batch mode — cache them component-side during `DoWorkAsync` if needed during output transformation.

### `TransformOutputs(Dictionary<string, IGH_Goo> decodedOutputs, ProcessingUnitContext context)`

Called **after** decode but **before** `FinishResults` / `SetPersistentOutput`. Fires inside `ProcessBatchResults` per sentinel (batch), or immediately after the AI call returns a real result (non-batch).

Override to:

- Split one AI response into multiple named outputs (A, B, C)
- Add computed fields alongside AI output (e.g. word count, confidence)
- Normalize or sanitize decoded values

Extra keys returned (any key other than the primary output param name) are accumulated and passed to `FinishResults` as `additionalOutputs`.

### `ProcessingUnitContext`

Lightweight read-only struct passed to both hooks:

| Property | Type | Available in non-batch | Available in batch |
| --- | --- | --- | --- |
| `Path` | `GH_Path` | ✓ | — |
| `ItemIndex` | `int?` | ✓ | — |
| `SentinelId` | `string` | — | ✓ |

---

## `FinishResults<T>`

```csharp
protected void FinishResults<T>(
    string primaryOutputParamName,
    GH_Structure<T> primaryTree,
    params (string name, object value)[] additionalOutputs)
    where T : IGH_Goo
```

Single finalization point called by **both** paths. Responsibilities:

1. `SetPersistentOutput` for the primary tree
2. `SetPersistentOutput` for each additional output (any GH-compatible type)
3. Stamp `AIReturnSnapshot.Metrics.CompletionTime` from `_batchCompletionTime` (fixes batch completion-time bug)
4. `SetMetricsOutput(null)` — emits the Metrics output

`additionalOutputs` value routing:

- `IGH_Structure` → `DA.SetDataTree`
- `IEnumerable` (non-string) → `DA.SetDataList`
- Anything else → `GH_Convert.ToGoo` → `DA.SetData`

---

## Metrics emission

| Component type | Where metrics are emitted |
| --- | --- |
| Standard AI components (Group A) | `FinishResults` (non-batch) or `ProcessBatchResults` → `FinishResults` (batch) |
| Synchronous-output components (e.g. `AIChatComponent`) | `SetMetricsOutput(DA)` called directly inside `SolveInstance` with the live DA reference |

`OnSolveInstancePostSolve` does **not** call `SetMetricsOutput`. Components that set outputs/metrics synchronously in their own `SolveInstance` (like `AIChatComponent`) are unaffected by the hook pattern and continue to work exactly as before.

---

## Implementation requirements (standard AI component)

```csharp
// Worker.DoWorkAsync — non-batch branch must call FinishResults:
var batchSubmitted = await parent.TrySubmitBatchAsync("Result", result, token);
if (!batchSubmitted)
    parent.FinishResults("Result", result["Result"]);

// Worker.SetOutput — must be a no-op:
public override void SetOutput(IGH_DataAccess DA, out string message)
    => message = string.Empty;

// OnBatchCompleted — delegate entirely to ProcessBatchResults:
protected override void OnBatchCompleted(
    IReadOnlyDictionary<string, JObject> results,
    IReadOnlyList<AIRuntimeMessage> messages = null)
{
    var sentinel = this.GetSentinelTree("Result");
    if (results == null || sentinel == null) return;
    this.ProcessBatchResults<GH_String>(
        "Result",
        sentinel,
        results,
        (customId, body) => new GH_String(Decode(body)),
        messages);
}
```

For components with **multiple outputs** (e.g. `AIFile2MdComponent`), pass extras to `FinishResults`:

```csharp
parent.FinishResults(
    "Markdown",
    markdownTree,
    ("Images", imagesTree),
    ("Format", formatTree));
```

---

## Usage

- Derive when your component sends prompts/requests to an AI provider (typically by calling AI Tools or an `AIRequestCall`).
- Call `CallAiToolAsync` (recommended) so provider/model are injected automatically and the `AIReturn` snapshot is stored for metrics and badges.
- Override `RequiredCapability` (and optionally `UsingAiTools`) so model selection, validation, and badges use the correct capability flags.
- Follow the **implementation requirements** above: `SetOutput` must be a no-op; non-batch branch must call `FinishResults`; `OnBatchCompleted` must call `ProcessBatchResults`.

---

## Related

- [AIProviderComponentBase](./AIProviderComponentBase.md) – provider UI/persistence.
- [StatefulComponentBase](./StatefulComponentBase.md) – stateful execution foundation.
