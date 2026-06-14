# AIInputAdapterBase

Synchronous, **non-AI** base for components that build an `AIInputPayload` from a single piece of data (text, image, audio, context filter). Sits at the input edge of the AI pipeline — adapter components produced by this base feed [AIOutputAdapterBase](./AIOutputAdapterBase.md) components downstream.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core.Grasshopper/ComponentBase/AIInputAdapterBase.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This base class defines the contract for converting raw data into the AI pipeline's canonical input type. If you are building a new input adapter or need to understand how data flows into the SmartHopper AI system, this documentation explains the uniform shape and constraints all input adapters must follow.

**You should read this if you:**

- Are creating a new input adapter component
- Need to understand how raw data is wrapped into `AIInputPayload`
- Want to know the design constraints for the input edge of the AI pipeline

---

## End-User Guide

Provide a uniform shape for "convert X into the AI pipeline's input type" components without dragging in async/state machinery. The component always exposes a single `Input >` output of type `AIInputPayloadParameter` at index 0.

---

## Developer Reference

### Subclass contract

```csharp
protected abstract Bitmap Icon { get; }

// Optional override (default: "AIInputPayload produced by this adapter."):
protected virtual string PayloadOutputDescription { get; }

// Optional extra outputs starting at index 1:
protected virtual void RegisterAdditionalOutputParams(GH_OutputParamManager pManager);

```

### Helper methods

```csharp
// Helpers do the wrapping:
// CreateTextPayload, CreateImagePayload, CreateAudioPayload, CreateContextPayload
// validate inputs and return AIInputPayload.
// WrapPayload boxes it into GH_AIInputPayload for the GH parameter system.

// Example usage inside a derived adapter:
protected override void SolveInstance(IGH_DataAccess DA)
{
    string text = "";
    if (!DA.GetData(0, ref text)) return;

    var payload = CreateTextPayload(text);
    DA.SetData(0, WrapPayload(payload));
}

```

---

## Architecture & Design

- **No AI calls.** Adapters are pure converters; they never reach a provider.
- **Single canonical output.** `RegisterOutputParams` is `sealed`; subclasses add extra outputs through `RegisterAdditionalOutputParams` starting at index 1.
- **Category locked.** Always `"SmartHopper" / "B. Input"`. Exposure level is per-instance via the constructor.
- **Helpers do the wrapping.** `CreateTextPayload`, `CreateImagePayload`, `CreateAudioPayload`, `CreateContextPayload` validate inputs and return `AIInputPayload`. `WrapPayload` boxes it into `GH_AIInputPayload` for the GH parameter system.

### Related

- `AIInputPayload`, `AIInputPayloadParameter`, `GH_AIInputPayload` in `SmartHopper.Core`.
- [AIOutputAdapterBase](./AIOutputAdapterBase.md) — consumer side.
