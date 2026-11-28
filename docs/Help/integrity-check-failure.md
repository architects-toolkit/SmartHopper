# What to Do When a Provider Integrity Check Fails

If SmartHopper reports that an AI provider assembly failed integrity verification or had a SHA-256 hash mismatch, this guide explains what the check does and how to resolve it.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Infrastructure/AIProviders/ProviderManager.cs`, `src/SmartHopper.Infrastructure/AIProviders/ProviderIntegrityReportGate.cs`, `src/SmartHopper.Infrastructure/Dialogs/ProviderTrustDialog.cs` |
| **Since Version** | 2.0.0 |
| **Last Updated** | 2026-11-23 |
| **Documentation Maintainer** | Devin AI |

---

## Why Read This?

- A dialog reported that one or more providers **failed integrity verification** or had a **hash mismatch**
- You want to know **what the integrity check verifies** and why a provider was blocked
- You need to **restore a blocked provider** or adjust the check mode (Soft / Hard / Strict)
- The report modal is blocking the UI and you need to **reach Settings**

---

## End-User Guide

### What is the provider integrity check?

Provider plugins (`SmartHopper.Providers.*.dll`) are executable .NET assemblies. When Grasshopper loads SmartHopper, `ProviderManager` computes the SHA-256 hash of each provider DLL on disk and compares it against the official hash manifest published for that SmartHopper release (in addition to verifying Authenticode and strong-name signatures on Windows).

A mismatch means the DLL on your computer does not match the official file published for your installed SmartHopper version — typically because of an incomplete manual update, a corrupted download, or a modified file.

### How to fix it

Reinstall SmartHopper from an official source so all assemblies come from the same release:

1. **Rhino Package Manager (recommended):** Run `PackageManager` in Rhino, select **SmartHopper**, click **Reinstall** (or update to the latest version), and restart Rhino.
2. **Food4Rhino / GitHub Releases:** Download the complete release archive for your platform, replace all files in your Grasshopper *Components Folder* (*File → Special Folders → Components Folder*), and restart Rhino.

After restarting, you can run **SmartHopper → Verify Providers** from the Grasshopper menu bar to confirm that all providers pass verification.

### Integrity check modes (`SmartHopper → Settings → Security`)

The **Provider integrity check** setting controls how SmartHopper behaves when a provider's hash cannot be verified against the official manifest:

- **Soft (default):** Logs and displays a warning on components using the unverified provider, but still allows it to load (required when testing self-built or community provider DLLs).
- **Hard:** Blocks providers whose hash mismatches or is missing from the official manifest when online, but permits loading when offline and the manifest cannot be reached.
- **Strict:** Blocks any provider that cannot be verified against the official online manifest, including when offline.

### Reaching Settings while findings exist

The integrity report dialog offers an **Open Settings** button that deep-links directly to the Security tab, so you can switch the mode back to **Soft** without hunting through tabs. The same report is shown at most once per set of findings — rescans (`SmartHopper → Refresh Providers`) only re-show it when the findings change. Opening **SmartHopper → Settings** itself never triggers a rescan, so Settings always opens even when providers are blocked.

The canvas assistant surfaces blocked providers too: when no provider can be resolved because integrity blocked them, its error dialog explains the block and offers to open Settings on the Security tab.

---

## Developer Reference

- `ProviderManager.DiscoverProvidersAsync` / `LoadProviderAssemblyAsync` — hash computation, manifest comparison, signature checks, and per-mode load blocking.
- `ProviderManager.LastIntegrityFindings` — findings from the last scan, consumed by UI surfaces without re-verifying.
- `ProviderIntegrityReportGate` — fingerprints findings (`file|severity|status`) and suppresses duplicate reports until the set changes or a clean scan re-arms it.
- `ProviderTrustDialog` — the report dialog; always renders **Open Settings**, which routes through `SettingsLauncher` to the Security tab.
- `SettingsLauncher` — Infrastructure-owned indirection; `Open(tab)` pends when no opener is registered yet and flushes on `RegisterOpenSettings`.

---

## Architecture & Design

- The mode is enforced at **provider discovery** (blocked assemblies are never instantiated/registered); consent checks are re-evaluated at **resolution time** in `GetProvider`/`GetProviders`.
- `SettingsDialog` construction performs **no provider discovery** — it reads the state already discovered, so opening Settings can never produce a nested modal or deadlock.
- The report dialog's **Open Settings** button is unconditional: availability is decided at click time by `SettingsLauncher`, not at dialog-build time, which removes the registration-order race.

---

See also the [online Security & Privacy guide](https://docs.smarthopper.xyz/docs/reference/security-and-privacy) and [Enable, disable, trust & verify providers](https://docs.smarthopper.xyz/docs/providers/enable-disable-providers).
