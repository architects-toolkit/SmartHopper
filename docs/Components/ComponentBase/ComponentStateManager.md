# ComponentStateManager

`src/SmartHopper.Core/ComponentBase/ComponentStateManager.cs`

Sealed, thread-safe class that owns the per-component state machine for [StatefulComponentBase](./StatefulComponentBase.md). Each component creates one instance in its constructor.

## Responsibilities

- **State transitions.** `RequestTransition(state, reason)` validates the transition through `IsValidTransition`, queues it if a transition is already in flight, and raises `StateChanged` / `StateEntered` / `StateExited`.
- **Debounce.** `StartDebounce(targetState, ms)` arms a `System.Threading.Timer` with a generation token; `CancelDebounce()` invalidates the timer.
- **Hash tracking.** Holds two dictionaries â€” *committed* (last successfully processed) and *pending* (current solve). `GetChangedInputs()` returns the per-input diff (or empty during restoration / suppression).
- **Restoration.** `BeginRestoration` / `EndRestoration` / `ClearSuppressionAfterFirstSolve` / `SuppressInputChangesForNextSolve` together guarantee that loading a `.gh` file does not classify the restored values as an input change.
- **Persistence helpers.** `GetCommittedHashes`, `RestoreCommittedHashes` and equivalents for branch counts so `StatefulComponentBase.Write/Read` can round-trip data.

## Public API summary

| Group | Members |
| --- | --- |
| State | `CurrentState`, `IsTransitioning`, `RequestTransition(state, reason)`, `IsValidTransition(from, to)`, `ForceState(state)`, `ClearPendingTransitions`, `Reset` |
| Restoration | `IsRestoringFromFile`, `IsSuppressingInputChanges`, `BeginRestoration`, `EndRestoration`, `ClearSuppressionAfterFirstSolve`, `SuppressInputChangesForNextSolve` |
| Debounce | `IsDebouncing`, `StartDebounce(state, ms)`, `CancelDebounce` |
| Hashes | `UpdatePendingHashes(dict)`, `UpdatePendingBranchCounts(dict)`, `CommitHashes`, `RestoreCommittedHashes(hashes, branches)`, `GetChangedInputs`, `GetCommittedHashes`, `GetCommittedBranchCounts`, `ClearHashes` |
| Events | `StateChanged`, `StateEntered`, `StateExited`, `DebounceStarted`, `DebounceCancelled`, `TransitionRejected` |

## Design criteria

- **Single source of truth.** No state lives on the component itself â€” only the manager. This eliminates the classic "two states out of sync" bugs.
- **Lock-isolated.** Two locks (`stateLock`, `hashLock`) guard the mutable fields; events are raised without holding the lock to avoid re-entrant deadlocks.
- **Generation-based debounce.** Each `StartDebounce` increments a generation counter; the timer callback ignores itself if its generation no longer matches, preventing stale callbacks from firing transitions on a newer state.
- **Friendly to GH save/load.** Restoration is explicit, suppressing change detection only for the first solve after `Read`.

## Reasons (`TransitionReason`)

`Initial`, `InputChanged`, `RunEnabled`, `RunDisabled`, `DebounceComplete`, `ProcessingComplete`, `Cancelled`, `Error`, `FileRestoration` â€” used purely for logging and diagnostics.

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-07-21
- Documentation Maintainer: Marc Roca Musach

---


## Why Read This?

This document provides details about ComponentStateManager.


## End-User Guide

End-user guidance for ComponentStateManager.


## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for ComponentStateManager.

```csharp
// Example code for Developer Reference
```

```csharp
// Additional example for Developer Reference
```