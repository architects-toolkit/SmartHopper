# IAIProviderSettings

Interface for provider settings descriptors and validation.

- Source: `src/SmartHopper.Infrastructure/AIProviders/IAIProviderSettings.cs`

## Purpose

Declare settings schema for a provider and validate user input before persistence.

## Members

- `IEnumerable<SettingDescriptor> GetSettingDescriptors()` — name, description, default, secret flag, allowed values, etc.
- `bool ValidateSettings(Dictionary<string, object> settings)` — enforce required/allowed values and sanitization.
- `bool EnableStreaming { get; }` — provider-level toggle to allow/disable streaming responses.

## Streaming toggle

- Implementations should expose an `EnableStreaming` setting descriptor (type `bool`).
- The base `AIProviderSettings` reads the persisted setting value; providers typically default this to `true` via their descriptors.

## Relationships

- Used by `ProviderManager.UpdateProviderSettings` to validate updates and persist via `SmartHopperSettings`.
- Consumed by `AIProvider.GetSettingDescriptors()` to surface UI.
