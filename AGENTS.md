# Agent Notes

## Testing

- The `SmartHopper.ProviderSdk.Tests` project can be built and run without a strong-name key:

  ```powershell
  dotnet test src/SmartHopper.ProviderSdk.Tests/SmartHopper.ProviderSdk.Tests.csproj -p:SignAssembly=false
  ```

- All Provider SDK test classes use `[Collection("ProviderSdk")]` to disable xUnit parallelization. This lets tests safely reset shared mutable state such as `AIModelCapabilityRegistry.Instance.Models`.

## Signing

- The official build scripts expect `signing.snk` / `signing.pfx` and update `SmartHopperPublicKey` in `src/SmartHopper.Infrastructure/SmartHopper.Infrastructure.csproj`.
- Do not commit generated signing keys, certificates, or API keys.
- `SmartHopperPublicKey` changes made by local build tooling should be reverted before committing.
