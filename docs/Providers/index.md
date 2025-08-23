# Providers

Overview of the provider architecture that connects SmartHopper to external AI services.

## Purpose

Providers implement API-specific logic while conforming to a common contract so components and tools can work provider-agnostically.

## Key locations

- `src/SmartHopper.Infrastructure/AIProviders/`
  - `IAIProvider` — provider contract (name, icon, models, PreCall/Call/PostCall, settings)
  - `AIProvider` — base template method flow and HTTP orchestration
  - `AIProviderModels` — capability and default model registry integration
  - `ProviderManager` — discovery, trust, registration, settings persistence
- External providers: `SmartHopper.Providers.*` projects (e.g., OpenAI, DeepSeek, MistralAI)

## Lifecycle

1. Discovery and trust — provider assemblies are scanned, verified (Authenticode + strong-name), and optionally trusted.
2. Initialization — provider registers its models and capabilities with the model registry.
3. Request flow — PreCall → FormatRequestBody → CallApi → PostCall; responses normalized into `AIReturn<T>`.
4. Settings — descriptor-driven, validated and persisted via `ProviderManager` (secrets stored securely).

## Security

- Signature verification before load, trusted providers tracked in settings.
- Secrets stored using OS secure mechanisms; no hardcoded keys.

## Extensibility

- Implement `IAIProvider` and a factory to expose your provider.
- Register model capabilities and defaults in `InitializeProviderAsync`.
- Keep provider-specific schema wrapping/unwrapping and tool-call formatting in the provider implementation.

## Detailed docs

- [ProviderManager](./ProviderManager.md)
- [IAIProvider](./IAIProvider.md)
- [AIProvider](./AIProvider.md)
- [AIProviderModels](./AIProviderModels.md)
- [IAIProviderFactory](./IAIProviderFactory.md)
- [IAIProviderSettings](./IAIProviderSettings.md)
- [AIProviderSettings](./AIProviderSettings.md)
- [AICall](./AICall/index.md)
- [AIModelCapabilities](./AIModelCapabilities.md)
- [Model Selection Policy](./ModelSelection.md)
