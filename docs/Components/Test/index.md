# DataTreeProcessorEqualPathsTestComponent

## Purpose

- Manually validate `DataTreeProcessor.RunFunctionAsync` using two hardcoded integer trees with equal paths.

## Component

- Name: Test DataTreeProcessor (Equal Paths)
- Nickname: TEST-DTP-EQ
- Assembly: SmartHopper.Components.Test
- Category: SmartHopper
- Subcategory: Testing Data
- File: `src/SmartHopper.Components.Test/DataProcessor/DataTreeProcessorEqualPathsTestComponent.cs`

## Usage (Grasshopper)

1. Drop the component on the canvas (SmartHopper > Testing Data).
2. Toggle the built-in `Run?` input to `true`.
3. Observe outputs:
   - Result (integer tree): path {0} containing a single value 7.
   - Success (bool): true if the result matches expected value.
   - Messages (string list): diagnostic info about inputs and result.

## Test Setup (internal)

- Inputs: A = 2 at path {0}, B = 5 at path {0}
- Function: returns `Sum = A + B` per branch
- Expected: Sum = 7 at path {0}

## Notes

- Uses internal data; no external inputs.
- `RunOnlyOnInputChanges = false` so it runs when `Run?` is enabled even without input changes.
- Helpful for quick, manual verification of data-tree branch mapping and processing.

---

## Provider Cancellation Test Components

### Purpose (Cancellation Tests)

- Validate that `CancellationToken` is correctly honored across all async provider operations.
- Covers both human-interactive scenarios (batch polling) and operations too fast for a human to cancel (standard API calls, vision calls, batch submit).

### Components

Six provider-specific components, one per supported AI provider:

- `TestOpenAICancellationComponent` — OpenAI
- `TestAnthropicCancellationComponent` — Anthropic
- `TestMistralAICancellationComponent` — MistralAI
- `TestDeepSeekCancellationComponent` — DeepSeek
- `TestGeminiCancellationComponent` — Gemini
- `TestOpenRouterCancellationComponent` — OpenRouter

All are located under `SmartHopper > Test/Providers` and share the same output structure.

### Outputs

- **Standard Call Cancelled** (bool): true if `provider.Call()` threw `OperationCanceledException`.
- **Batch Submit Cancelled** (bool): true if `batchProvider.SubmitBatchAsync()` threw `OperationCanceledException` (local HTTP abort only).
- **Batch Remote Cancelled** (bool): true if a submitted batch was successfully cancelled via `CancelBatchAsync` and the provider's polled status became `Cancelled`.
- **Vision Call Cancelled** (bool): true if a vision `provider.Call()` threw `OperationCanceledException` (local only).
- **Messages** (string list): per-test diagnostics and error details.

### Test approach

- For **standard call**, **batch submit**, and **vision call**: a linked `CancellationTokenSource` is cancelled after 100 ms. If the network request is still in flight, `OperationCanceledException` is thrown and the boolean is `true`. If the provider responds faster than 100 ms, the call completes and the boolean is `false` — this is a valid outcome, not a failure. Remote verification is not possible for regular chat-completion calls; the cancellation is purely client-side.
- For **batch remote cancellation**: a batch is submitted successfully, then `CancelBatchAsync` is called. The test polls for up to the configured **HTTP timeout** (set in SmartHopper Settings > Network > Timeout) and reports success only when the provider's status becomes `Cancelled`.

### Notes (Cancellation Tests)

- All tests use `CancellationTokenSource.CreateLinkedTokenSource(token)` so both the programmed timeout and a human toggling **Run?** off propagate cancellation.
- `RunOnlyOnInputChanges = false` so the component re-runs every time **Run?** is toggled.
