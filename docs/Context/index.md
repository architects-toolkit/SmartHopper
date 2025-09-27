# Context

Context providers enrich AI requests with environment information (such as time), improving relevance and grounding.

## Purpose

Supply dynamic key-value context injected into `AIBody` so prompts and tools can adapt to the user's environment.

## Key locations

- `src/SmartHopper.Core/AIContext/` — interfaces and concrete providers
  - `IAIContextProvider` — contract for context sources
  - `EnvironmentContextProvider`, `TimeContextProvider`, `SelectionContextProvider`

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
- `current-file`: provides `current-file-file-name`, `current-file-selected-count`, `current-file-object-count`, `current-file-component-count`, `current-file-param-count`, `current-file-scribble-count`, `current-file-group-count`

## WebChat defaults

WebChat (both the Canvas Button and the AIChatComponent dialog) enables a curated context set by default:

- `time, environment, current-file`
