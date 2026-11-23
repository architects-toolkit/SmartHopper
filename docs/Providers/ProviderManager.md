# ProviderManager

Discovers, verifies, registers, and initializes AI providers at runtime.

## Pre-consent metadata

Trust previews must never load provider assemblies or invoke provider code. The host
reads the embedded `SmartHopper.ProviderManifest.json` resource using PE metadata:
`{"schemaVersion":1,"displayName":"Example","modelCount":12}`. Embed it with that
logical resource name; first-party projects use `provider.manifest.json` automatically.
The optional `modelCount` is a publisher-declared non-deprecated catalogue snapshot,
not a live network query. Dynamic catalogues use `null`. Keep the snapshot current
when changing provider models. Missing/invalid manifests use the assembly filename
and an unknown count, with no provider-specific icon before consent.

The embedded bytes are covered by the DLL's SHA-256, verified against the existing
official hash catalogue before execution. A manifest alone never proves trust:
only a matching official DLL hash earns the verified indicator. Community/unavailable
verification stays visibly unverified even when a manifest provides a friendly name.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/AIProviders/ProviderManager.cs` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

- Discovery
  - Scans the app-local directory and `%AppData%/SmartHopper/Providers` for `SmartHopper.Providers.*.dll`.
  - Each candidate is loaded into a per-provider `AssemblyLoadContext` (see `ProviderAssemblyLoader`) so private dependencies stay isolated.
  - SDK type identity (`IAIProviderFactory`) is validated before activation. Mismatch → `Invalid`.
  - SemVer compatibility is checked via `BuiltAgainstSdk`/`MinHostSdk` assembly attributes.
- Cryptographic classification (`ProviderClassifier`)
  - `Official` — strong-name token matches host AND/OR Authenticode matches host AND/OR SHA-256 is in the published manifest, with no contradicting signal.
  - `OfficialTampered` — one signal says official but another contradicts. Always blocked.
  - `Community` — valid managed assembly not tied to SmartHopper. Subject to `AllowCommunityProviders` and `BlockNonOfficialProviders`.
  - `Invalid` — load failure, missing factory, SDK type-identity mismatch, version incompatibility.
- Trust settings (`SmartHopperSettings`)
  - `AllowCommunityProviders` (default `false`): community providers are blocked unless this is on.
  - `BlockNonOfficialProviders` (default `false`): hard override that allows only `Official` providers.
  - `ProviderIntegrityCheckMode` continues to govern hash-mismatch behavior for Official providers.
  - `TrustedProviders` — per-provider enable decisions persisted as a `Dictionary<string, bool>` keyed by assembly name.
  - `ProviderTrustRecords` — per-provider consent snapshot (`ConsentedHash` + `VerifiedTrust`) that makes stored trust verification-conditional.
- Verification-conditional trust (`EvaluateStoredTrust`)
  - Consent binds to the bytes consented to, not the assembly name — a trusted file is re-checked on every load, even when the catalogue check fails.
  - `Match` → loads silently and re-mints the record, so legitimate official updates never re-prompt.
  - `Mismatch` on a trusted file → treated as tampering: severe error, the provider is disabled, and the user is told to re-download from official sources. Never honored by records — a forged snapshot cannot bypass it.
  - `NotFound`/`Unavailable`/check failure → unchanged bytes keep consent; changed bytes or a missing record re-prompt in `Soft` and block in `Hard`/`Strict`.
  - DEBUG builds keep trust silently on changed bytes — local provider rebuilds never re-prompt.
  - Disabled providers are skipped before any verification or `Assembly.LoadFrom` — their code never executes.
- Registration & initialization
  - Duplicate provider ids: Official > Community > everything else. Tampered/Invalid never win.
  - `InitializeProviderAsync()` is wrapped in a 30-second per-provider timeout so a hanging provider can't block discovery.
- Integrity findings & report lifecycle
  - Per-load findings (`ProviderIntegrityIssue`) are kept on `LastIntegrityFindings` so non-modal surfaces (e.g. the canvas assistant's no-provider error) can explain blocked state without re-verifying.
  - `ReportIntegrityIssuesAsync` shows one unified report per findings set. `ProviderIntegrityReportGate` fingerprints the set (`file|severity|status`, order-insensitive): an unchanged set is not re-shown on every refresh, a changed set reports again, a clean scan re-arms the gate.
  - Discovery runs at startup and on the explicit `SmartHopper → Refresh Providers` menu item only. `SettingsDialog` never triggers discovery — a modal report during dialog construction deadlocked Settings, the very place needed to relax integrity mode.
  - The report offers an **Open Settings** button deep-linking to the Security tab via `SettingsLauncher`, an Infrastructure-owned opener callback registered by SmartHopper.Menu so Infrastructure never references the Menu project.
- Accessors
  - `GetProviders(includeUntrusted=false)`
  - `GetProvider(name)` (handles "Default" indirection)
  - `GetProviderSettings(name)`, `GetProviderAssembly(name)`, `GetProviderIcon(name)`
  - `GetDefaultAIProvider()`
  - `GetProviderClassification(name)`, `IsProviderCommunity(name)`, `IsProviderUnsigned(name)`, `IsProviderMismatched(name)`, `IsProviderUnknown(name)`, `IsProviderUnavailable(name)`, `GetProviderTrustRecord(name)`
- Settings management
  - `UpdateProviderSettings(name, Dictionary<string, object>)`
  - Validates via `IAIProviderSettings.ValidateSettings`, persists via `SmartHopperSettings`, refreshes provider cache.

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
- **Trusting providers**: The first time untrusted providers are discovered, a single dialog lists all of them with icon, hash-verification status, last update and model count — you pick which to enable
- **Provider updates**: Official updates verify against the published hash manifest and load silently. If a custom/third-party provider's file changes, a re-confirmation dialog explains the change before it may run again. A file claiming an official provider name but failing hash verification is disabled and reported as a possible tampering attempt — re-download it from official sources before re-enabling it
- **Updating settings**: When you change provider settings in the SmartHopper preferences, ProviderManager validates and persists them

### Visual Guide

<!-- PLACEHOLDER: Screenshot of SmartHopper provider settings panel -->
<!-- - Location: Rhino → Tools → Options → SmartHopper → Providers -->
<!-- - Shows: list of discovered providers with trust status and settings buttons -->

### Common Questions

**Q: Why is my new provider not showing up?**
A: Ensure the DLL is named `SmartHopper.Providers.*.dll`, is in the same directory as SmartHopper, and has passed trust verification. Check the Rhino command line for discovery log messages.

**Q: What does "untrusted provider" mean?**
A: The provider DLL failed Authenticode or strong-name verification. Only trust providers from sources you verify.

**Q: Can I disable a provider without uninstalling it?**
A: Yes. Use the provider settings UI to disable it; the DLL is then skipped entirely — its code never runs and it does not appear in the provider list.

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
Rhino Startup → RefreshProviders() → Scan Directory → Verify Trust →
InitializeProviderAsync() → Register in Registry → Available to Components

```

### Supply-Chain Security

| Layer | Mechanism |
| --- | --- |
| Authenticode | `WinVerifyTrust` must prove the signature covers the file bytes (Windows); signer certificate thumbprint must then match the host assembly |
| Strong-name | Public key token must match host assembly |
| User trust | First discovery shows a unified enable-providers dialog; decisions persisted |

`AuthenticodeSignatureVerifier` (`SmartHopper.Infrastructure/Security`) wraps `WinVerifyTrust` with `WINTRUST_ACTION_GENERIC_VERIFY_V2`. Statuses proving file integrity are accepted — `Valid`, or `ValidUntrusted` for signature-valid chain failures (untrusted root, expired, unchainable) since the signer is a non-CA certificate pinned by thumbprint. `Unsigned` and `Invalid` (bad digest, malformed, distrusted) are rejected. Revocation is not checked: the pinned cert has no revocation infrastructure and offline use must keep working. Both `ProviderManager.VerifySignature` (the load gate) and `ProviderClassifier` (trust signals) share the same verification — a tampered file can no longer pass by merely carrying the certificate blob, and the classifier's "signature present" signal means "integrity proven". Strong-name remains an identity check, not a trust boundary. macOS has no Authenticode: the strong-name token and the SHA-256 hash manifest apply there.

### Related Documentation

- [AI Provider Base](./AIProvider.md)
- [IAIProvider Interface](./IAIProvider.md)
- [IAIProviderFactory](./IAIProviderFactory.md)
- [Authentication](./Authentication.md)
