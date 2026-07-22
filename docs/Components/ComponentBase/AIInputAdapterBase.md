# AIInputAdapterBase

`src/SmartHopper.Core/ComponentBase/AIInputAdapterBase.cs`

Synchronous, **non-AI** base for components that build an `AIInputPayload` from a single piece of data (text, image, audio, context filter). Sits at the input edge of the AI pipeline â€” adapter components produced by this base feed [AIOutputAdapterBase](./AIOutputAdapterBase.md) components downstream.

## Purpose

Provide a uniform shape for "convert X into the AI pipeline's input type" components without dragging in async/state machinery. The component always exposes a single `Input >` output of type `AIInputPayloadParameter` at index 0.

## Design criteria

- **No AI calls.** Adapters are pure converters; they never reach a provider.
- **Single canonical output.** `RegisterOutputParams` is `sealed`; subclasses add extra outputs through `RegisterAdditionalOutputParams` starting at index 1.
- **Category locked.** Always `"SmartHopper" / "Input"`. Exposure level is per-instance via the constructor.
- **Helpers do the wrapping.** `CreateTextPayload`, `CreateImagePayload`, `CreateAudioPayload`, `CreateContextPayload` validate inputs and return `AIInputPayload`. `WrapPayload` boxes it into `GH_AIInputPayload` for the GH parameter system.

## Subclass contract

```csharp
protected abstract Bitmap Icon { get; }

// Optional override (default: "AIInputPayload produced by this adapter."):
protected virtual string PayloadOutputDescription { get; }

// Optional extra outputs starting at index 1:
protected virtual void RegisterAdditionalOutputParams(GH_OutputParamManager pManager);
```

## Related

- `AIInputPayload`, `AIInputPayloadParameter`, `GH_AIInputPayload` in `SmartHopper.Core`.
- [AIOutputAdapterBase](./AIOutputAdapterBase.md) â€” consumer side.

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about AIInputAdapterBase.


## End-User Guide

End-user guidance for AIInputAdapterBase.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for AIInputAdapterBase.

```csharp
// Example code for Developer Reference
```

```csharp
// Additional example for Developer Reference
```