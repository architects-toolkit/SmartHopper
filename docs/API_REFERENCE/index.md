# API Reference

This section provides developer-focused documentation for SmartHopper's public APIs, base classes, and extension points.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/` (SmartHopper.Core, SmartHopper.Components, SmartHopper.Infrastructure, SmartHopper.Providers.*) |
| **Since Version** | ? |
| **Last Updated** | 2026-06-13 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

- You are a **Grasshopper user** wanting to understand how SmartHopper's AI components work internally
- You are a **developer** looking to extend SmartHopper with custom components, providers, or AI tools
- You want to understand the **architecture and design patterns** used in SmartHopper
- You need to integrate your own AI provider or create custom AI-powered components
- You want to contribute to the SmartHopper codebase

---

## End-User Guide

This API Reference is primarily intended for developers. For end-user documentation, please refer to:

- [Components Documentation](../Components/index.md) - Learn about individual Grasshopper components
- [Getting Started Guide](../GETTING_STARTED/index.md) - Installation and basic usage
- [Tools Documentation](../Tools/index.md) - Available AI tools and their usage

If you are an end-user encountering issues with SmartHopper components, please check the component-specific documentation or file an issue on GitHub.

---

## Developer Reference

### Example 1: Creating a Simple AI Component

```csharp
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Models;
using SmartHopper.Providers;

public class MyAIComponent : AIStatefulAsyncComponentBase
{
    public override Guid ComponentGuid => new Guid("YOUR-GUID-HERE");
    protected override AICapability RequiredCapability => AICapability.TextGeneration;

    protected override async Task<AIResponse> ProcessAIAsync(
        AIInputPayload input,
        AIRequestParameters parameters,
        CancellationToken ct)
    {
        // Get the selected provider
        var provider = ProviderManager.Instance.GetProvider(SelectedProviderId);

        // Build the request
        var request = new AIRequestCall(provider, parameters);

        // Execute the AI call
        var response = await request.ExecAsync(input, ct);

        return response;
    }
}

```

### Example 2: Implementing a Custom Context Provider

```csharp
using SmartHopper.Core.Context;

public class MyCustomContextProvider : IAIContextProvider
{
    public string ProviderName => "MyCustomContext";

    public Task<Dictionary<string, object>> GetContextAsync(CancellationToken ct)
    {
        var context = new Dictionary<string, object>
        {
            { "custom_metric", GetCustomMetric() },
            { "custom_state", GetCustomState() }
        };

        return Task.FromResult(context);
    }
}

// Register in AIContextBootstrapper
AIContextManager.RegisterProvider(new MyCustomContextProvider());

```

---

## Architecture & Design

SmartHopper follows a layered architecture designed for extensibility and maintainability:

### Core Principles

1. **Modularity**: Each layer (Core, Components, Infrastructure, Providers) has clear responsibilities
2. **Async-First**: All AI operations are fully asynchronous with proper cancellation support
3. **Provider Abstraction**: Support for multiple AI providers through a common interface
4. **Component Hierarchy**: Layered base classes allow incremental opt-in to capabilities
5. **State Management**: Built-in state persistence and debounce mechanisms
6. **Context-Aware**: Automatic collection of Rhino/Grasshopper context for AI prompts

### Layer Responsibilities

| Layer | Responsibility |
| --- | --- |
| **SmartHopper.Core** | Abstractions, base classes, types, and interfaces |
| **SmartHopper.Core.Grasshopper** | Grasshopper-specific utilities and AI Tools |
| **SmartHopper.Components** | End-user Grasshopper components |
| **SmartHopper.Infrastructure** | Provider management, AI call pipeline, context system |
| **SmartHopper.Providers.*** | External provider plugins (OpenAI, Anthropic, etc.) |

### Extension Points

The architecture provides clear extension points for:

- **New Providers**: Implement `IAIProvider` or derive from `AIProvider`
- **New Components**: Choose from the component base class hierarchy
- **New Tools**: Implement `IAIToolProvider` in `SmartHopper.Core.Grasshopper`
- **New Context**: Implement `IAIContextProvider` for custom context injection

For the full extensibility guidelines, see [Architecture](../Architecture.md).

---

## Solution Structure

```text
src/
├── SmartHopper.Core/              -- Core abstractions (component bases, state, types)
├── SmartHopper.Core.Grasshopper/  -- Grasshopper utilities and AI Tools
├── SmartHopper.Components/        -- End-user Grasshopper components
├── SmartHopper.Infrastructure/    -- Providers, models, context, AI call pipeline
└── SmartHopper.Providers.*/       -- Provider plugins (external DLLs)

```

## Component Base Classes

The component hierarchy lets you opt into specific capabilities layer by layer.

| Base Class | Purpose | Docs |
| --- | --- | --- |
| `AsyncComponentBase` | Async lifecycle (workers, tasks, cancellation) | [docs](../Components/ComponentBase/AsyncComponentBase.md) |
| `StatefulComponentBase` | State machine, persistence, debounce | [docs](../Components/ComponentBase/StatefulComponentBase.md) |
| `AIProviderComponentBase` | AI provider selection menu and persistence | [docs](../Components/ComponentBase/AIProviderComponentBase.md) |
| `AIStatefulAsyncComponentBase` | Core AI component base (8 partial files) | [docs](../Components/ComponentBase/AIStatefulAsyncComponentBase.md) |
| `AIInputAdapterBase` | Sync adapters producing `AIInputPayload` | [docs](../Components/ComponentBase/AIInputAdapterBase.md) |
| `AIOutputAdapterBase` | AI components consuming `AIInputPayload` | [docs](../Components/ComponentBase/AIOutputAdapterBase.md) |

See [ComponentBase overview](../Components/ComponentBase/index.md) for the full hierarchy and design criteria.

## Provider System

| Type | Purpose | Docs |
| --- | --- | --- |
| `IAIProvider` | Provider contract (lifecycle, call pipeline, encoding) | [docs](../Providers/IAIProvider.md) |
| `AIProvider` | Shared base (HTTP, auth, settings, model registration) | [docs](../Providers/AIProvider.md) |
| `ProviderManager` | Discovery, trust verification, initialization | [docs](../Providers/ProviderManager.md) |
| `AICapability` | Flags enum for model capabilities | [docs](../Providers/AICapability.md) |
| `ProviderManager` | Capability registry, model selection, defaults | [docs](../Providers/ProviderManager.md) |

## AI Call Pipeline

| Type | Purpose | Docs |
| --- | --- | --- |
| `AIBody` / `AIBodyBuilder` | Immutable request body construction | [docs](../Providers/AICall/body-metrics-status.md) |
| `AIRequestCall` | Single-turn request execution | [docs](../Providers/AICall/requests.md) |
| `ConversationSession` | Multi-turn orchestration with tool loops | [docs](../Providers/AICall/requests.md) |
| `PolicyPipeline` | Request/response policy chain | [docs](../Providers/AICall/policy-pipeline.md) |
| `AIInteraction` | Message abstraction (text, image, audio) | [docs](../Providers/AICall/interactions.md) |
| `SHRuntimeMessage` | Structured diagnostics | [docs](../Providers/AICall/messages.md) |

## Context System

| Type | Purpose | Docs |
| --- | --- | --- |
| `IAIContextProvider` | Contract for context providers | [docs](../Context/index.md) |
| `AIContextManager` | Registration and aggregation | [docs](../Context/index.md) |
| `EnvironmentContextProvider` | OS, Rhino version, platform | [docs](../Context/index.md) |
| `TimeContextProvider` | Current time and timezone | [docs](../Context/index.md) |
| `FileContextProvider` | Current file, selection, object counts | [docs](../Context/index.md) |

## Core Types

| Type | Purpose | Docs |
| --- | --- | --- |
| `AIInputPayload` | Unified input format for AI operations | [docs](../Architecture/AIInputPayload.md) |
| `AIRequestParameters` | Per-request customization (temperature, tokens) | [docs](../Architecture/AIRequestParameters.md) |
| `VersatileAudio` | Audio type system (file, URL, base64) | [docs](../Architecture/VersatileAudio.md) |

## Extension Guide

### Adding a New Provider

1. Implement `IAIProvider` or derive from `AIProvider`.
2. Register models and capabilities during `InitializeProviderAsync()`.
3. Sign the assembly (Authenticode + strong-name).
4. Ship as `SmartHopper.Providers.YourProvider.dll`.

### Adding a New Component

1. Choose the appropriate base class from the hierarchy above.
2. Override `RequiredCapability` for AI components.
3. Follow the patterns in existing components under `src/SmartHopper.Components/`.

### Adding a New AI Tool

AI tools are callable operations the AI can invoke via function/tool calling. They live in `src/SmartHopper.Core.Grasshopper/AITools/`.

1. Create a class implementing `IAIToolProvider`.
2. Define fields for `toolName`, `toolCapabilityRequirements` (`AICapability` flags), `systemPrompt`, and `userPrompt`.
3. Implement `GetTools()` returning one or more `AITool` instances with name, description, category, JSON parameter schema, execute handler, required capabilities, and a `buildRequest` delegate.
4. Implement the `buildRequest` delegate to construct an `AIRequestCall` from the tool call parameters (used for batch aggregation).
5. Implement the execute handler (`async Task<AIReturn>`) that builds the request, calls `request.Exec()`, parses the response, and returns an `AIReturn` with success/error.
6. Follow naming conventions: `gh_*`, `text_*`, `list_*`, `img_*`, `web_*`, `script_*`.
7. Attach `ToolResultEnvelope` metadata to the result's root `__envelope` key.

See [Tools](../Tools/index.md) for the full tool catalog and [ToolResultEnvelope](../Tools/ToolResultEnvelope.md) for the envelope convention.

### Adding a New Context Provider

1. Implement `IAIContextProvider`.
2. Register with the context manager in `AIContextBootstrapper`.

See [Architecture](../Architecture.md) for full extensibility guidelines.
