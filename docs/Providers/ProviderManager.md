# ProviderManager

Discovers, verifies, registers, and initializes AI providers at runtime.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/AIProviders/ProviderManager.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

ProviderManager is the backbone of SmartHopper's extensible provider architecture. It handles everything from discovering new AI provider plugins to enforcing supply-chain security and persisting user settings.

**You should read this if you:**

- Are developing a new AI provider plugin for SmartHopper
- Need to understand how provider trust and security works
- Want to customize provider discovery or initialization behavior
- Are debugging why a provider is not appearing in the UI

---

## End-User Guide

### What Is This?

ProviderManager is an internal service that runs when SmartHopper starts. It scans for provider plugin DLLs, verifies their authenticity, loads them into memory, and makes them available to Grasshopper components. You typically do not interact with it directly, but it powers the provider dropdown menus you see in every AI component.

### When to Use It

- **Adding a new provider**: After installing a new provider plugin (e.g., `SmartHopper.Providers.Gemini.dll`), ProviderManager discovers it automatically on the next Rhino restart
- **Trusting a provider**: The first time an untrusted provider is discovered, a UI prompt asks you to confirm trust
- **Updating settings**: When you change provider settings in the SmartHopper preferences, ProviderManager validates and persists them

### Visual Guide

<!-- PLACEHOLDER: Screenshot of SmartHopper provider settings panel -->
<!-- - Location: Rhino â†’ Tools â†’ Options â†’ SmartHopper â†’ Providers -->
<!-- - Shows: list of discovered providers with trust status and settings buttons -->

### Common Questions

**Q: Why is my new provider not showing up?**
A: Ensure the DLL is named `SmartHopper.Providers.*.dll`, is in the same directory as SmartHopper, and has passed trust verification. Check the Rhino command line for discovery log messages.

**Q: What does "untrusted provider" mean?**
A: The provider DLL failed Authenticode or strong-name verification. Only trust providers from sources you verify.

**Q: Can I disable a provider without uninstalling it?**
A: Yes. Use the provider settings UI to disable it; ProviderManager will filter it from the available list.

---

## Developer Reference

### API Overview

```csharp
public sealed class ProviderManager : IDisposable
{
    public static ProviderManager Instance { get; }

    public void RefreshProviders();
    public IReadOnlyList<IAIProvider> GetProviders(bool includeUntrusted = false);
    public IAIProvider GetProvider(string name);
    public IAIProviderSettings GetProviderSettings(string name);
    public Assembly GetProviderAssembly(string name);
    public Image GetProviderIcon(string name);
    public IAIProvider GetDefaultAIProvider();
    public void UpdateProviderSettings(string name, Dictionary<string, object> settings);
}

```

### Key Types

| Type | Purpose |
| --- | --- |
| `ProviderManager` | Singleton service for provider discovery and lifecycle |
| `IAIProviderFactory` | Factory contract implemented by external provider assemblies |
| `IAIProvider` | Provider contract (lifecycle, call pipeline, encoding) |
| `IAIProviderSettings` | Per-provider descriptors and validation |
| `SmartHopperSettings` | Persistence for trust and provider settings |

### Code Examples

#### Basic Provider Discovery

```csharp
// Refresh providers during plugin initialization
ProviderManager.Instance.RefreshProviders();

// List all trusted providers
var providers = ProviderManager.Instance.GetProviders(includeUntrusted: false);
foreach (var provider in providers)
{
    Console.WriteLine($"Provider: {provider.Name}");
}

```

**Output**: A list of verified and initialized AI providers.

#### Retrieving a Provider by Name

```csharp
// Get a specific provider (handles "Default" indirection)
var provider = ProviderManager.Instance.GetProvider("OpenAI");

// Get provider settings for UI rendering
var settings = ProviderManager.Instance.GetProviderSettings("OpenAI");
var descriptors = settings.GetSettingDescriptors();

```

#### Updating Provider Settings

```csharp
var newSettings = new Dictionary<string, object>
{
    { "ApiKey", "sk-..." },
    { "Model", "gpt-4o" }
};

ProviderManager.Instance.UpdateProviderSettings("OpenAI", newSettings);

```

### Error Handling

| Error | Cause | Solution |
| --- | --- | --- |
| Provider not found | DLL missing or not named correctly | Verify file name starts with `SmartHopper.Providers.` |
| Trust verification failed | Authenticode or strong-name mismatch | Verify provider source and re-sign if necessary |
| `PlatformNotSupportedException` | Authenticode checked on non-Windows | Expected on macOS; only strong-name verification applies |
| Settings validation failed | Invalid value type or range | Check `IAIProviderSettings.ValidateSettings` error message |

---

## Architecture & Design

### Design Rationale

**Problem**: SmartHopper needs to support multiple AI providers from external assemblies without hardcoding them, while ensuring users are protected from malicious plugins.

**Approach**: A singleton `ProviderManager` scans for assemblies, verifies their authenticity, and initializes them asynchronously. It exposes a registry pattern for consumers to query providers by name or capability.

**Trade-offs**:

- Dynamic discovery (flexibility) vs startup overhead (scanning + verification)
- Authenticode + strong-name verification (security) vs macOS incompatibility (Authenticode is Windows-only)
- Singleton pattern (convenience) vs testability (requires mocking)

### Data Flow

```text
Rhino Startup â†’ RefreshProviders() â†’ Scan Directory â†’ Verify Trust â†’
InitializeProviderAsync() â†’ Register in Registry â†’ Available to Components

```

### Supply-Chain Security

| Layer | Mechanism |
| --- | --- |
| Authenticode | Certificate thumbprint must match host assembly |
| Strong-name | Public key token must match host assembly |
| User trust | First discovery prompts user; decision persisted |

### Related Documentation

- [AI Provider Base](./AIProvider.md)
- [IAIProvider Interface](./IAIProvider.md)
- [IAIProviderFactory](./IAIProviderFactory.md)
- [Authentication](./Authentication.md)
