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
- External providers: `SmartHopper.Providers.*` projects (e.g., OpenAI, DeepSeek, MistralAI, Gemini)

## Lifecycle

1. Discovery and trust — provider assemblies are scanned, verified (Authenticode + strong-name), and optionally trusted.
2. Initialization — provider registers its models and capabilities with the model registry.
3. Request flow — PreCall → FormatRequestBody → CallApi → PostCall; responses normalized into `AIReturn<T>`.
4. Settings — descriptor-driven, validated and persisted via `ProviderManager` (secrets stored securely).

## Security

- **Cryptographic classification** — providers are classified as `Official`, `OfficialTampered`, `Community`, or `Invalid` based purely on strong-name token, Authenticode signature (Windows), and SHA-256 manifest. Names and provider ids never affect classification.
- **Trust gates** — community providers are blocked unless `AllowCommunityProviders=true`. The global `BlockNonOfficialProviders=true` switch overrides everything to allow Official providers only.
- **Per-provider trust** — first-time discovery of an allowed community provider triggers a trust prompt. Trust is invalidated automatically if the file's SHA-256 changes.
- **Visible warnings** — every AI component using a community/unsigned/unverified provider receives a runtime warning message in Grasshopper.
- **Secrets** — stored using OS secure mechanisms (DPAPI on Windows, Keychain on macOS); no hardcoded keys. Provider code is scoped to its own keys via `IProviderSettingsStore`.

## Extensibility

- Implement `IAIProvider` and a factory to expose your provider.
- Register model capabilities and defaults in `InitializeProviderAsync`.
- Keep provider-specific schema wrapping/unwrapping and tool-call formatting in the provider implementation.

## Detailed docs

- [Provider SDK (community-facing)](./ProviderSdk.md)
- [ProviderManager](./ProviderManager.md)
- [IAIProvider](./IAIProvider.md)
- [AIProvider](./AIProvider.md)
- [Authentication](./Authentication.md)
- [AIProviderModels](./AIProviderModels.md)
- [IAIProviderFactory](./IAIProviderFactory.md)
- [IAIProviderSettings](./IAIProviderSettings.md)
- [AIProviderSettings](./AIProviderSettings.md)
- [AICall](./AICall/index.md)
- [AIModelCapabilities](./AIModelCapabilities.md)
- [Model Selection Policy](./ModelSelection.md)
- [Streaming Adapters](./AICall/Streaming.md)

## Provider implementations (in order of implementation)

- MistralAI
- OpenAI
- DeepSeek
- Anthropic
- OpenRouter
- [Google Gemini](./Gemini.md)
