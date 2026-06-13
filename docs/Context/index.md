# Context

Context providers enrich AI requests with environment information (such as time), improving relevance and grounding.

## Purpose

Supply dynamic key-value context injected into `AIBody` so prompts and tools can adapt to the user's environment.

## Key locations

- `src/SmartHopper.Core/AIContext/` — interfaces and concrete providers
  - `IAIContextProvider` — contract for context sources
  - `EnvironmentContextProvider` (ProviderId: `environment`)
  - `TimeContextProvider` (ProviderId: `time`)
  - `FileContextProvider` (ProviderId: `current-file`)

## How it works

- The component builds `AIBody` and can include context filters.
- The context manager aggregates values from registered providers.
- ContextProviders return small, well-scoped dictionaries (e.g., OS, Rhino/Grasshopper info, timestamps).

## Guidance

- Keep context minimal, deterministic, and privacy-aware.
- Prefer whitelisting of keys; avoid leaking sensitive data.
- Document each provider's keys and intended consumers (prompts/tools).

## Available providers

- `time`: provides `time_current-datetime`, `time_current-timezone`
- `environment`: provides `environment_operating-system`, `environment_rhino-version`, `environment_platform`
- `current-file`: provides `current-file-file-name`, `current-file-selected-count`, `current-file-selected-component-count`, `current-file-selected-param-count`, `current-file-selected-objects`, `current-file-object-count`, `current-file-component-count`, `current-file-param-count`, `current-file-scribble-count`, `current-file-group-count`

## Bootstrapping

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

## WebChat defaults

WebChat (both the Canvas Button and the AIChatComponent dialog) enables a curated context set by default:

- `time, environment, current-file`
