# SelectingStatefulComponentBase

`src/SmartHopper.Core/ComponentBase/SelectingStatefulComponentBase.cs`

Combines [StatefulComponentBase](./StatefulComponentBase.md) with the *Select Components* button machinery, without any AI. Implements `ISelectingComponent` and delegates to `SelectingComponentCore` (same helper used by [SelectingComponentBase](./SelectingComponentBase.md) and [AISelectingStatefulAsyncComponentBase](./AISelectingStatefulAsyncComponentBase.md)).

## When to derive

- You need a Select button and persistent outputs, but no AI provider integration.
- For a non-stateful version use [SelectingComponentBase](./SelectingComponentBase.md). For AI use [AISelectingStatefulAsyncComponentBase](./AISelectingStatefulAsyncComponentBase.md).

## Notes

- Uses `SelectingComponentAttributes` (the non-AI flavour) so the component shows the Select button without provider badges.
- `Write` / `Read` chain to both `StatefulComponentBase` (state, hashes, outputs) and `SelectingComponentCore` (selection GUIDs).

See [SelectingComponentBase](./SelectingComponentBase.md) for the full description of the selection pipeline and design criteria â€” they are shared.

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about SelectingStatefulComponentBase.


## End-User Guide

End-user guidance for SelectingStatefulComponentBase.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for SelectingStatefulComponentBase.

```csharp
// Example code for Developer Reference
```

```csharp
// Additional example for Developer Reference
```