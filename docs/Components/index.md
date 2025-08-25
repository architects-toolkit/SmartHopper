# Components

Grasshopper components that form the user-facing interface for SmartHopper.

## Purpose

Expose AI capabilities (chat, list/text generation, image generation, canvas utilities) as standard GH components.

## Key locations

- `src/SmartHopper.Components/` — production components
- `src/SmartHopper.Components.Test/` — test-only components (not built in Release)
- Bases in `src/SmartHopper.Core/ComponentBase/`:
  - [AsyncComponentBase](./ComponentBase/AsyncComponentBase.md) — base for long-running async operations off the UI thread
  - [StatefulAsyncComponentBase](./ComponentBase/StatefulAsyncComponentBase.md) — async state machine with debouncing, progress, and error handling
  - [AIProviderComponentBase](./ComponentBase/AIProviderComponentBase.md) — provider/model selection UI and persistence
  - [AIStatefulAsyncComponentBase](./ComponentBase/AIStatefulAsyncComponentBase.md) — AI provider integration + stateful async execution
  - [SelectingComponentBase](./ComponentBase/SelectingComponentBase.md) — adds a "Select Components" button and selection management
  - AI catalog: [AI Components](./AI/index.md)
  - Test components: [Test](./Test/index.md)
  - [IO](./IO/index.md) — safe, versioned persistence for component outputs

## Behavior

- Components construct `AIBody`, select provider/model, and execute `AIRequestCall`.
- Metrics and errors are surfaced on outputs and runtime messages.
- Supports both button and toggle Run patterns; debounce and state transitions manage re-execution.

## Best practices

- Set `RunOnlyOnInputChanges` appropriately for your component.
- Ensure UI changes occur on Rhino's UI thread.
- Validate tool schemas and model capabilities; give clear, actionable errors.
