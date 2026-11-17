# What to Do When a Provider Integrity Check Fails

If SmartHopper reports that an AI provider assembly failed integrity verification or had a SHA-256 hash mismatch, this guide explains what the check does and how to resolve it.

## What is the provider integrity check?

Provider plugins (`SmartHopper.Providers.*.dll`) are executable .NET assemblies. When Grasshopper loads SmartHopper, `ProviderManager` computes the SHA-256 hash of each provider DLL on disk and compares it against the official hash manifest published for that SmartHopper release (in addition to verifying Authenticode and strong-name signatures on Windows).

A mismatch means the DLL on your computer does not match the official file published for your installed SmartHopper version — typically because of an incomplete manual update, a corrupted download, or a modified file.

## How to fix it

Reinstall SmartHopper from an official source so all assemblies come from the same release:

1. **Rhino Package Manager (recommended):** Run `PackageManager` in Rhino, select **SmartHopper**, click **Reinstall** (or update to the latest version), and restart Rhino.
2. **Food4Rhino / GitHub Releases:** Download the complete release archive for your platform, replace all files in your Grasshopper *Components Folder* (*File → Special Folders → Components Folder*), and restart Rhino.

After restarting, you can run **SmartHopper → Verify Providers** from the Grasshopper menu bar to confirm that all providers pass verification.

## Integrity check modes (`SmartHopper → Settings → General`)

The **Provider integrity check** setting controls how SmartHopper behaves when a provider's hash cannot be verified against the official manifest:

- **Soft (default):** Logs and displays a warning on components using the unverified provider, but still allows it to load (required when testing self-built or community provider DLLs).
- **Hard:** Blocks providers whose hash mismatches or is missing from the official manifest when online, but permits loading when offline and the manifest cannot be reached.
- **Strict:** Blocks any provider that cannot be verified against the official online manifest, including when offline.

See also the [online Security & Privacy guide](https://docs.smarthopper.xyz/docs/reference/security-and-privacy) and [Enable, disable, trust & verify providers](https://docs.smarthopper.xyz/docs/providers/enable-disable-providers).
