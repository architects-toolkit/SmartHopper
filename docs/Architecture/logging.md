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

## Consent

Logging is diagnostics: it follows `SmartHopperSettings.EffectiveDiagnosticsEnabled`
— there is no separate local-logging override. Per-sink toggles and minimum
levels live next to it (`LiveOutputEnabled`/`LiveOutputLevel`,
`LogFileEnabled`/`LogFileLevel`, `LogRetentionDays`).

Providers get a typed logger via `AIProvider.Log` (`ILogger` on the base class,
`ILogger<T>` on `AIProvider<T>`); other code uses `Logger.For<T>()` or
`Logger.For(Type)`.

## UI

- `Settings → Logging` exposes every per-sink toggle and level; the collector
  shipping switch (`OtlpLogEnabled`/`OtlpLogLevel`) only ships while
  `DiagnosticsEnabled` is on.
- `SmartHopper → Diagnostics` offers: Open Log Folder (Debug builds), Copy
  Diagnostics for GitHub (a scrubbed markdown report on the clipboard), and
  Generate Diagnostic Zip (scrubbed logs plus `diagnostics.json` with the
  anonymous installation hash and versions, so a shared zip can join a
  collector-side investigation).
