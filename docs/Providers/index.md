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
  - `IAIProvider` â€” provider contract (name, icon, models, PreCall/Call/PostCall, settings)
  - `AIProvider` â€” base template method flow and HTTP orchestration
  - `AIProviderModels` â€” capability and default model registry integration
  - `ProviderManager` â€” discovery, trust, registration, settings persistence
- External providers: `SmartHopper.Providers.*` projects (e.g., OpenAI, DeepSeek, MistralAI, Gemini)

### Lifecycle

1. Discovery and trust â€” provider assemblies are scanned, verified (Authenticode + strong-name), and optionally trusted.
2. Initialization â€” provider registers its models and capabilities with the model registry.
3. Request flow â€” PreCall â†’ FormatRequestBody â†’ CallApi â†’ PostCall; responses normalized into `AIReturn<T>`.
4. Settings â€” descriptor-driven, validated and persisted via `ProviderManager` (secrets stored securely).

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

## Provider implementations (in order of implementation)

- MistralAI
- OpenAI
- DeepSeek
- Anthropic
- OpenRouter
- [Google Gemini](./Gemini.md)

## Developer Reference

Example usage:

`csharp
// Placeholder example
``r

`csharp
// Another placeholder example
``r


## Architecture & Design

Architecture and design notes for index.

```csharp
// Example code for Developer Reference
```

```csharp
// Additional example for Developer Reference
```