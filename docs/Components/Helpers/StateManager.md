# StateManager

Defines component states and helpers to present user‑friendly messages.

## Purpose

Provide a compact state model used by stateful components to drive execution flow and UI feedback.

## Key features

- `ComponentState` enum: Completed, Waiting, NeedsRun, Processing, Cancelled, Error.
- Extensions to convert states to short, user‑facing messages.
- Shared by all stateful bases to keep behavior consistent.

## Usage

- Rely on base classes to transition state; avoid ad‑hoc state handling.
- Use the friendly messages on UI or outputs when appropriate.

## Related

- [StatefulComponentBase](../ComponentBase/StatefulComponentBase.md), [AIStatefulAsyncComponentBase](../ComponentBase/AIStatefulAsyncComponentBase.md) – consumers of these states.
