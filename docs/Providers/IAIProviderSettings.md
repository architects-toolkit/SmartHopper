# IAIProviderSettings

Interface for provider settings descriptors and validation.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Providers/IAIProviderSettings.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Understanding `IAIProviderSettings` is essential when you need to declare the settings schema for a custom AI provider or validate user input before it is persisted. This interface defines the contract between SmartHopper's configuration system and individual provider settings.

**You should read this if you:**

- Want to create a custom AI provider plugin for SmartHopper
- Need to understand how provider settings are described and validated
- Are extending or modifying an existing provider's configuration options

---

## End-User Guide

As an end user, you don't interact directly with `IAIProviderSettings`. This is a developer-facing interface used to define and validate the settings that appear in SmartHopper's provider configuration UI.

The interface serves to:

- Declare the settings schema for a provider
- Validate user input before persistence

### Streaming Toggle

Implementations should expose an `EnableStreaming` setting descriptor (type `bool`). The base `AIProviderSettings` reads the persisted setting value; providers typically default this to `true` via their descriptors.

---

## Developer Reference

### Interface Members

- `IEnumerable<SettingDescriptor> GetSettingDescriptors()` — name, description, default, secret flag, allowed values, etc.
- `bool ValidateSettings(Dictionary<string, object> settings)` — enforce required/allowed values and sanitization.
- `bool EnableStreaming { get; }` — provider-level toggle to allow/disable streaming responses.

### Example: Defining Provider Settings

```csharp
public class MyProviderSettings : IAIProviderSettings
{
    public bool EnableStreaming => true;

    public IEnumerable<SettingDescriptor> GetSettingDescriptors()
    {
        return new[]
        {
            new SettingDescriptor("ApiKey", "API Key", typeof(string), secret: true),
            new SettingDescriptor("Model", "Model", typeof(string), defaultValue: "default-model"),
            new SettingDescriptor("EnableStreaming", "Enable Streaming", typeof(bool), defaultValue: true)
        };
    }

    public bool ValidateSettings(Dictionary<string, object> settings)
    {
        if (!settings.ContainsKey("ApiKey") || string.IsNullOrWhiteSpace(settings["ApiKey"]?.ToString()))
        {
            return false;
        }
        return true;
    }
}

```

### Example: Validating Streaming Settings

```csharp
public class StreamingSettingsValidator : IAIProviderSettings
{
    public bool EnableStreaming { get; private set; }

    public IEnumerable<SettingDescriptor> GetSettingDescriptors()
    {
        return new[]
        {
            new SettingDescriptor("EnableStreaming", "Enable Streaming", typeof(bool), defaultValue: true)
        };
    }

    public bool ValidateSettings(Dictionary<string, object> settings)
    {
        if (settings.TryGetValue("EnableStreaming", out var value) && value is bool streaming)
        {
            EnableStreaming = streaming;
            return true;
        }
        EnableStreaming = true;
        return true;
    }
}

```

---

## Architecture & Design

### Relationships

- Used by `ProviderManager.UpdateProviderSettings` to validate updates and persist via `SmartHopperSettings`.
- Consumed by `AIProvider.GetSettingDescriptors()` to surface UI.
