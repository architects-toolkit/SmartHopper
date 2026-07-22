# AISelectingStatefulAsyncComponentBase

`src/SmartHopper.Core/ComponentBase/AISelectingStatefulAsyncComponentBase.cs`

Extends [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md) with the canvas selection UI. Implements `ISelectingComponent`. Delegates selection to the shared `SelectingComponentCore` helper.

## Purpose

AI components that need the user to pick other Grasshopper objects on the canvas as part of their input â€” for example *Smart Connect* and other canvas-aware AI utilities.

## What it adds

- Constructor creates a `SelectingComponentCore` and subscribes to document events.
- `CreateAttributes` installs `AISelectingComponentAttributes`, which extends [`ComponentBadgesAttributes`](./AIStatefulAsyncComponentBase.md) so badges (provider, model) coexist with the Select button. The provider tooltip is rendered last so it stays above the Select overlay.
- Adds a *Select Components* item to the context menu.
- `Write` / `Read` chain into `SelectingComponentCore` for GUID-based persistence on top of all the AI persistence (batch state, sentinels, hashes, outputs).
- `RemovedFromDocument` unsubscribes the selection core from document events.

## Public selection API

```csharp
public List<IGH_DocumentObject> SelectedObjects { get; }
public void EnableSelectionMode();
```

`SelectedObjects` is auto-pruned of deleted references on every read.

## Design criteria

Same as [SelectingComponentBase](./SelectingComponentBase.md) â€” selection logic is shared via `SelectingComponentCore`, GUIDs are persisted, UI work runs on Rhino's UI thread. The AI base contributes nothing new to the selection pipeline; it only ensures the Select button cooperates with badge rendering.

## Related

- [AIStatefulAsyncComponentBase](./AIStatefulAsyncComponentBase.md)
- [SelectingComponentBase](./SelectingComponentBase.md)

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about AISelectingStatefulAsyncComponentBase.


## End-User Guide

End-user guidance for AISelectingStatefulAsyncComponentBase.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for AISelectingStatefulAsyncComponentBase.

```csharp
// Example code for Developer Reference
```

```csharp
// Additional example for Developer Reference
```