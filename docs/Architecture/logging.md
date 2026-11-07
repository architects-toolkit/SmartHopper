# Structured logging

SmartHopper's logging surface lives in two layers:

- `SmartHopper.ProviderSdk.Logging` — the contracts every layer uses:
  `ILogger`/`ILogger<T>`, the `Logger.For`/`Logger.For<T>` facade,
  `ProviderLogLevel`, async-flowing scopes (`LogScope`/`LogScopeContext`),
  `SmartHopperLogScrubber` (PII scrubbing), `SmartHopperLogExceptionFormatter`,
  and the `DebugLogger` fallback used before a host registers a real logger.
- `SmartHopper.Infrastructure.AICall.Logging` — the sinks: `ILogSink` /
  `ILogFormatter`, `SmartHopperLogEntry`, `SmartHopperLogFormatter`,
  `SmartHopperLiveSink` (Rhino command line) and `SmartHopperLogFileSink`
  (rolling per-day files under the diagnostics log folder).

`ProviderSdkHost.Logger` is the single registered `ILogger`. The host installs
`SmartHopperLogger` (Infrastructure.Hosting) at startup; it routes each entry to
every sink whose per-sink level admits it and rebuilds routing whenever
`SmartHopperSettings` is saved.

## Local-only

Logging is local diagnostics: every sink writes on-machine (Rhino command line
or the rolling log folder) and nothing leaves the machine unless the user
exports a diagnostics zip. Per-sink toggles and minimum
levels live in `SmartHopperSettings` (`LiveOutputEnabled`/`LiveOutputLevel`,
`LogFileEnabled`/`LogFileLevel`, `LogRetentionDays`).

Providers get a typed logger via `AIProvider.Log` (`ILogger` on the base class,
`ILogger<T>` on `AIProvider<T>`); other code uses `Logger.For<T>()` or
`Logger.For(Type)`.

## Conventions

- A caught exception is logged via `Logger.Exception`/`ILogger<T>.Exception`
  (Error level, full type + stack trace + inner exceptions, `exception.type` /
  `exception.stacktrace` scope for structured sinks) — never reduced to
  `{ex.Message}` inside a `Warning`/`Trace` call, which loses everything that
  makes the failure diagnosable.
- Exceptions converted into user-facing results should use the
  `AIReturn.CreateProviderError`/`CreateNetworkError`/`CreateToolError`
  `Exception` overloads, which log first and then build the result — so turning
  a failure into a canvas/tool/MCP response never erases the diagnostic record.
- Deliberate, expected rejections (e.g. an already-terminal batch refusing
  cancel) may stay at `Warning` with a descriptive message; anything unexpected
  — transport, serialization, bugs — goes through `Logger.Exception`.
- Data-shape problems are `SHRuntimeMessage` diagnostics, not exceptions: a
  malformed batch result item produces an `Error`-severity message instead of a
  thrown `JsonException` that would spam the log once per poll tick.

## UI

- `Settings → General` (Logs section) exposes every per-sink toggle and level.
  All sinks are local — nothing leaves the machine unless the user exports a
  diagnostics zip from the `SmartHopper → Diagnostics` menu.
- `SmartHopper → Diagnostics` offers: Open Log Folder (Debug builds), Copy
  Diagnostics for GitHub (a scrubbed markdown report on the clipboard), and
  Generate Diagnostic Zip (scrubbed logs plus `diagnostics.json` with the
  anonymous installation hash and versions, so a shared zip can join a
  collector-side investigation).
