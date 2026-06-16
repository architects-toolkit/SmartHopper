# AIOutputAdapterBase

Specialised [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) for components that consume `AIInputPayload` trees, send them to the AI through a forced tool call, and project the response onto one or more typed outputs (e.g. `AI2Boolean`, `AI2Number`, `AI2GhJson`, …).

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/AIOutputAdapterBase.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This base class eliminates boilerplate for components that turn AI payload trees into typed Grasshopper outputs. If you are building a new "AI2X" style component or need to understand how the adapter pattern maps AI responses to parameters, this is the authoritative reference.

**You should read this if you:**

- Are creating a new output adapter component (e.g. `AI2Boolean`, `AI2Number`)
- Need to understand how `OutputMapping` declaratively drives parameter generation
- Want to know how branch-by-branch execution and batch mode work for output adapters

---

## End-User Guide

Eliminate boilerplate for "tree of payloads in → typed values out" components. The base handles parameter shape, branch-by-branch execution, batch interception, decoding via declarative output mappings and atomic finalization.

---

## Developer Reference

### Subclass contract

```csharp
protected abstract string GetInternalSystemPrompt();
protected abstract Bitmap Icon { get; }
protected abstract IReadOnlyList<OutputMapping> GetOutputMappings();    // must be non-empty

// Optional:
protected virtual void GatherAdditionalInputs(IGH_DataAccess DA, Dictionary<string, object> additionalInputs);
protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager);
protected override IReadOnlyList<string> UsingAiTools => new[] { "..." };

```

### `OutputMapping`

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

### Execution flow

```csharp
// Conceptual execution steps inside the adapter base:
// 1. PrepareInputs merges the Input> branch into an AIBody and prepends the system prompt.
// 2. CallAIAsync(body, forceToolName) sends the request.
// 3. DecodeAllMappings(AIReturn) runs every Extractor for each registered OutputMapping.
// 4. FinishResults(primary, …extras) handles atomic persistence and metrics.

// In batch mode:
// - The call is queued via OnBatchCompleted.
// - ProcessMappingsBatchResults calls the same DecodeAllMappings helper.
// - Then delegates to FinishResults for persistence.

```

---

## Architecture & Design

- **Branch-by-branch conversation.** `ComponentProcessingOptions` is fixed to `BranchToBranch`: each input branch is one merged `AIBody` → one provider call.
- **Forced tool calling.** `CallAIAsync(body, forceToolName: ...)` is used so the model is constrained to call the adapter's tool. Capabilities are derived from `UsingAiTools` (no need to override `RequiredCapability`).
- **Declarative outputs via `OutputMapping`.** Subclasses declare a list; the base auto-registers Grasshopper outputs and runs every mapping's `Extractor` against the `AIReturn`. The first mapping is the *primary* output for batch reconstruction.
- **`OutputMapping.Single` helper** wraps a scalar extractor into the unified `IEnumerable<IGH_Goo>` contract; list-shaped extractors return their list.
- **Symmetric batch and sync paths.** Both legs run the same `DecodeAllMappings` helper, so list-shaped outputs work transparently in batch mode. The legacy `SentinelTransformOutputs` hook is **not** invoked by the adapter base — declare every named output through `GetOutputMappings` instead.
- **Sealed input shape.** Adds `Input >` (`AIInputPayloadParameter`, tree access) at index 0, then chains to `base.RegisterInputParams`. Subclasses use `RegisterAdditionalInputParams` for extra inputs and `GatherAdditionalInputs(DA, dict)` to inject them into the per-branch input dictionary.
- **Category locked** to `"SmartHopper" / "C. Output"`.

### Related

- [AIInputAdapterBase](./AIInputAdapterBase.md) — producer side.
- [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) — execution model and metrics.
