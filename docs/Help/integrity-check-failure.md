# What to Do When Integrity Check Fails

If you see a message that a provider "failed integrity verification" or "hash mismatch," this guide will help you understand what that means and how to fix it.

---

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `N/A` |
| **Since Version** | ? |
| **Last Updated** | 2026-06-14 |
| **Documentation Maintainer** | Devin AI |

_Note: This documentation was written by AI on its own. It may contain some mistakes. If you would like to help, read this documentation and delete this comment if everything is okay._

---

## Why Read This?

The integrity check is a critical security feature that protects your data and system from tampered or corrupted AI provider files. This guide explains how the check works, why it matters, and the exact steps to resolve failures safely.

**You should read this if you:**

- See an integrity check warning or "hash mismatch" message in SmartHopper
- Want to understand how SmartHopper verifies provider security
- Need to reinstall a provider after a failed verification

---

## End-User Guide

### What Is the Integrity Check?

SmartHopper uses **SHA-256 hash verification** to confirm that your AI provider files are exactly what we published. Think of it like a fingerprint: every file has a unique hash value, and if even a single bit changes, the hash becomes completely different.

When you see a "mismatch," it means the file on your computer doesn't match our official records.

### Why Is This Important?

The integrity check protects you from:

- **Corrupted downloads**: Files can be damaged during download, causing crashes or strange behavior
- **Malware/tampering**: Bad actors might try to inject malicious code into AI providers

**In short**: A failed integrity check means something is wrong with the provider file, and using it could put your data or system at risk.

### What Are the Risks?

If you choose to use a provider that fails the integrity check:

- **Data theft**: A tampered provider could send your data to malicious servers
- **System compromise**: Malicious code could run on your computer through the provider
- **Project loss**: Erratic behavior could damage your Grasshopper definitions

**We strongly recommend fixing the issue rather than ignoring it.**

### How to Fix It

The solution is simple: **reinstall the affected provider(s)** from an official source.

#### Option 1: Reinstall from Rhino Package Manager (Recommended)

1. In Rhino, type `PackageManager` in the command line and press Enter
2. Search for "SmartHopper"
3. Find the provider with the issue (e.g., "SmartHopper.Providers.MistralAI")
4. Click **Reinstall**, wait for it to complete
5. Restart Rhino

#### Option 2: Download from Food4Rhino

1. Visit [https://www.food4rhino.com](https://www.food4rhino.com)
2. Search for "SmartHopper"
3. Download the latest version
4. Place the files in the Grasshopper Components folder
5. Restart Rhino

### What If the Problem Persists?

If you've reinstalled but still see the error:

1. **Check your internet connection**: The verification requires downloading hash information
2. **Contact us**: If the problem continues, please report it on our [GitHub Issues page](https://github.com/architects-toolkit/SmartHopper/issues)

### Can I Still Use the Provider?

Yes, but **we don't recommend it**. SmartHopper gives you three options:

- **Soft Integrity Check** (default): Allows the provider but shows warnings
- **Hard Integrity Check** (safer): Is more strict about loading unverified provider. This option only allows verified providers, but is permissive when there is no internet connection to verify them.
- **Strict Integrity Check** (most secure): Uses the strictest rules and may block more providers. Providers are blocked unless they can be verified agains the official repository.

You can change this setting in **SmartHopper Settings > Providers > Integrity Check Mode** (a dropdown with Soft, Hard, and Strict).

### Need More Help?

- **Documentation**: Visit our [online documentation](https://architects-toolkit.github.io/SmartHopper)
- **Report a bug**: [GitHub Issues](https://github.com/architects-toolkit/SmartHopper/issues)
- **Community**: [Grasshopper Forum](https://discourse.mcneel.com/)

---

## Developer Reference

### Computing a SHA-256 Hash in C#

SmartHopper computes SHA-256 hashes to verify provider files. The same logic can be used in your own utilities:

```csharp
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static string ComputeSha256Hash(string filePath)
{
    using var sha256 = SHA256.Create();
    using var stream = File.OpenRead(filePath);
    var hash = sha256.ComputeHash(stream);

    var sb = new StringBuilder();
    foreach (var b in hash)
    {
        sb.Append(b.ToString("x2"));
    }

    return sb.ToString();
}

```

### Verifying File Integrity Programmatically

You can compare a computed hash against an expected value to implement your own integrity checks:

```csharp
public static bool VerifyFileIntegrity(string filePath, string expectedHash)
{
    if (!File.Exists(filePath))
        return false;

    var actualHash = ComputeSha256Hash(filePath);
    return actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
}

```

---

## Architecture & Design

### Verification Pipeline

The integrity check runs during provider loading and follows this pipeline:

1. **Hash Request**: SmartHopper downloads the official SHA-256 hash list from the repository
2. **Local Computation**: For each provider DLL, it computes the SHA-256 hash of the file on disk
3. **Comparison**: The computed hash is compared against the official hash
4. **Decision**: Based on the selected integrity mode (Soft, Hard, Strict), the provider is either loaded, loaded with a warning, or blocked

### Offline Behavior

- **Soft / Hard modes**: If no internet connection is available, providers are allowed to load without verification (Hard is permissive when offline)
- **Strict mode**: Providers are blocked unless they can be verified against the official repository, even if offline
