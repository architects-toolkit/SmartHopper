# ProviderManager

Discovers, verifies, registers, and initializes AI providers at runtime.

- Source: `src/SmartHopper.Infrastructure/AIProviders/ProviderManager.cs`

## Purpose

Provide a central service to load external providers (`SmartHopper.Providers.*.dll`), enforce trust, and manage settings.

## Key features

- Discovery
  - Scans the app-local directory and `%AppData%/SmartHopper/Providers` for `SmartHopper.Providers.*.dll`.
  - Each candidate is loaded into a per-provider `AssemblyLoadContext` (see `ProviderAssemblyLoader`) so private dependencies stay isolated.
  - SDK type identity (`IAIProviderFactory`) is validated before activation. Mismatch → `Invalid`.
  - SemVer compatibility is checked via `BuiltAgainstSdk`/`MinHostSdk` assembly attributes.
- Cryptographic classification (`ProviderClassifier`)
  - `Official` — strong-name token matches host AND/OR Authenticode matches host AND/OR SHA-256 is in the published manifest, with no contradicting signal.
  - `OfficialTampered` — one signal says official but another contradicts. Always blocked.
  - `Community` — valid managed assembly not tied to SmartHopper. Subject to `AllowCommunityProviders` and `BlockNonOfficialProviders`.
  - `Invalid` — load failure, missing factory, SDK type-identity mismatch, version incompatibility.
- Trust settings (`SmartHopperSettings`)
  - `AllowCommunityProviders` (default `false`): community providers are blocked unless this is on.
  - `BlockNonOfficialProviders` (default `false`): hard override that allows only `Official` providers.
  - `ProviderIntegrityCheckMode` continues to govern hash-mismatch behavior for Official providers.
  - `TrustedProviderRecords` — structured per-provider trust records (legacy `TrustedProviders` boolean map is migrated automatically).
- Registration & initialization
  - Duplicate provider ids: Official > Community > everything else. Tampered/Invalid never win.
  - `InitializeProviderAsync()` is wrapped in a 30-second per-provider timeout so a hanging provider can't block discovery.
- Accessors
  - `GetProviders(includeUntrusted=false)`
  - `GetProvider(name)` (handles "Default" indirection)
  - `GetProviderSettings(name)`, `GetProviderAssembly(name)`, `GetProviderIcon(name)`
  - `GetDefaultAIProvider()`
  - `GetProviderClassification(name)`, `IsProviderCommunity(name)`, `IsProviderUnsigned(name)`, `IsProviderMismatched(name)`, `IsProviderUnknown(name)`, `IsProviderUnavailable(name)`, `GetProviderTrustRecord(name)`
- Settings management
  - `UpdateProviderSettings(name, Dictionary<string, object>)`
  - Validates via `IAIProviderSettings.ValidateSettings`, persists via `SmartHopperSettings`, refreshes provider cache.

## Usage

Call `ProviderManager.Instance.RefreshProviders()` during app init to discover and initialize providers.
Use accessors to obtain providers and manage settings from UI components.

## Relationships

- `IAIProviderFactory` — factory contract implemented by external provider assemblies.
- `IAIProvider`/`AIProvider` — providers created and initialized by the manager.
- `IAIProviderSettings` — per-provider descriptors and validation.
- `SmartHopperSettings` — persistence for trust and provider settings.

## UI/UX notes

- Trust prompts run on the UI thread via `RhinoApp.InvokeOnUiThread`.
- Duplicate/disabled providers are filtered by trust status.
