---
trigger: model_decision
description: Information about the Context Manager (AIContext)
---

# AIContext Infrastructure

- **Purpose**
  - Provide a lightweight, extensible way to collect runtime context (key–value pairs) from multiple sources and make it available to AI requests.
  - Centralize registration, filtering, and combination of context across the application.

- **Core Concepts**
  - **Providers**: IAIContextProvider: contract for any context source.
  - **Manager**: AIContextManager: static registry and aggregator.

- **Filtering Rules (`providerFilter`)**
  - `*`: include all providers (default behavior).
  - `-*`: exclude all providers.
  - Comma/space separated list of IDs: include only those (e.g., `time, project`).
  - Prefix with `-` to exclude specific providers (e.g., `*, -private`).

- **Combination Behavior**
  - Each provider’s `GetContext()` is merged into a single dictionary.
  - Keys without an underscore `_` are automatically namespaced as `"{ProviderId}_{key}"`.
  - Later entries overwrite earlier ones for duplicate keys.

- **Integration with AICall**
  - Context is automatically injected in `src/SmartHopper.Infrastructure/AICall/AIBody.cs`.
    - In the `AIBody.Interactions` getter, when `ContextFilter` is set and context has content, a synthesized AIInteractionText with `Agent = Context` is inserted at the start of the returned interactions.
    - This injection is non‑mutating: the internal interactions list remains unchanged; the context message is only added to the returned copy.

- **Typical Flow**
  1. Implement a provider: create a class implementing IAIContextProvider and return fast, up‑to‑date context.
  2. Register it during plugin/app startup via `AIContextManager.RegisterProvider()`.
  3. In an AI request, set `AIBody.ContextFilter` (e.g., `"*"`, `"time, project"`, `"*, -private"`).
  4. On access, `AIBody.Interactions` automatically includes a synthesized context message when the filter yields context data.

- **Best Practices**
  - Keep `GetContext()` non‑blocking and light; avoid long operations or UI thread dependencies.
  - Use stable, descriptive keys; rely on namespacing rules or include your own underscores when needed.
  - Register/unregister providers cleanly on lifecycle events to prevent duplicates or stale data.

- **Extensibility**
  - Add new providers without modifying the manager.
  - Filter syntax allows flexible, per‑request control over which context sources apply.