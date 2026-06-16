# Providers

Overview of the provider architecture that connects SmartHopper to external AI services.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Providers/` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Providers implement API-specific logic while conforming to a common contract so components and tools can work provider-agnostically. Understanding the provider architecture is essential for extending SmartHopper with new AI services or troubleshooting integration issues.

**You should read this if you:**

- Want to add a new AI provider to SmartHopper
- Need to understand how provider discovery and trust works
- Are troubleshooting model selection or API call issues
- Want to learn about the lifecycle of a provider from discovery to request handling

---

## End-User Guide

### Purpose

Providers implement API-specific logic while conforming to a common contract so components and tools can work provider-agnostically.

### Key locations

- `src/SmartHopper.Infrastructure/AIProviders/`
  - `IAIProvider` — provider contract (name, icon, models, PreCall/Call/PostCall, settings)
  - `AIProvider` — base template method flow and HTTP orchestration
  - `AIProviderModels` — capability and default model registry integration
  - `ProviderManager` — discovery, trust, registration, settings persistence
- External providers: `SmartHopper.Providers.*` projects (e.g., OpenAI, DeepSeek, MistralAI, Gemini)

### Lifecycle

1. Discovery and trust — provider assemblies are scanned, verified (Authenticode + strong-name), and optionally trusted.
2. Initialization — provider registers its models and capabilities with the model registry.
3. Request flow — PreCall → FormatRequestBody → CallApi → PostCall; responses normalized into `AIReturn<T>`.
4. Settings — descriptor-driven, validated and persisted via `ProviderManager` (secrets stored securely).

### Security

- Signature verification before load, trusted providers tracked in settings.
- Secrets stored using OS secure mechanisms; no hardcoded keys.

### Extensibility

- Implement `IAIProvider` and a factory to expose your provider.
- Register model capabilities and defaults in `InitializeProviderAsync`.
- Keep provider-specific schema wrapping/unwrapping and tool-call formatting in the provider implementation.

### Detailed docs

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
- [Prompt Caching](./PromptCaching.md)
- [Streaming Adapters](./AICall/Streaming.md)

### Provider implementations (in order of implementation)

- [MistralAI](./MistralAI.md)
- [OpenAI](./OpenAI.md)
- [DeepSeek](./DeepSeek.md)
- [Anthropic](./Anthropic.md)
- [OpenRouter](./OpenRouter.md)
- [Google Gemini](./Gemini.md)

---

## Developer Reference

The provider system uses a template-method pattern where `AIProvider` defines the orchestration and concrete providers fill in encoding, decoding, and API details.

```csharp
// Implementing a custom provider
public class MyCustomProvider : AIProvider
{
    public override string Name => "mycustom";
    public override string IconUrl => "Resources/mycustom.png";

    public override async Task InitializeProviderAsync()
    {
        // Register models and capabilities
        var models = await Models.RetrieveModels();
        foreach (var model in models)
        {
            ModelManager.RegisterCapabilities(model);
        }
        await base.InitializeProviderAsync();
    }

    protected override object Encode(AIRequestCall request)
    {
        // Convert SmartHopper request to provider-specific format
        return new { model = request.Model, messages = request.Messages };
    }

    protected override AIReturn<string> Decode(string responseBody)
    {
        // Parse provider response into SmartHopper format
        return new AIReturn<string> { Result = responseBody };
    }
}

```

```csharp
// Factory registration for discovery
public class MyCustomProviderFactory : IAIProviderFactory
{
    public IAIProvider CreateProvider()
    {
        return new MyCustomProvider();
    }
}

```

---

## Architecture & Design

The provider architecture is designed around separation of concerns:

- **Contracts (`IAIProvider`)** define what every provider must expose: name, icon, model lists, settings, and the call pipeline.
- **Base class (`AIProvider`)** implements shared concerns: HTTP orchestration, settings caching, model resolution via `ModelManager`, metrics recording, and the PreCall/Call/PostCall lifecycle.
- **Model metadata (`AIProviderModels`)** lets each provider declare what models it supports, their capabilities, and which model is default for each capability.
- **Settings (`AIProviderSettings`)** provide descriptor-driven UI and validation so each provider can expose its own configuration without building custom UI.
- **Discovery (`ProviderManager`)** scans for provider assemblies, verifies signatures, and registers trusted providers so the system remains secure even with third-party extensions.

This architecture means adding a new AI service typically requires only: a provider class inheriting `AIProvider`, a models class inheriting `AIProviderModels`, a settings class inheriting `AIProviderSettings`, and a factory implementing `IAIProviderFactory`.
