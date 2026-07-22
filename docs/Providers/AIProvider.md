# AIProvider (base class)

Abstract base class implementing `IAIProvider` with shared initialization, settings, and HTTP orchestration.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Providers/AIProvider.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

`AIProvider` is the heart of every SmartHopper AI integration. It defines the template-method pipeline that turns a SmartHopper request into an external API call and back again. If you are building or extending a provider, you will spend most of your time overriding methods from this class.

**You should read this if you:**

- Are implementing a new AI provider
- Need to understand how requests are encoded, sent, and decoded
- Want to customize model selection, settings handling, or HTTP behavior
- Are investigating provider initialization or metrics collection

---

## End-User Guide

### Purpose

Offer a template-method style pipeline for providers: register models, load settings, encode → call → decode, and record metrics.

### Highlights

- Initialization (`InitializeProviderAsync`)
  - Retrieves `Models.RetrieveModels()`.
  - Registers provider models and capabilities with `ModelManager` (single source of truth).
  - Loads default setting values from descriptors and merges with stored settings.
- Settings helpers
  - `GetSetting<T>(key)` with type conversion and recursion guard.
  - `SetSetting(key, value)` persists to `SmartHopperSettings` within provider scope.
  - `RefreshCachedSettings(settings)` merges external updates into the cached dictionary.
  - `IEnumerable<SettingDescriptor> GetSettingDescriptors()` via `ProviderManager.GetProviderSettings(Name)`.
- Default model resolution
  - `GetDefaultModel(requiredCapability, useSettings)` validates capability with `ModelManager`.
- Provider-scoped model selection
  - `SelectModel(requiredCapability, requestedModel)` resolves the concrete API-ready model.
  - Default implementation delegates to centralized `ModelManager.SelectBestModel` to keep policy consistent while hiding the singleton behind the provider interface. Providers may override.
- HTTP/API orchestration
  - `Call(request)` handles PreCall, validation, `CallApi`, metrics, PostCall.
  - `CallApi` supports GET, POST, DELETE, PATCH, Bearer auth, JSON content.
- Tools formatting
  - `GetFormattedTools(toolFilter)` converts registered AITools to function definitions for provider APIs.
  - Disabled tools (`AITool.Enabled == false`) are filtered out before the category filter is applied.
- Abstracts
  - `Encode(AIRequestCall)`, `Encode(IAIInteraction)`, `Decode(string)`, `DefaultServerUrl`.
- Generic singleton variant `AIProvider<T>`
  - Provides `Instance` for providers that prefer a static singleton access pattern.

### Extending

Override `Encode/Decode/PreCall/PostCall` and set `DefaultServerUrl`.
Use `SelectModel(...)` for model resolution in requests/components. Use `Models` to implement provider-specific retrieval/capabilities.

### Security & reliability

- Bearer token pulled from settings (`ApiKey`) with clear error if missing.
- Exceptions are wrapped with provider context for easier diagnostics.
- Metrics include provider name, model, completion time.

---

## Developer Reference

`AIProvider` is an abstract class. Concrete providers must override encoding and decoding, and can optionally override the call lifecycle hooks.

```csharp
public class OpenAIProvider : AIProvider
{
    public override string Name => "openai";
    protected override string DefaultServerUrl => "<https://api.openai.com/v1";>

    protected override object Encode(AIRequestCall request)
    {
        return new
        {
            model = request.Model,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            tools = GetFormattedTools(request.ToolFilter)
        };
    }

    protected override AIReturn<string> Decode(string responseBody)
    {
        var json = JObject.Parse(responseBody);
        var content = json["choices"]?[0]?["message"]?["content"]?.ToString();
        return new AIReturn<string> { Result = content };
    }
}

```

```csharp
// Using the generic singleton variant
public class MySingletonProvider : AIProvider<MySingletonProvider>
{
    // Access via MySingletonProvider.Instance anywhere in the provider assembly
    public override string Name => "mysingleton";

    protected override async Task PreCall(AIRequestCall request)
    {
        // Add custom headers or logging before the API call
        await base.PreCall(request);
    }

    protected override async Task PostCall(AIRequestCall request, AIReturn<string> response)
    {
        // Log metrics or transform response after the API call
        await base.PostCall(request, response);
    }
}

```

---

## Architecture & Design

`AIProvider` uses the Template Method pattern to enforce a consistent request lifecycle across all providers while allowing provider-specific customization at each step.

The lifecycle is:

1. **PreCall** — provider-specific request preparation
2. **Encode** — convert `AIRequestCall` into provider-specific JSON/body
3. **CallApi** — HTTP call with Bearer auth, retry, and timeout handling
4. **PostCall** — provider-specific response cleanup or logging
5. **Decode** — convert raw response into `AIReturn<T>`

Settings are cached per-provider to avoid repeated deserialization, and `ModelManager` is used as the single source of truth so that model selection policy lives in one place rather than being duplicated in every provider.

The generic singleton variant (`AIProvider<T>`) is a convenience for providers that are naturally singletons, avoiding DI container complexity in plugin-loaded assemblies.
