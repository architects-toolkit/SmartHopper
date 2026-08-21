# SmartHopper.ProviderSdk

Stable, MIT-licensed SDK for building third-party AI providers for [SmartHopper](https://github.com/architects-toolkit/SmartHopper).

Community providers built against this SDK can compile on a clean machine without
the SmartHopper repository, produce a `SmartHopper.Providers.<Name>.dll`, and drop
it into the SmartHopper provider folder.

## License

This SDK is licensed under the **MIT License** (see [LICENSE](LICENSE)).

This is intentional: the SmartHopper host is licensed under **LGPLv3**, but the
SDK that providers link against is permissive so that closed-source community
providers can link to it without inheriting LGPL obligations on their own
assemblies.

## What this package contains

- Provider contracts: `IAIProvider`, `IAIProviderFactory`, `IAIProviderSettings`, `IAIProviderModels`.
- Provider base classes: `AIProvider`, `AIProvider<T>`, `AIProviderSettings`, `AIProviderModels`, `AIProviderStreamingAdapter`.
- Request/response DTOs: `AICall.Core.Interactions`, `AICall.Core.Requests`, `AICall.Core.Returns`, `AICall.Core.Base`, `AICall.Metrics`.
- Model capability types: `AIModels.AICapability`, `AIModels.AIModelCapabilities`, `AIModels.AIModelPricing`, `AIExtraDescriptor`.
- Settings descriptors: `SettingDescriptor`, secret flags, validation result types.
- Streaming: `IStreamingAdapter` and provider-facing delta/result types.
- Host-injected abstractions a provider receives: `IProviderSettingsStore`,
  `IProviderLogger`, `IProviderHttpClientFactory`, `IProviderDiagnostics`.
- Compatibility metadata: `SmartHopperProviderSdkVersionAttribute`,
  `SmartHopperProviderIdAttribute`, `BuiltAgainstSdkAttribute`.

## What this package does NOT contain

The following remain in the SmartHopper host and are NOT exposed through the SDK:

- `ProviderManager`, hash/signature verification, dialogs, settings persistence.
- Policy pipeline, conversation sessions, AICall validation pipeline, execution,
  batch orchestration.
- Tool manager, tool registry, `IAIToolProvider` (tool DTOs in interactions are
  included so providers can encode/decode tool calls in responses, but tool
  registration is host-only).
- Rhino/Eto UI, WebChat, badges, runtime message UI.

## Target frameworks

- `net7.0-windows`
- `net7.0`

## Getting started

```bash
dotnet new classlib -n SmartHopper.Providers.MyProvider
dotnet add SmartHopper.Providers.MyProvider package SmartHopper.ProviderSdk
```

Then implement an `IAIProviderFactory` that returns your `AIProvider<T>`
subclass. Build with target frameworks `net7.0-windows;net7.0` and drop the
resulting `SmartHopper.Providers.MyProvider.dll` into:

- **App-local**: the same folder as `SmartHopper.Infrastructure.dll`, or
- **User-local**: `%AppData%\SmartHopper\Providers\` (Windows) /
  `~/.config/SmartHopper/Providers/` (macOS).

See [docs/Providers/ProviderSdk.md](https://github.com/architects-toolkit/SmartHopper/blob/main/docs/Providers/ProviderSdk.md)
in the SmartHopper repository for the full guide.
