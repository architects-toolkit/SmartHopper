# Context

Context providers enrich AI requests with environment information (such as time), improving relevance and grounding.

## Purpose

Supply dynamic key-value context injected into `AIBody` so prompts and tools can adapt to the user's environment.

## Key locations

- `src/SmartHopper.Core/AIContext/` — interfaces and concrete providers
  - `IAIContextProvider` — contract for context sources
  - `EnvironmentContextProvider`, `TimeContextProvider`, etc.

## How it works

- The component builds `AIBody` and can include context filters.
- The context manager aggregates values from registered providers.
- ContextProviders return small, well-scoped dictionaries (e.g., OS, Rhino/Grasshopper info, timestamps).

## Guidance

- Keep context minimal, deterministic, and privacy-aware.
- Prefer whitelisting of keys; avoid leaking sensitive data.
- Document each provider's keys and intended consumers (prompts/tools).
