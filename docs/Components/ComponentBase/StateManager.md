# ComponentState

`src/SmartHopper.Core/ComponentBase/StateManager.cs`

Defines the state enum used by [StatefulComponentBase](./StatefulComponentBase.md) and [ComponentStateManager](./ComponentStateManager.md), plus a `ToMessageString` extension used to render the component message.

## States

| State | Meaning |
| --- | --- |
| `Completed` | Initial / idle. All workers finished; previous outputs are emitted. |
| `Waiting` | Toggle Run is true; idle until inputs change. |
| `NeedsRun` | Inputs changed while Run is false. Awaits Run = true. |
| `Processing` | A worker is running. |
| `Cancelled` | Manually cancelled. |
| `Error` | A persistent error was reported. |

`ComponentStateExtensions.ToMessageString(state, progressInfo?)` produces the friendly message shown on the component (`"Run me!"`, `"Process N/M..."`, `"Done"`, etc.). Pass a [ProgressInfo](./ProgressInfo.md) to render counters during `Processing`.

> Note: `StateManager.cs` only defines the enum and the extension. The runtime state machine (transitions, debounce, hash tracking) lives in [ComponentStateManager](./ComponentStateManager.md).
