# AIProviderSettings (base)

Base class for building provider settings UI descriptors and validation.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Providers/AIProviderSettings.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Every provider needs settings: API keys, server URLs, streaming toggles, and more. `AIProviderSettings` gives you a typed, descriptor-driven way to expose those settings to the SmartHopper UI without writing custom panels.

**You should read this if you:**

- Are adding configurable settings to a new provider
- Need to validate provider settings before they are persisted
- Want to understand how streaming and other toggles are exposed to users

---

## End-User Guide

### Purpose

Provide a convenient base that keeps a reference to the provider and centralizes descriptor/validation patterns.

### Members

- Ctor: `(IAIProvider provider)`
- Abstract: `IEnumerable<SettingDescriptor> GetSettingDescriptors()`
- Abstract: `bool ValidateSettings(Dictionary<string, object> settings)`
- Property: `bool EnableStreaming { get; }` — reads the provider's persisted `EnableStreaming` setting with fallback to the descriptor default.

### Relationships

- Implements `IAIProviderSettings`.
- Typically returned by a `IAIProviderFactory` implementation for the provider assembly.

### Notes

- Secret settings are handled via `SmartHopperSettings` encryption; descriptors mark secrets.
- Providers should expose an `EnableStreaming` boolean descriptor (typically default `true`). The base class will surface the effective value via this property.

---

## Developer Reference

Inherit from `AIProviderSettings` and override `GetSettingDescriptors` to expose provider-specific configuration.

```csharp
public class OpenAIProviderSettings : AIProviderSettings
{
    public OpenAIProviderSettings(IAIProvider provider) : base(provider) { }

    public override IEnumerable<SettingDescriptor> GetSettingDescriptors()
    {
        return new List<SettingDescriptor>
        {
            new SettingDescriptor
            {
                Key = "ApiKey",
                DisplayName = "API Key",
                Description = "Your OpenAI API key",
                Type = SettingType.Secret,
                IsRequired = true
            },
            new SettingDescriptor
            {
                Key = "ServerUrl",
                DisplayName = "Server URL",
                Description = "Custom API endpoint (optional)",
                Type = SettingType.String,
                DefaultValue = "<https://api.openai.com/v1",>
                IsRequired = false
            },
            new SettingDescriptor
            {
                Key = "EnableStreaming",
                DisplayName = "Enable Streaming",
                Description = "Stream responses in real time",
                Type = SettingType.Boolean,
                DefaultValue = true,
                IsRequired = false
            }
        };
    }

    public override bool ValidateSettings(Dictionary<string, object> settings)
    {
        if (!settings.ContainsKey("ApiKey") || string.IsNullOrWhiteSpace(settings["ApiKey"]?.ToString()))
        {
            return false;
        }

        if (settings.TryGetValue("ServerUrl", out var url) &&
            !string.IsNullOrWhiteSpace(url?.ToString()) &&
            !Uri.TryCreate(url.ToString(), UriKind.Absolute, out _))
        {
            return false;
        }

        return true;
    }
}

```

```csharp
// Factory returning the settings implementation
public class OpenAIProviderFactory : IAIProviderFactory
{
    public IAIProvider CreateProvider() => new OpenAIProvider();

    public IAIProviderSettings CreateSettings(IAIProvider provider)
    {
        return new OpenAIProviderSettings(provider);
    }
}

```

---

## Architecture & Design

`AIProviderSettings` uses the Descriptor pattern to separate "what settings exist and how should they behave" from "how they are displayed and stored." Each descriptor carries metadata: type, default, whether it is required, and whether it is a secret.

This design means:

- The UI can render settings generically for any provider without knowing the provider's internals.
- Secrets are marked at the descriptor level so `SmartHopperSettings` knows to encrypt them.
- Validation lives next to the descriptors, keeping provider-specific rules in the provider assembly.
- The `EnableStreaming` property is promoted to the base class because streaming is a cross-cutting concern used by multiple UI components and the call pipeline.
