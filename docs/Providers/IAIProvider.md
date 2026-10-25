# IAIProvider

Defines the provider contract for integrating external AI services.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Providers/IAIProvider.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

This document defines the core `IAIProvider` interface that all AI providers in SmartHopper must implement. Understanding this contract is essential for anyone building, extending, or debugging provider integrations.

**You should read this if you:**

- Are implementing a new AI provider for SmartHopper
- Want to understand the lifecycle and methods available on every provider
- Need to customize provider behavior through `PreCallAsync`, `Call`, or `PostCall`
- Are working with provider settings, model selection, or encoding/decoding logic

---

## End-User Guide

### Purpose

Provide a stable interface so components and tools can call any provider in a provider-agnostic way.

### Usage

- Implement providers by either implementing this interface or deriving from `AIProvider`.
- Use `SelectModel(requiredCapability, requestedModel)` to resolve the concrete, API-ready model for a request.
- Use `GetDefaultModel(...)` only for fallback/default display purposes.
- Implement `Encode/Decode` to translate between SmartHopper models and the provider API schema.
- Use `PreCallAsync/Call/PostCall` to customize end-to-end request handling.

---

## Developer Reference

### Key Members

- Properties
  - `Name: string`
  - `Icon: Image` (16x16 recommended)
  - `IsEnabled: bool`
  - `AuthenticationErrorMessage: string?` — optional plain-text authentication message supplied by provider code.
  - `Models: IAIProviderModels`
- Lifecycle
  - `Task InitializeProviderAsync()`
- Encoding/decoding
  - `string Encode(AIRequestCall request)`
  - `string Encode(IAIInteraction interaction)`
  - `string Encode(List<IAIInteraction> interactions)`
  - `List<IAIInteraction> Decode(string response)`
- Call pipeline
  - `Task<AIRequestCall> PreCallAsync(AIRequestCall request, CancellationToken cancellationToken = default)`
  - `Task<IAIReturn> Call(AIRequestCall request)`
  - `IAIReturn PostCall(IAIReturn response)`
- Model selection
  - `string GetDefaultModel(AICapability requiredCapability = AICapability.Text2Text, bool useSettings = true)`
  - `string SelectModel(AICapability requiredCapability, string requestedModel)`
- Settings
  - `void RefreshCachedSettings(Dictionary<string, object> settings)`
  - `IEnumerable<SettingDescriptor> GetSettingDescriptors()`

### Implementing a Custom Provider

Derive from `AIProvider` and override the required members:

```csharp
public class MyCustomProvider : AIProvider
{
    public override string Name => "MyCustom";

    public override async Task<AIRequestCall> PreCallAsync(
        AIRequestCall request,
        CancellationToken cancellationToken = default)
    {
        // Customize request before sending
        request.Authentication = "bearer";
        request = await base.PreCallAsync(request, cancellationToken).ConfigureAwait(false);
        return request;
    }

    public override async Task<IAIReturn> Call(AIRequestCall request)
    {
        // Implement the core API call
        var jsonPayload = Encode(request);
        var response = await CallApi(request, jsonPayload);
        return PostCall(response);
    }

    public override string Encode(AIRequestCall request)
    {
        // Serialize request to provider-specific JSON
        return JsonSerializer.Serialize(request);
    }

    public override List<IAIInteraction> Decode(string response)
    {
        // Deserialize provider response to SmartHopper interactions
        var result = JsonSerializer.Deserialize<ProviderResponse>(response);
        return ConvertToInteractions(result);
    }
}

```

### Model Selection Example

Resolve the appropriate model for a capability and optional model override:

```csharp
public async Task<string> ResolveModel(IAIProvider provider)
{
    // Get default model for text generation
    var defaultModel = provider.GetDefaultModel(AICapability.Text2Text);

    // Select a specific model if requested, falling back to default if unavailable
    var resolvedModel = provider.SelectModel(AICapability.Text2Text, "gemini-2.5-flash");

    return resolvedModel;
}

```

### Relationships

- Base implementation: `AIProvider` — common initialization, settings, HTTP, metrics.
- Model abstraction: `IAIProviderModels` — capability and default model retrieval.
- Settings: `IAIProviderSettings` and `AIProviderSettings` — descriptors + validation.
- Manager: `ProviderManager` — discovery, trust, and registration.

### Security Notes

- Trust and signature verification are enforced by `ProviderManager` during discovery.
- Secrets are persisted via `SmartHopperSettings` and surfaced to providers through descriptors.
- For HTTP 401, `AIProvider.ClassifyHttpError(..., IAIProvider)` uses `AuthenticationErrorMessage` instead of generic text and the server body when the message passes safety checks. HTTP 403 uses the existing provider-name classification. Providers implementing `IAIProvider` directly must provide this property.
- The message is trimmed and limited to 1–300 characters; control and Unicode format characters, URL markers (`://`, `www.`, `mailto:`, `javascript:`, `data:`), email addresses, and domain names are rejected. It is plain text, not clickable, and never includes or appends the server body.

---

## Architecture & Design

The `IAIProvider` interface sits at the center of SmartHopper's provider architecture. It defines a clear contract with three major phases:

1. **Initialization**: `InitializeProviderAsync()` sets up models, settings, and internal state.
2. **Request Lifecycle**: `PreCallAsync` → `Call` → `PostCall` forms a pipeline where each stage can inspect and modify requests/responses.
3. **Translation**: `Encode/Decode` methods bridge SmartHopper's generic interaction model with provider-specific API formats.

This design enables:

- **Provider Agnosticism**: Tools and components interact only with `IAIProvider`, never with provider-specific APIs directly.
- **Composability**: `PreCallAsync` and `PostCall` allow cross-cutting concerns (auth, logging, retries) to be layered without changing core call logic.
- **Testability**: The interface can be mocked for unit testing tools and components that depend on AI providers.
