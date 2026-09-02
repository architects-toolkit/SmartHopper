# SmartHopper Provider SDK

The Provider SDK (`SmartHopper.ProviderSdk`) is a standalone, MIT-licensed assembly that exposes the contracts, base classes, and DTOs a community AI provider needs to integrate with SmartHopper. Provider authors can build against this SDK on a clean machine without cloning or building the rest of the SmartHopper repo.

## What the SDK contains

The SDK is **API-connection only**:

- **Contracts** — `IAIProvider`, `IAIProviderFactory`, `IAIProviderSettings`, `IAIProviderModels`, `IAIBatchProvider`.
- **Base classes** — `AIProvider`, `AIProvider<T>`, `AIProviderSettings`, `AIProviderModels`, `AIProviderStreamingAdapter`.
- **Request / response DTOs** — `AICall.Core.{Base, Interactions, Requests, Returns}`, `AICall.Metrics`, `AICall.JsonSchemas` (`JsonSchemaService`, `IJsonSchemaAdapter`, `OpenAICompatibleJsonSchemaAdapter`), `AICall.Core.Interactions.OpenAICompatibleRoleMapper`, and `AICall.Batch` contracts.
- **Model capabilities** — `AIModels.*`, `AIExtraDescriptor`, `AIModelCapabilityRegistry` singleton.
- **Settings descriptors** — `SettingDescriptor`, secret flags, validation result types.
- **Streaming** — `IStreamingAdapter` and provider-facing delta/result types.
- **Tool DTOs** — structures required to encode/decode tool calls and tool results inside a provider response (no `ToolManager`, no tool registration).
- **Compatibility metadata** — `SmartHopperProviderSdkVersionAttribute`, `BuiltAgainstSdkAttribute`, `MinHostSdkAttribute`, `SmartHopperProviderIdAttribute`.

## JSON schema adapters

OpenAI-compatible providers can register `OpenAICompatibleJsonSchemaAdapter` directly:

```csharp
JsonSchemaAdapterRegistry.Register(new OpenAICompatibleJsonSchemaAdapter(this.Name));
```

This shared adapter wraps non-object root schemas into an object root (`array` → `items`, primitives → `value`, unknown → `data`) so providers that require object-root structured-output schemas can consume them. See [AICall/JsonSchemaAdapters.md](./AICall/JsonSchemaAdapters.md) for the full contract.

## Role mapping

OpenAI-compatible providers can use `OpenAICompatibleRoleMapper` to map `AIAgent` values to provider-facing chat message roles:

```csharp
var role = OpenAICompatibleRoleMapper.MapRole(interaction.Agent);
if (role == null)
{
    return null;
}
```

`MapRole` returns `system` for `AIAgent.System` and `AIAgent.Context`, `user` for `AIAgent.User`, `assistant` for `AIAgent.Assistant` and `AIAgent.ToolCall`, and `tool` for `AIAgent.ToolResult`. It returns `null` for agents that should not be sent to the provider (e.g., UI-only diagnostics or unknown agents). A `MapRoleOrThrow` overload is available for providers that fail fast on unsupported agents.

The built-in `OpenAI`, `MistralAI`, `LocalAI`, `Ollama`, `OpenRouter`, and `DeepSeek` providers all use this shared mapper, removing the duplicated `switch (interaction.Agent)` blocks.

## What is NOT in the SDK

These types stay host-side in `SmartHopper.Infrastructure` and the rest of the application:

- `ProviderManager`, `ProviderHashVerifier`, `ProviderClassifier`, trust dialogs, signature/hash policy, `SmartHopperSettings` persistence, secret storage.
- `AICall.{Policies, Sessions, Validation, Execution, Batch}`.
- `AITools.*` (`ToolManager`, `IAIToolProvider`).
- Rhino/Eto UI, WebChat, badges.

## Target frameworks

- `net7.0` (cross-platform; macOS-friendly)
- `net7.0-windows` (Windows-specific surface)

## License

The SDK is licensed under **MIT**. SmartHopper itself remains LGPLv3. The permissive license on the SDK lets closed-source community providers link against it without taking on LGPL obligations on their own assemblies.

## Naming convention

Provider assemblies discovered at runtime must match `SmartHopper.Providers.*.dll`. SmartHopper scans:

1. The app-local directory (next to `SmartHopper.Infrastructure.dll`).
2. `%AppData%/SmartHopper/Providers` (Windows) or the platform equivalent under the user's application data folder.

App-local providers win on duplicate-id conflicts (see plan §2.2).

## Minimal provider

```csharp
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Settings;

[assembly: SmartHopper.ProviderSdk.Metadata.BuiltAgainstSdk("1.0.0")]
[assembly: SmartHopper.ProviderSdk.Metadata.MinHostSdk("1.0.0")]
[assembly: SmartHopper.ProviderSdk.Metadata.SmartHopperProviderId("my-provider")]

public sealed class MyProviderFactory : IAIProviderFactory
{
    public IAIProvider CreateProvider() => new MyProvider();
    public IAIProviderSettings CreateProviderSettings() => new MyProviderSettings();
}
```

The host instantiates the factory through an isolated `AssemblyLoadContext` so private dependencies of the provider don't leak into the rest of the SmartHopper process.

## SemVer & compatibility

- The SDK uses Semantic Versioning. `MAJOR` is reserved for breaking provider contract changes.
- Providers declare both `BuiltAgainstSdk` and `MinHostSdk` via assembly attributes.
- At load time the host enforces `BuiltAgainstSdk.MAJOR == HostSdk.MAJOR` and `HostSdk >= provider.MinHostSdk`. Mismatches classify the provider as `Invalid` and block it with a clear diagnostic.
- Two host majors cannot coexist in one Rhino process and this is documented as unsupported.

## Trust model

Community providers (those not cryptographically attributable to SmartHopper) are only loaded when:

1. `AllowCommunityProviders = true` in `SmartHopperSettings.json`, and
2. `BlockNonOfficialProviders = false`, and
3. The user accepts the per-provider trust prompt the first time a given community DLL is discovered.

The classification is purely cryptographic — strong-name token + Authenticode (Windows) + the SmartHopper hash manifest. File names and provider ids cannot reach `Official` without one of those signals.

Community/unsigned providers surface warning runtime messages on every AI component that uses them.

## Threat notes

Trusting a community provider grants it full SmartHopper process privileges. The SDK does not sandbox provider code. Community providers can read other providers' settings only through the `IProviderSettingsStore` abstraction, which scopes access by provider id.

## Metadata

- Source Code: See source repository.
- Since Version: 2.0.0
- Last Updated: 2026-09-02
- Documentation Maintainer: Marc Roca Musach

---

## Why Read This?

This document provides details about ProviderSdk.

## End-User Guide

End-user guidance for ProviderSdk.

## Developer Reference

Example usage:

```csharp
// Placeholder example
```

```csharp
// Another placeholder example
```

## Architecture & Design

Architecture and design notes for ProviderSdk.

```csharp
// Example code for Developer Reference
```

```csharp
// Additional example for Developer Reference
```
