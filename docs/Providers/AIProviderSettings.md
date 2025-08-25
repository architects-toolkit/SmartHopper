# AIProviderSettings (base)

Base class for building provider settings UI descriptors and validation.

- Source: `src/SmartHopper.Infrastructure/AIProviders/AIProviderSettings.cs`

## Purpose

Provide a convenient base that keeps a reference to the provider and centralizes descriptor/validation patterns.

## Members

- Ctor: `(IAIProvider provider)`
- Abstract: `IEnumerable<SettingDescriptor> GetSettingDescriptors()`
- Abstract: `bool ValidateSettings(Dictionary<string, object> settings)`
- Property: `bool EnableStreaming { get; }` — reads the provider's persisted `EnableStreaming` setting with fallback to the descriptor default.

## Relationships

- Implements `IAIProviderSettings`.
- Typically returned by a `IAIProviderFactory` implementation for the provider assembly.

## Notes

- Secret settings are handled via `SmartHopperSettings` encryption; descriptors mark secrets.
- Providers should expose an `EnableStreaming` boolean descriptor (typically default `true`). The base class will surface the effective value via this property.
