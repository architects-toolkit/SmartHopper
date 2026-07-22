# AIOutputAdapterBase

`src/SmartHopper.Core/ComponentBase/AIOutputAdapterBase.cs`

Specialised [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) for components that consume `AIInputPayload` trees, send them to the AI through a forced tool call, and project the response onto one or more typed outputs (e.g. `AI2Boolean`, `AI2Number`, `AI2GhJson`, â€¦).

## Purpose

Eliminate boilerplate for "tree of payloads in â†’ typed values out" components. The base handles parameter shape, branch-by-branch execution, batch interception, decoding via declarative output mappings and atomic finalization.

## Design criteria

- **Branch-by-branch conversation.** `ComponentProcessingOptions` is fixed to `BranchToBranch`: each input branch is one merged `AIBody` â†’ one provider call.
- **Forced tool calling.** `CallAIAsync(body, forceToolName: ...)` is used so the model is constrained to call the adapter's tool. Capabilities are derived from `UsingAiTools` (no need to override `RequiredCapability`).
- **Declarative outputs via `OutputMapping`.** Subclasses declare a list; the base auto-registers Grasshopper outputs and runs every mapping's `Extractor` against the `AIReturn`. The first mapping is the *primary* output for batch reconstruction.
- **`OutputMapping.Single` helper** wraps a scalar extractor into the unified `IEnumerable<IGH_Goo>` contract; list-shaped extractors return their list.
- **Symmetric batch and sync paths.** Both legs run the same `DecodeAllMappings` helper, so list-shaped outputs work transparently in batch mode. The legacy `SentinelTransformOutputs` hook is **not** invoked by the adapter base â€” declare every named output through `GetOutputMappings` instead.
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
  â†’ CallAIAsync(body, forceToolName)
  â†’ DecodeAllMappings(AIReturn)              â† runs every Extractor
  â†’ FinishResults(primary, â€¦extras)          â† atomic persistence + metrics
```

In batch mode the call is queued, `OnBatchCompleted` invokes `ProcessMappingsBatchResults` which calls the same `DecodeAllMappings` helper before delegating to `FinishResults`.

## Related

- [AIInputAdapterBase](./AIInputAdapterBase.md) â€” producer side.
- [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) â€” execution model and metrics.

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about AIOutputAdapterBase.


## End-User Guide

End-user guidance for AIOutputAdapterBase.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for AIOutputAdapterBase.
