# ComponentStateManager

Sealed, thread-safe class that owns the per-component state machine for [StatefulComponentBase](./StatefulComponentBase.md). Each component creates one instance in its constructor.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core/ComponentBase/Cores/ComponentStateManager.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This class centralizes all state, debounce, hash tracking, and file-restoration logic for every SmartHopper component. Understanding it is essential when debugging transition loops, missing solves after file load, or debounce timing issues.

**You should read this if you:**

- Are building or debugging a `StatefulComponentBase` subclass
- Need to understand how input-change detection works across solves
- Want to customize debounce or restoration behavior

---

## End-User Guide

### Responsibilities

- **State transitions.** `RequestTransition(state, reason)` validates the transition through `IsValidTransition`, queues it if a transition is already in flight, and raises `StateChanged` / `StateEntered` / `StateExited`.
- **Debounce.** `StartDebounce(targetState, ms)` arms a `System.Threading.Timer` with a generation token; `CancelDebounce()` invalidates the timer.
- **Hash tracking.** Holds two dictionaries — _committed_ (last successfully processed) and _pending_ (current solve). `GetChangedInputs()` returns the per-input diff (or empty during restoration / suppression).
- **Restoration.** `BeginRestoration` / `EndRestoration` / `ClearSuppressionAfterFirstSolve` / `SuppressInputChangesForNextSolve` together guarantee that loading a `.gh` file does not classify the restored values as an input change.
- **Persistence helpers.** `GetCommittedHashes`, `RestoreCommittedHashes` and equivalents for branch counts so `StatefulComponentBase.Write/Read` can round-trip data.

### Reasons (`TransitionReason`)

`Initial`, `InputChanged`, `RunEnabled`, `RunDisabled`, `DebounceComplete`, `ProcessingComplete`, `Cancelled`, `Error`, `FileRestoration` — used purely for logging and diagnostics.

---

## Developer Reference

### Public API summary

| Group | Members |
| --- | --- |
| State | `CurrentState`, `IsTransitioning`, `RequestTransition(state, reason)`, `IsValidTransition(from, to)`, `ForceState(state)`, `ClearPendingTransitions`, `Reset` |
| Restoration | `IsRestoringFromFile`, `IsSuppressingInputChanges`, `BeginRestoration`, `EndRestoration`, `ClearSuppressionAfterFirstSolve`, `SuppressInputChangesForNextSolve` |
| Debounce | `IsDebouncing`, `StartDebounce(state, ms)`, `CancelDebounce` |
| Hashes | `UpdatePendingHashes(dict)`, `UpdatePendingBranchCounts(dict)`, `CommitHashes`, `RestoreCommittedHashes(hashes, branches)`, `GetChangedInputs`, `GetCommittedHashes`, `GetCommittedBranchCounts`, `ClearHashes` |
| Events | `StateChanged`, `StateEntered`, `StateExited`, `DebounceStarted`, `DebounceCancelled`, `TransitionRejected` |

### Usage Examples

```csharp
// Example: Requesting a state transition
var manager = new ComponentStateManager();
manager.StateChanged += (s, e) => 
    Debug.WriteLine($"State changed to {e.NewState} because {e.Reason}");

// Request a valid transition
bool accepted = manager.RequestTransition(
    ComponentState.Processing, 
    TransitionReason.InputChanged);

```

```csharp
// Example: Debounce and hash tracking
manager.StartDebounce(ComponentState.Idle, 500);

// During solve, compare pending hashes against committed
var changed = manager.GetChangedInputs();
manager.UpdatePendingHashes(currentHashes);
manager.UpdatePendingBranchCounts(currentBranchCounts);

// After successful processing, commit the new hashes
manager.CommitHashes();

```

---

## Architecture & Design

- **Single source of truth.** No state lives on the component itself — only the manager. This eliminates the classic "two states out of sync" bugs.
- **Lock-isolated.** Two locks (`stateLock`, `hashLock`) guard the mutable fields; events are raised without holding the lock to avoid re-entrant deadlocks.
- **Generation-based debounce.** Each `StartDebounce` increments a generation counter; the timer callback ignores itself if its generation no longer matches, preventing stale callbacks from firing transitions on a newer state.
- **Friendly to GH save/load.** Restoration is explicit, suppressing change detection only for the first solve after `Read`.
