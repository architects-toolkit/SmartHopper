# Context

Context providers enrich AI requests with environment information (such as time), improving relevance and grounding.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Core/Context/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Context providers allow AI prompts and tools to adapt to the user's current environment without hardcoding assumptions. Understanding how they are registered, bootstrapped, and consumed is important when adding new providers or troubleshooting why certain context values are missing.

**You should read this if you:**

- Want to understand how environment data reaches AI prompts and tools
- Need to add a new context provider (e.g., for plugin-specific state)
- Are debugging missing context in WebChat or component AI requests

---

## End-User Guide

### Purpose

Supply dynamic key-value context injected into `AIBody` so prompts and tools can adapt to the user's environment.

### How it works

- The component builds `AIBody` and can include context filters.
- The context manager aggregates values from registered providers.
- ContextProviders return small, well-scoped dictionaries (e.g., OS, Rhino/Grasshopper info, timestamps).

### Available providers

- `time`: provides `time_current-datetime`, `time_current-timezone`
- `environment`: provides `environment_operating-system`, `environment_rhino-version`, `environment_platform`
- `current-file`: provides `current-file-file-name`, `current-file-selected-count`, `current-file-selected-component-count`, `current-file-selected-param-count`, `current-file-selected-objects`, `current-file-object-count`, `current-file-component-count`, `current-file-param-count`, `current-file-scribble-count`, `current-file-group-count`

### WebChat defaults

WebChat (both the Canvas Button and the AIChatComponent dialog) enables a curated context set by default:

- `time, environment, current-file`

---

## Developer Reference

### Key locations

- `src/SmartHopper.Core/AIContext/` — interfaces and concrete providers
  - `IAIContextProvider` — contract for context sources
  - `EnvironmentContextProvider` (ProviderId: `environment`)
  - `TimeContextProvider` (ProviderId: `time`)
  - `FileContextProvider` (ProviderId: `current-file`)

### Bootstrapping

Context providers are registered at startup via `AIContextBootstrapper`, ensuring they are globally available.

- File: `src/SmartHopper.Core/AIContext/AIContextBootstrapper.cs`
- **Idempotent initialization**: Safe to call multiple times; uses double-checked locking to ensure providers are registered exactly once
- **Module initializer**: Automatically invoked when the assembly loads via `[ModuleInitializer]` attribute
- **Manual initialization**: Can be triggered explicitly via `AIContextBootstrapper.EnsureInitialized()`
- **Registered providers**:
  - `TimeContextProvider` — provides current date/time and timezone
  - `EnvironmentContextProvider` — provides OS, Rhino version, and platform info
  - `FileContextProvider` — provides current file and selection metadata
- **Registration mechanism**: Uses `AIContextManager.RegisterProvider()` which is idempotent by ProviderId
- **Error handling**: Initialization errors are logged to debug output but do not throw

### Initialization Flow

1. Assembly loads → `[ModuleInitializer]` attribute triggers `Init()`
2. `Init()` calls `EnsureInitialized()`
3. Double-checked lock ensures single initialization
4. All three default providers are registered with `AIContextManager`
5. Subsequent calls to `EnsureInitialized()` return early (no-op)

### Usage

Providers are automatically available after assembly load. To manually ensure initialization:

```csharp
AIContextBootstrapper.EnsureInitialized();

```

### Example: Consuming context in a tool or prompt

```csharp
// Retrieve aggregated context from the manager
var context = AIContextManager.GetContext();

// Access specific provider values
if (context.TryGetValue("time_current-datetime", out var dateTime))
{
    Console.WriteLine($"Current time: {dateTime}");
}

if (context.TryGetValue("environment_rhino-version", out var rhinoVersion))
{
    Console.WriteLine($"Running on Rhino {rhinoVersion}");
}

// Inject into AIBody for a prompt or tool
var body = new AIBody();
body.SetContext(context);

```

---

## Architecture & Design

The context system is intentionally minimal and privacy-aware:

- **Minimal footprint**: Each provider returns a small, well-scoped dictionary. There are no large serialized objects or unbounded collections.
- **Deterministic keys**: Provider keys follow a `providerid_key-name` convention so collisions are avoided and consumers can reliably check for specific values.
- **Privacy by design**: Sensitive data is not collected. File context exposes counts and names, not full geometry or proprietary data.
- **Whitelist-friendly**: Consumers can filter the full context map to only the keys they need, making it easy to audit what information is actually sent to an AI provider.

Guidance for extending the system:

- Keep context minimal, deterministic, and privacy-aware.
- Prefer whitelisting of keys; avoid leaking sensitive data.
- Document each provider's keys and intended consumers (prompts/tools).
