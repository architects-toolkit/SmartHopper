# Test Components

Internal test components for validating data tree processing and provider cancellation.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/Components/Test/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Test components are used internally to validate core SmartHopper behavior, including data-tree branch mapping and async provider cancellation. They are exposed as Grasshopper components so that developers and advanced users can manually verify system correctness.

**You should read this if:**

- You are developing or debugging SmartHopper core functionality
- You need to manually verify that `DataTreeProcessor` maps branches correctly
- You want to confirm that `CancellationToken` propagation works across all supported AI providers
- You are adding a new AI provider and need to validate cancellation behavior

---

## End-User Guide

### DataTreeProcessorEqualPathsTestComponent

#### Purpose

- Manually validate `DataTreeProcessor.RunFunctionAsync` using two hardcoded integer trees with equal paths.

#### Component

- Name: Test DataTreeProcessor (Equal Paths)
- Nickname: TEST-DTP-EQ
- Assembly: SmartHopper.Components.Test
- Category: SmartHopper
- Subcategory: Testing Data
- File: `src/SmartHopper.Components.Test/DataProcessor/DataTreeProcessorEqualPathsTestComponent.cs`

#### Usage (Grasshopper)

1. Drop the component on the canvas (SmartHopper > Testing Data).
2. Toggle the built-in `Run?` input to `true`.
3. Observe outputs:
   - Result (integer tree): path {0} containing a single value 7.
   - Success (bool): true if the result matches expected value.
   - Messages (string list): diagnostic info about inputs and result.

#### Test Setup (internal)

- Inputs: A = 2 at path {0}, B = 5 at path {0}
- Function: returns `Sum = A + B` per branch
- Expected: Sum = 7 at path {0}

#### Notes

- Uses internal data; no external inputs.
- `RunOnlyOnInputChanges = false` so it runs when `Run?` is enabled even without input changes.
- Helpful for quick, manual verification of data-tree branch mapping and processing.

---

### Provider Cancellation Test Components

#### Purpose (Cancellation Tests)

- Validate that `CancellationToken` is correctly honored across all async provider operations.
- Covers both human-interactive scenarios (batch polling) and operations too fast for a human to cancel (standard API calls, vision calls, batch submit).

#### Components

Six provider-specific components, one per supported AI provider:

- `TestOpenAICancellationComponent` — OpenAI
- `TestAnthropicCancellationComponent` — Anthropic
- `TestMistralAICancellationComponent` — MistralAI
- `TestDeepSeekCancellationComponent` — DeepSeek
- `TestGeminiCancellationComponent` — Gemini
- `TestOpenRouterCancellationComponent` — OpenRouter

All are located under `SmartHopper > Test/Providers` and share the same output structure.

#### Outputs

- **Standard Call Cancelled** (bool): true if `provider.Call()` threw `OperationCanceledException`.
- **Batch Submit Cancelled** (bool): true if `batchProvider.SubmitBatchAsync()` threw `OperationCanceledException` (local HTTP abort only).
- **Batch Remote Cancelled** (bool): true if a submitted batch was successfully cancelled via `CancelBatchAsync` and the provider's polled status became `Cancelled`.
- **Vision Call Cancelled** (bool): true if a vision `provider.Call()` threw `OperationCanceledException` (local only).
- **Messages** (string list): per-test diagnostics and error details.

#### Test approach

- For **standard call**, **batch submit**, and **vision call**: a linked `CancellationTokenSource` is cancelled after 100 ms. If the network request is still in flight, `OperationCanceledException` is thrown and the boolean is `true`. If the provider responds faster than 100 ms, the call completes and the boolean is `false` — this is a valid outcome, not a failure. Remote verification is not possible for regular chat-completion calls; the cancellation is purely client-side.
- For **batch remote cancellation**: a batch is submitted successfully, then `CancelBatchAsync` is called. The test polls for up to the configured **HTTP timeout** (set in SmartHopper Settings > Network > Timeout) and reports success only when the provider's status becomes `Cancelled`.

#### Notes (Cancellation Tests)

- All tests use `CancellationTokenSource.CreateLinkedTokenSource(token)` so both the programmed timeout and a human toggling **Run?** off propagate cancellation.
- `RunOnlyOnInputChanges = false` so the component re-runs every time **Run?** is toggled.

---

## Developer Reference

The following examples illustrate how the test components are structured and how cancellation tokens are used in the SmartHopper provider framework.

### Implementing a Data Tree Test Component

```csharp
using SmartHopper.Components.Test;
using Grasshopper.Kernel;

// The test component follows the standard Grasshopper pattern
public class DataTreeProcessorEqualPathsTestComponent : GH_Component
{
    public DataTreeProcessorEqualPathsTestComponent()
        : base("Test DataTreeProcessor (Equal Paths)",
               "TEST-DTP-EQ",
               "Manually validate DataTreeProcessor.RunFunctionAsync",
               "SmartHopper", "Testing Data")
    { }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddBooleanParameter("Run?", "R", "Trigger the test", GH_ParamAccess.item, false);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddIntegerParameter("Result", "Res", "Computed result tree", GH_ParamAccess.tree);
        pManager.AddBooleanParameter("Success", "Ok", "True if result matches expected", GH_ParamAccess.item);
        pManager.AddTextParameter("Messages", "Msg", "Diagnostic messages", GH_ParamAccess.list);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        bool run = false;
        if (!DA.GetData(0, ref run) || !run) return;

        // Internal test: A=2, B=5, Expected Sum=7 at path {0}
        // ... validation logic here
    }
}

```

### Using Cancellation Tokens with Providers

```csharp
using SmartHopper.Core.AI.Providers;
using System.Threading;
using System.Threading.Tasks;

public async Task<bool> TestProviderCancellationAsync(IAIProvider provider)
{
    using var cts = new CancellationTokenSource();
    cts.CancelAfter(100); // Cancel after 100 ms

    try
    {
        var result = await provider.CallAsync(
            messages: new[] { new Message { Role = "user", Content = "Hello" } },
            cancellationToken: cts.Token);
        return false; // Completed before cancellation
    }
    catch (OperationCanceledException)
    {
        return true; // Correctly threw on cancellation
    }
}

```

---

## Architecture & Design

Test components are organized into two functional groups: data-tree validation and provider cancellation validation. The `DataTreeProcessorEqualPathsTestComponent` uses hardcoded integer trees so that branch mapping can be verified without external inputs. It disables `RunOnlyOnInputChanges` to ensure the test executes on every toggle of the `Run?` parameter.

The provider cancellation suite contains one component per supported AI provider. Each component exercises four cancellation paths: standard chat completion, batch submission, batch remote cancellation, and vision call cancellation. Standard and vision calls rely on a local timeout because remote cancellation confirmation is not available for individual chat-completion requests. Batch remote cancellation, however, is verified end-to-end by polling the provider's batch status until it reports `Cancelled`.

All cancellation tests use `CancellationTokenSource.CreateLinkedTokenSource` to merge the component's own `Run?` toggle with a programmatic 100 ms timeout. This ensures that both manual and automated cancellation scenarios are covered.

