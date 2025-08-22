# IAIProvider

Defines the provider contract for integrating external AI services.

- Source: `src/SmartHopper.Infrastructure/AIProviders/IAIProvider.cs`

## Purpose

Provide a stable interface so components and tools can call any provider in a provider-agnostic way.

## Key members

- Properties
  - `Name: string`
  - `Icon: Image` (16x16 recommended)
  - `IsEnabled: bool`
  - `Models: IAIProviderModels`
- Lifecycle
  - `Task InitializeProviderAsync()`
- Encoding/decoding
  - `string Encode(AIRequestCall request)`
  - `string Encode(IAIInteraction interaction)`
  - `string Encode(List<IAIInteraction> interactions)`
  - `List<IAIInteraction> Decode(string response)`
- Call pipeline
  - `AIRequestCall PreCall(AIRequestCall request)`
  - `Task<IAIReturn> Call(AIRequestCall request)`
  - `IAIReturn PostCall(IAIReturn response)`
- Model selection
  - `string GetDefaultModel(AICapability requiredCapability = AICapability.Text2Text, bool useSettings = true)`
- Settings
  - `void RefreshCachedSettings(Dictionary<string, object> settings)`
  - `IEnumerable<SettingDescriptor> GetSettingDescriptors()`

## Usage

- Implement providers by either implementing this interface or deriving from `AIProvider`.
- Use `Models` to resolve the model from a user request or defaults.
- Implement `Encode/Decode` to translate between SmartHopper models and the provider API schema.
- Use `PreCall/Call/PostCall` to customize end-to-end request handling.

## Relationships

- Base implementation: `AIProvider` — common initialization, settings, HTTP, metrics.
- Model abstraction: `IAIProviderModels` — capability and default model retrieval.
- Settings: `IAIProviderSettings` and `AIProviderSettings` — descriptors + validation.
- Manager: `ProviderManager` — discovery, trust, and registration.

## Security notes

- Trust and signature verification are enforced by `ProviderManager` during discovery.
- Secrets are persisted via `SmartHopperSettings` and surfaced to providers through descriptors.
