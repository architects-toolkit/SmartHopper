# SmartHopper Components Test Suite

This section documents the test-only Grasshopper components used to validate SmartHopper provider and utility behavior inside Rhino/Grasshopper.

## Per-provider test runners

The `src/SmartHopper.Components.Test/Providers/` directory contains one component per provider and feature (e.g., `TestOpenAIEncodeComponent`, `TestAnthropicStreamingComponent`). These ~43 components are **intentional per-provider test runners**, not accidental duplication. Each one:

- Inherits from `ProviderTestComponentBase` for shared setup/teardown.
- Keeps its own `ComponentGuid`, display name, nickname, description, outputs, and test-specific `Worker` logic.
- Calls the real provider API (`Encode`, `Decode`, `Call`, `Batch`, `Streaming`, `Tools`, `Vision`) and reports boolean results plus diagnostic messages.

## Shared setup base

`ProviderTestComponentBase` lives in `src/SmartHopper.Components.Test/Providers/ProviderTestComponentBase.cs` and centralizes the boilerplate that every provider test component used to repeat:

```csharp
public abstract class ProviderTestComponentBase : AIStatefulAsyncComponentBase
{
    protected abstract string TestProviderName { get; }

    protected ProviderTestComponentBase(
        string name,
        string nickname,
        string description,
        string category = "SmartHopper Tests",
        string subCategory = "Testing Providers")
        : base(name, nickname, description, category, subCategory)
    {
        this.RunOnlyOnInputChanges = false;
        this.SetSelectedProviderName(this.TestProviderName);
    }

    public override GH_Exposure Exposure => GH_Exposure.secondary;
}
```

Derived components only override `TestProviderName`, pass their name/nickname/description, and implement their test worker:

```csharp
public class TestOpenAIEncodeComponent : ProviderTestComponentBase
{
    public override Guid ComponentGuid => new Guid("AD538781-65B9-4123-B4EE-874D03BD6FC3");
    protected override string TestProviderName => "OpenAI";

    public TestOpenAIEncodeComponent()
        : base("Test OpenAI Encode", "TEST-OPENAI-ENC", "Tests OpenAI message encoding.")
    {
    }

    protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
    {
        return new Worker(this, this.AddRuntimeMessage);
    }

    private sealed class Worker : AsyncWorkerBase
    {
        // provider-specific test logic
    }
}
```

This keeps the test matrix explicit while removing duplicated wiring.

## Other test component groups

- `AiTools/` — tests for AI canvas tools (`gh_get`, `gh_put`, `gh_move`, etc.).
- `Badges/` — tests for component badge rendering.
- `DataProcessor/` — tests for data-tree matching, broadcasting, and grafting utilities.
- `Misc/` — tests for state managers, async prime calculators, and dialogs.

## Running the tests

These components are only built in Debug and are not included in Release builds. Drop them onto a Grasshopper canvas and trigger them to exercise the corresponding provider or utility. Each component reports success/failure booleans and a `Messages` list for diagnostic output.
