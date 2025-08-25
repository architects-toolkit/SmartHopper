# Policy Pipeline

This page explains the always-on request/response policy pipeline that runs around every provider call.

- Location: `src/SmartHopper.Infrastructure/AICall/Policies/`
- Core types:
  - `PolicyPipeline` — orchestrates request and response policies
  - Default response policy: `CompatibilityDecodeResponsePolicy`

## What policies do

- Request policies run before the provider call to validate and normalize the request.
- Response policies run after the provider call to decode raw JSON, standardize fields, and attach diagnostics via `AIReturn.AddRuntimeMessage(...)`.

## Default pipeline behavior

- The default pipeline includes `CompatibilityDecodeResponsePolicy` which:
  - Attempts to decode raw provider JSON (`AIReturn.Raw`) using `ProviderInstance.Decode(...)`.
  - On success: appends decoded interactions to the `AIReturn.Body`.
  - On failure: does not throw; attaches a warning message preserving the raw payload for later inspection.

## Developer guidance

- When adding new providers, implement `Encode(...)` and `Decode(...)` so the pipeline can operate consistently.
- Prefer attaching structured diagnostics through `AIReturn.AddRuntimeMessage(...)` rather than writing directly to logs.
- Avoid mutating interaction lists outside the decoding phase; rely on `AIBody` aggregation rules.

## See also

- Requests: `AIRequestCall` — `docs/Providers/AICall/requests.md`
- Body and message aggregation — `docs/Providers/AICall/body-metrics-status.md` and `docs/Providers/AICall/messages.md`
