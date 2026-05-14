# AIOutputAdapterBase

`src/SmartHopper.Core/ComponentBase/AIOutputAdapterBase.cs`

Specialised [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) for components that consume `AIInputPayload` trees, send them to the AI through a forced tool call, and project the response onto one or more typed outputs (e.g. `AI2Boolean`, `AI2Number`, `AI2GhJson`, …).

## Purpose

Eliminate boilerplate for "tree of payloads in → typed values out" components. The base handles parameter shape, branch-by-branch execution, batch interception, decoding via declarative output mappings and atomic finalization.

## Design criteria

- **Branch-by-branch conversation.** `ComponentProcessingOptions` is fixed to `BranchToBranch`: each input branch is one merged `AIBody` → one provider call.
- **Forced tool calling.** `CallAIAsync(body, forceToolName: ...)` is used so the model is constrained to call the adapter's tool. Capabilities are derived from `UsingAiTools` (no need to override `RequiredCapability`).
- **Declarative outputs via `OutputMapping`.** Subclasses declare a list; the base auto-registers Grasshopper outputs and runs every mapping's `Extractor` against the `AIReturn`. The first mapping is the *primary* output for batch reconstruction.
- **`OutputMapping.Single` helper** wraps a scalar extractor into the unified `IEnumerable<IGH_Goo>` contract; list-shaped extractors return their list.
- **Symmetric batch and sync paths.** Both legs run the same `DecodeAllMappings` helper, so list-shaped outputs work transparently in batch mode. The legacy `SentinelTransformOutputs` hook is **not** invoked by the adapter base — declare every named output through `GetOutputMappings` instead.
- **Sealed input shape.** Adds `Input >` (`AIInputPayloadParameter`, tree access) at index 0, then chains to `base.RegisterInputParams`. Subclasses use `RegisterAdditionalInputParams` for extra inputs and `GatherAdditionalInputs(DA, dict)` to inject them into the per-branch input dictionary.
- **Category locked** to `"SmartHopper" / "Output"`.

## Subclass contract

```csharp
protected abstract string GetInternalSystemPrompt();
protected abstract Bitmap Icon { get; }
protected abstract IReadOnlyList<OutputMapping> GetOutputMappings();    // must be non-empty

// Optional:
protected virtual void GatherAdditionalInputs(IGH_DataAccess DA, Dictionary<string, object> additionalInputs);
protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager);
protected override IReadOnlyList<string> UsingAiTools => new[] { "..." };
```

## `OutputMapping`

```csharp
public class OutputMapping
{
    public string ParamName { get; set; }
    public string NickName { get; set; }       // defaults to first char of ParamName
    public string Description { get; set; }    // defaults to "Output: {ParamName}"
    public GH_ParamAccess Access { get; set; } = GH_ParamAccess.tree;
    public Type ParamType { get; set; }        // GH parameter type, e.g. typeof(Param_Boolean)
    public Func<AIReturn, IEnumerable<IGH_Goo>> Extractor { get; set; }

    public static Func<AIReturn, IEnumerable<IGH_Goo>> Single(Func<AIReturn, IGH_Goo> extractor);
}
```

## Execution

```text
PrepareInputs (merge Input> branch into AIBody, prepend system prompt)
  → CallAIAsync(body, forceToolName)
  → DecodeAllMappings(AIReturn)              ← runs every Extractor
  → FinishResults(primary, …extras)          ← atomic persistence + metrics
```

In batch mode the call is queued, `OnBatchCompleted` invokes `ProcessMappingsBatchResults` which calls the same `DecodeAllMappings` helper before delegating to `FinishResults`.

## Related

- [AIInputAdapterBase](./AIInputAdapterBase.md) — producer side.
- [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) — execution model and metrics.
