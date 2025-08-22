# IAIProviderFactory

Factory interface discovered inside external provider assemblies.

- Source: `src/SmartHopper.Infrastructure/AIProviders/IAIProviderFactory.cs`

## Purpose

Expose a simple creation contract for a provider and its settings UI/validation.

## Members

- `IAIProvider CreateProvider()`
- `IAIProviderSettings CreateProviderSettings()`

## Usage

Implement this interface in `SmartHopper.Providers.*` assemblies so `ProviderManager` can instantiate your provider and settings during discovery.
