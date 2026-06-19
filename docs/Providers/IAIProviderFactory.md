# IAIProviderFactory

Factory interface discovered inside external provider assemblies.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/Providers/IAIProviderFactory.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

Understanding `IAIProviderFactory` is essential if you want to create a custom AI provider plugin for SmartHopper or understand how external provider assemblies are discovered and loaded. This interface serves as the contract between SmartHopper's core infrastructure and external provider implementations, enabling a plugin-based architecture.

**You should read this if you:**

- Want to create a custom AI provider plugin for SmartHopper
- Need to understand how external provider assemblies are discovered and loaded
- Plan to extend SmartHopper with support for new AI services

---

## End-User Guide

As an end user, you don't interact directly with `IAIProviderFactory`. This is a developer-facing interface used internally when SmartHopper discovers and loads provider plugins.

When you install a new provider plugin:

1. SmartHopper scans for assemblies matching `SmartHopper.Providers.*.dll`
2. The `ProviderManager` discovers implementations of `IAIProviderFactory`
3. The factory creates the provider instance and its settings UI
4. The provider becomes available in SmartHopper's settings and AI components

---

## Developer Reference

### Interface Definition

```csharp
/// <summary>
/// Factory interface implemented by external provider assemblies.
/// Used by ProviderManager to instantiate providers and their settings.
/// </summary>
public interface IAIProviderFactory
{
    /// <summary>
    /// Creates the provider instance.
    /// </summary>
    IAIProvider CreateProvider();

    /// <summary>
    /// Creates the provider settings descriptor and validator.
    /// </summary>
    IAIProviderSettings CreateProviderSettings();
}

```

### Example: Implementing a Custom Provider Factory

```csharp
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Providers.MyProvider
{
    /// <summary>
    /// Factory for creating MyCustomProvider instances.
    /// This class is discovered by ProviderManager at runtime.
    /// </summary>
    public class MyProviderFactory : IAIProviderFactory
    {
        public IAIProvider CreateProvider()
        {
            return new MyCustomProvider();
        }

        public IAIProviderSettings CreateProviderSettings()
        {
            return new MyProviderSettings();
        }
    }
}

```

### Example: Factory with Provider Metadata

```csharp
using SmartHopper.Infrastructure.AIProviders;
using System.ComponentModel.Composition;

namespace SmartHopper.Providers.AdvancedProvider
{
    /// <summary>
    /// Advanced provider factory with initialization parameters.
    /// </summary>
    public class AdvancedProviderFactory : IAIProviderFactory
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        public AdvancedProviderFactory(ILogger logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public IAIProvider CreateProvider()
        {
            var provider = new AdvancedProvider(_logger);
            
            // Apply default configuration
            provider.Configure(_config.GetSection("AI:Advanced"));
            
            return provider;
        }

        public IAIProviderSettings CreateProviderSettings()
        {
            return new AdvancedProviderSettings
            {
                EnableStreaming = true,
                DefaultModel = "gpt-4o"
            };
        }
    }
}

```

### Factory Registration Requirements

To ensure your factory is discovered:

1. **Assembly Naming**: Use pattern `SmartHopper.Providers.{YourProvider}.dll`
2. **Public Visibility**: Factory class must be public
3. **Default Constructor**: Must have a parameterless constructor or MEF-compatible constructor
4. **Single Implementation**: Only one factory per assembly is typically discovered

---

## Architecture & Design

### Design Philosophy

The `IAIProviderFactory` follows the **Factory Method** design pattern, separating:

- **Object creation** (factory responsibility)
- **Object usage** (SmartHopper core responsibility)

### Runtime Discovery Flow

```

┌─────────────────┐
│  SmartHopper   │
│    Startup     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ ProviderManager │
│  ScanDirectory  │
└────────┬────────┘
         │
         ▼
┌─────────────────────────┐
│ Find *.Providers.*.dll │
└────────┬────────────────┘
         │
         ▼
┌─────────────────────────┐
│ Load Assembly & Search  │
│ for IAIProviderFactory │
└────────┬────────────────┘
         │
         ▼
┌─────────────────────────┐
│ Instantiate Factory     │
│ CreateProvider()        │
│ CreateProviderSettings()│
└────────┬────────────────┘
         │
         ▼
┌─────────────────────────┐
│ Register with           │
│ ProviderManager         │
└─────────────────────────┘

```

### Relationship with Other Components

| Component | Role | Interaction with Factory |
| --- | --- | --- |--------------------------|
| `ProviderManager` | Discovery orchestrator | Scans for and instantiates factories |
| `IAIProvider` | Provider interface | Created by factory's `CreateProvider()` |
| `IAIProviderSettings` | Settings interface | Created by factory's `CreateProviderSettings()` |
| `SmartHopperSettings` | Persistence layer | Stores settings validated by factory-created settings |

### Security Considerations

- Factories are only loaded from trusted assemblies (matching host Authenticode/thumbprint)
- First discovery prompts user for trust confirmation
- Factory instances are cached after initial creation

## See Also

- [IAIProviderSettings](IAIProviderSettings.md) - Settings interface for providers
- [ProviderManager](ProviderManager.md) - Discovery and management service
- [IAIProvider](AIProvider.md) - Base provider interface (if documented separately)
