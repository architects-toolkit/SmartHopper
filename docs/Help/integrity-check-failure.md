# What to Do When Integrity Check Fails

If you see a message that a provider "failed integrity verification" or "hash mismatch," this guide will help you understand what that means and how to fix it.

---

## What Is the Integrity Check?

SmartHopper uses **SHA-256 hash verification** to confirm that your AI provider files are exactly what we published. Think of it like a fingerprint: every file has a unique hash value, and if even a single bit changes, the hash becomes completely different.

When you see a "mismatch," it means the file on your computer doesn't match our official records.

---

## Why Is This Important?

The integrity check protects you from:

- **Corrupted downloads**: Files can be damaged during download, causing crashes or strange behavior
- **Malware/tampering**: Bad actors might try to inject malicious code into AI providers

**In short**: A failed integrity check means something is wrong with the provider file, and using it could put your data or system at risk.

---

## What Are the Risks?

If you choose to use a provider that fails the integrity check:

- **Data theft**: A tampered provider could send your data to malicious servers
- **System compromise**: Malicious code could run on your computer through the provider
- **Project loss**: Erratic behavior could damage your Grasshopper definitions

**We strongly recommend fixing the issue rather than ignoring it.**

---

## How to Fix It

The solution is simple: **reinstall the affected provider(s)** from an official source.

### Option 1: Reinstall from Rhino Package Manager (Recommended)

1. In Rhino, type `PackageManager` in the command line and press Enter
2. Search for "SmartHopper"
3. Find the provider with the issue (e.g., "SmartHopper.Providers.MistralAI")
4. Click **Reinstall**, wait for it to complete
5. Restart Rhino

### Option 2: Download from Food4Rhino

1. Visit [https://www.food4rhino.com](https://www.food4rhino.com)
2. Search for "SmartHopper"
3. Download the latest version
4. Place the files in the Grasshopper Components folder
5. Restart Rhino

---

## What If the Problem Persists?

If you've reinstalled but still see the error:

1. **Check your internet connection**: The verification requires downloading hash information
2. **Contact us**: If the problem continues, please report it on our [GitHub Issues page](https://github.com/architects-toolkit/SmartHopper/issues)

---

## Can I Still Use the Provider?

Yes, but **we don't recommend it**. SmartHopper gives you two options:

- **Hard Integrity Check** (safer): Blocks unverified providers completely
- **Soft Integrity Check** (default): Allows the provider but shows warnings

You can change this setting in **SmartHopper Settings > Providers > Enable hard integrity check**.

---

## Need More Help?

- **Documentation**: Visit our [online documentation](https://architects-toolkit.github.io/SmartHopper)
- **Report a bug**: [GitHub Issues](https://github.com/architects-toolkit/SmartHopper/issues)
- **Community**: [Grasshopper Forum](https://discourse.mcneel.com/)
