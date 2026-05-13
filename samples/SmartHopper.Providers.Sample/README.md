# SmartHopper Provider SDK — Sample scaffolding

This directory contains a starter csproj that builds a SmartHopper community provider against the `SmartHopper.ProviderSdk` package only — no SmartHopper host references required.

## Layout

```
samples/
└── SmartHopper.Providers.Sample/
    ├── SmartHopper.Providers.Sample.csproj   # references SmartHopper.ProviderSdk
    └── README.md                              # this file
```

To turn this into a working provider:

1. Add a class deriving from `SmartHopper.ProviderSdk.AIProviders.AIProvider` (or `AIProvider<T>` for typed responses).
2. Add an `IAIProviderFactory` implementation that returns instances of your provider and its settings.
3. Add an `IAIProviderSettings` subclass with descriptors for API key, model id, etc.
4. Decorate the assembly with:
   ```csharp
   [assembly: SmartHopper.ProviderSdk.Metadata.BuiltAgainstSdk("1.0.0")]
   [assembly: SmartHopper.ProviderSdk.Metadata.MinHostSdk("1.0.0")]
   [assembly: SmartHopper.ProviderSdk.Metadata.SmartHopperProviderId("my-provider")]
   ```
5. Build, copy the resulting `SmartHopper.Providers.MyProvider.dll` into either:
   - the SmartHopper app-local directory (next to `SmartHopper.Infrastructure.dll`), or
   - `%AppData%/SmartHopper/Providers` on Windows (platform equivalent elsewhere).
6. In SmartHopper settings, enable `AllowCommunityProviders` and accept the per-provider trust prompt.

For full reference docs, see [docs/Providers/ProviderSdk.md](../../docs/Providers/ProviderSdk.md) and the in-tree providers under `src/SmartHopper.Providers.*` for working examples.

## License

Sample code is licensed under MIT, matching the SDK itself. SmartHopper host code remains under LGPLv3.
