# ProviderManager

Discovers, verifies, registers, and initializes AI providers at runtime.

- Source: `src/SmartHopper.Infrastructure/AIProviders/ProviderManager.cs`

## Purpose

Provide a central service to load external providers (`SmartHopper.Providers.*.dll`), enforce trust, and manage settings.

## Key features

- Discovery
  - Scans application directory for `SmartHopper.Providers.*.dll`.
  - Instantiates `IAIProviderFactory` implementations to create providers and settings.
- Supply-chain security
  - Authenticode certificate thumbprint must match host.
  - Strong-name public key token must match host.
  - First discovery prompts user to trust; decision persisted.
- Registration & initialization
  - Registers provider + settings; runs `InitializeProviderAsync()` in background.
- Accessors
  - `GetProviders(includeUntrusted=false)`
  - `GetProvider(name)` (handles "Default" indirection)
  - `GetProviderSettings(name)`, `GetProviderAssembly(name)`, `GetProviderIcon(name)`
  - `GetDefaultAIProvider()`
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
