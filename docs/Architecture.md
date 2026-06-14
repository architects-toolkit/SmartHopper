# SmartHopper Architecture

A modular, secure AI layer integrated into Grasshopper 3D, connecting parametric design workflows with external AI providers.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `N/A` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This document is the single entry point for understanding how SmartHopper is structured. It covers the full data flow from user input to AI response, the provider plugin model, security controls, and extensibility guidelines.

**You should read this if you:**

- Want to understand how SmartHopper connects Grasshopper to AI providers
- Are building a new provider plugin or extending the system
- Need to audit the security model or trust chain
- Want to trace how a request flows through the system

---

## End-User Guide

### What Does SmartHopper Do?

SmartHopper adds AI capabilities to Grasshopper. You place components on the canvas, connect them to data sources, select an AI provider and model, and get AI-generated results -- all within your normal Grasshopper workflow.

### How Data Flows

```text
Your Data → Input Component → Output Component → Your Result

```

1. **You provide data** -- text prompts, images, files, web URLs, or audio.
2. **Input components** package your data into a unified `AIInputPayload` format.
3. **Output components** call the AI provider, then extract the result type you need (text, numbers, images, JSON, etc.).

### What Happens Behind the Scenes

When you press **Run** on an output component:

1. SmartHopper gathers context about your environment (Rhino version, current file, time).
2. The output component resolves the provider and model (set via its right-click menu or inputs).
3. It validates that the chosen model supports the requested operation.
4. It applies policies (timeout limits, tool validation, schema checks).
5. It sends the request to the AI provider's API.
6. It decodes the response, records performance metrics, and emits the typed result.

### Choosing a Provider

SmartHopper discovers provider plugins automatically. Each provider must be digitally signed for security. On first use, you will be prompted to trust each provider.

<!-- PLACEHOLDER: Screenshot showing provider selection dropdown on a component -->

---

## Developer Reference

### Solution Structure

| Project | Purpose |
| --- | --- |
| `SmartHopper.Core` | Core abstractions shared across components |
| `SmartHopper.Core.Grasshopper` | Grasshopper utilities and AI Tools |
| `SmartHopper.Components` | End-user Grasshopper components |
| `SmartHopper.Infrastructure` | Settings, Provider/Model managers, Context, AI call pipeline |
| `SmartHopper.Providers.*` | Provider plugins (external DLLs, loaded securely) |

- **Target platforms**: Rhino 8 (Grasshopper 1)
- **Frameworks**: .NET 7 (Windows/macOS)

### Provider Contract

Providers implement `IAIProvider` ([docs](./Providers/IAIProvider.md)) or derive from `AIProvider` ([docs](./Providers/AIProvider.md)):

```csharp
public interface IAIProvider
{
    string Name { get; }
    Bitmap Icon { get; }
    bool IsEnabled { get; }
    IReadOnlyList<AIProviderModels> Models { get; }

    Task InitializeProviderAsync();
    void RefreshCachedSettings();

    AIBody PreCall(AIBody body);
    Task<AIReturn> Call(AIBody body, CancellationToken ct);
    AIReturn PostCall(AIReturn result);
}

```

- **Discovery**: `ProviderManager` scans for `SmartHopper.Providers.*.dll` ([docs](./Providers/ProviderManager.md))
- **Models**: Registered via `AIModelCapabilities` in `ModelManager` ([docs](./Providers/AIModelCapabilities.md))
- **Capabilities**: Expressed as `AICapability` flags ([docs](./Providers/AICapability.md))

### Context Provider Contract

Context is injected into AI prompts via `IAIContextProvider` implementations. A minimal context provider looks like this:

```csharp
public class EnvironmentContextProvider : IAIContextProvider
{
    public string ProviderId => "environment";

    public Dictionary<string, string> GetContext()
    {
        return new Dictionary<string, string>
        {
            { "operating-system", Environment.OSVersion.ToString() },
            { "rhino-version", RhinoApp.Version.ToString() },
            { "platform", Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit" }
        };
    }
}

```

### Component Base Hierarchy

```text
GH_Component
├── AsyncComponentBase              -- async lifecycle
│   └── StatefulComponentBase       -- + state machine, persistence
│       ├── AIProviderComponentBase -- + provider selection
│       │   └── AIStatefulAsyncComponentBase (8 partial files)
│       │       ├── AISelectingStatefulAsyncComponentBase
│       │       └── AIOutputAdapterBase
│       └── SelectingStatefulComponentBase
├── ProviderComponentBase           -- non-async provider
├── SelectingComponentBase          -- non-async Select button
└── AIInputAdapterBase              -- sync input adapters

```

See [ComponentBase](./Components/ComponentBase/index.md) for full documentation.

### Context Providers

Context is injected into AI prompts via `IAIContextProvider` implementations:

| Provider | Source | Data |
| --- | --- | --- |
| `EnvironmentContextProvider` | `SmartHopper.Core` | OS, Rhino version, platform |
| `TimeContextProvider` | `SmartHopper.Core` | Current time and timezone |
| `FileContextProvider` | `SmartHopper.Core` | Current file, selection, object counts |

### Execution Pipeline

```text
1) Run triggered → 2) Provider + Model resolved → 3) Context aggregated
→ 4) PolicyPipeline: request policies → 5) Provider.PreCall()
→ 6) Provider.Call() (HTTP) → 7) Provider.PostCall()
→ 8) PolicyPipeline: response policies → 9) Metrics recorded → 10) Outputs emitted

```

### Extensibility

**Adding a new Provider:**

1. Implement `IAIProvider` or derive from `AIProvider` and register models/capabilities.
2. Sign the assembly (Authenticode + strong-name) and publish its hash for online validation.
3. Ship as `SmartHopper.Providers.MyProvider.dll` in the host folder.
4. On first run, user will be prompted to trust/enable.

**Adding a new Component:**

- Choose the appropriate base class from the hierarchy above.
- Override `RequiredCapability` for AI components.

**Adding a new Context Provider:**

- Implement `IAIContextProvider` and register with the context manager.

**Adding a new Tool:**

- Create a component in `src/SmartHopper.Core.Grasshopper/AITools/` following existing patterns.

---

## Architecture & Design

### High-Level Design

SmartHopper integrates with external AI providers through a secure plug-in model. Providers declare models and capabilities, which are centrally validated and resolved at runtime. Components offer UI for selecting providers/models, orchestrate context gathering, invoke AI calls, and surface metrics and results to the user.

### Security Model

**Provider loading security:**

- Authenticode signature verification (thumbprint match)
- Strong-name public key token match
- Online hash validation (assembly hash checked against a published registry)
- First-discovery trust prompt; persisted in settings
- Load paths restricted to expected directories

**Secrets management:**

- API keys handled by `SmartHopperSettings` with secure persistence
- No static secrets committed to source; per-user configuration

### Concurrency and Reliability

- Model registry lookups use exact model names and aliases only; `ModelManager.SelectBestModel` centralizes capability-aware selection and fallbacks.
- Default model resolution prefers concrete names to avoid API errors and uses `Verified`/`Deprecated`/`Rank` metadata as tie-breakers.
- Components maintain run state and metrics; the async bases ensure metrics are not cleared mid-processing in toggle scenarios.
- The AICall `PolicyPipeline` runs request/response policies (timeouts, tool validation, context injection, schema attach/validate, finish-reason normalization) for every call.
- Conversation sessions (`ConversationSession`) orchestrate multi-turn calls and tool loops on top of `AIRequestCall.Exec`, aggregating per-turn metrics for the UI.
- Providers should implement timeouts, retries with jitter, cancellation, and error classification.

### Design Decisions

Key architectural choices are documented in [Design Decisions](./DESIGN_DECISIONS/index.md):

- Composable Input/Output adapters over monolithic components
- Immutable records for AI request/response data
- Policy pipeline for cross-cutting concerns
- Capability flags for model selection
- Secure provider plugin loading
- Layered component base hierarchy

### Directory Map

```text
src/
├── SmartHopper.Core/
│   ├── ComponentBase/        -- Component hierarchy bases
│   ├── AIContext/             -- Context providers (Environment, Time)
│   ├── IO/                   -- File I/O and serialization
│   └── Types/                -- Core type definitions
├── SmartHopper.Core.Grasshopper/
│   └── AITools/              -- AI tools (gh_*, text_*, list_*, script_*, img_*, web_*)
├── SmartHopper.Components/
│   ├── Input/                -- Input adapter components
│   ├── Output/               -- Output adapter components
│   ├── Text/                 -- Legacy monolithic text components
│   ├── JSON/                 -- JSON components
│   ├── Img/                  -- Image components
│   ├── Knowledge/            -- File/web/forum knowledge extraction
│   ├── Grasshopper/          -- GH definition manipulation
│   ├── Audio/                -- Audio playback/visualization
│   ├── Modifiers/            -- Script and parameter modifiers
│   └── Misc/                 -- Metrics deconstruction
├── SmartHopper.Infrastructure/
│   ├── AIProviders/          -- Provider manager, contract, base
│   ├── AIModels/             -- Model manager, capabilities, registry
│   ├── AIContext/            -- Context manager, bootstrapper
│   ├── AICall/               -- AI call pipeline, policies, tools
│   │   ├── Core/             -- AIBody, AIRequest, AIReturn, AIMetrics
│   │   └── Tools/            -- Tool result envelope
│   ├── Diagnostics/          -- Logging and tracing
│   └── Streaming/            -- Streaming response handling
└── SmartHopper.Providers.*/  -- External provider plugins

```

### Related Documentation

- [Design Decisions](./DESIGN_DECISIONS/index.md) -- rationale behind key choices
- [AI Call Pipeline](./Providers/AICall/index.md) -- request/response processing
- [ComponentBase](./Components/ComponentBase/index.md) -- component hierarchy
- [Provider System](./Providers/index.md) -- provider management
- [Context System](./Context/index.md) -- context providers
