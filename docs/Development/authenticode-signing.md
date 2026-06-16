# Authenticode Signing Security

The `Sign-Authenticode.ps1` script supports both **secure interactive use** and **CI/CD automation** through a dual-mode password handling approach.

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

This document explains the security model behind the Authenticode signing script. It helps developers understand the risks of plain-text password handling and how to safely sign assemblies both locally and in automated CI/CD pipelines.

**You should read this if you:**

- Need to sign SmartHopper assemblies with a PFX certificate
- Configure or maintain CI/CD release workflows
- Want to understand why the script accepts plain-text password parameters in automation

---

## End-User Guide

### Security Considerations

#### ⚠️ Plain-Text Password Risks

Passing passwords as command-line arguments (`-Password "mypassword"`) exposes them in:

- **Shell history** - Stored in PowerShell history files
- **Process listings** - Visible to other users via Task Manager/ps
- **Log files** - May be captured by logging systems

#### ✅ Recommended Approaches

##### Interactive Use (Local Development)

**Omit the `-Password` parameter** to be prompted securely:

```powershell

# Secure - password not exposed

.\tools\Sign-Authenticode.ps1 -Generate

# You will be prompted: "Enter password for PFX certificate generation"

.\tools\Sign-Authenticode.ps1 -Base64 $base64Data

# You will be prompted: "Enter password for Base64 PFX import"

.\tools\Sign-Authenticode.ps1 -SignRelease

# You will be prompted if needed


```

##### CI/CD Automation (GitHub Actions)

Use **secrets management** to inject passwords via environment variables:

```yaml

- name: Sign assemblies
  shell: pwsh
  env:
    PFX_PASSWORD: ${{ secrets.SIGNING_PFX_PASSWORD }}
  run: |
    $trimmedPassword = $env:PFX_PASSWORD.Trim()
    ./tools/Sign-Authenticode.ps1 -Sign $buildPath -Password $trimmedPassword

```

**Why this is acceptable in CI/CD:**

- Secrets are injected by the runner, not stored in code
- Environment variables are cleared after job completion
- No persistent shell history in ephemeral runners
- GitHub Actions masks secrets in logs

### Usage Examples

#### Generate Self-Signed Certificate

```powershell

# Secure (prompted)

.\tools\Sign-Authenticode.ps1 -Generate

# CI/CD (from secret)

.\tools\Sign-Authenticode.ps1 -Generate -Password $env:PFX_PASSWORD

```

#### Decode Base64 PFX

```powershell

# Secure (prompted)

.\tools\Sign-Authenticode.ps1 -Base64 $base64Data

# CI/CD (from secret)

.\tools\Sign-Authenticode.ps1 -Base64 $env:PFX_BASE64 -Password $env:PFX_PASSWORD

```

#### Sign Assemblies

```powershell

# Secure (prompted if needed)

.\tools\Sign-Authenticode.ps1 -SignRelease

# CI/CD (from secret)

.\tools\Sign-Authenticode.ps1 -Sign bin/Release -Password $env:PFX_PASSWORD

```

### Best Practices

1. **Never commit passwords** to version control
2. **Use GitHub Secrets** for CI/CD passwords
3. **Omit `-Password`** for local development
4. **Rotate certificates** regularly
5. **Limit certificate access** to authorized personnel only

---

## Developer Reference

### Dual-Mode Password Handling

The script accepts `[string]$Password` but includes safeguards:

```powershell

# Password handling: Accept string for CI/CD compatibility, but warn about security

$securePassword = $null
if (-not [string]::IsNullOrEmpty($Password)) {
    # CI/CD mode: Convert plain-text password to SecureString
    # WARNING: This exposes the password in shell history and process listings
    $securePassword = ConvertTo-SecureString -String $Password -AsPlainText -Force
    
    # Only warn in interactive sessions (not in CI/CD)
    if ([Environment]::UserInteractive -and -not $env:CI -and -not $env:GITHUB_ACTIONS) {
        Write-Warning "Password provided as plain text. For better security, omit -Password to be prompted securely."
    }
}

```

### Detection Logic

The script detects CI/CD environments by checking:

- `[Environment]::UserInteractive` - False in non-interactive sessions
- `$env:CI` - Set by most CI/CD platforms
- `$env:GITHUB_ACTIONS` - Set by GitHub Actions specifically

Warnings are **suppressed in CI/CD** to avoid cluttering build logs.

### Secure Prompting

When `-Password` is omitted, the script uses `Read-Host -AsSecureString`:

```powershell
if (-not $Password) {
    $securePassword = Read-Host "Enter password for PFX certificate generation" -AsSecureString
    if (-not $securePassword) {
        Write-Error "-Password is required when generating a PFX certificate."
        exit 1
    }
}

```

This ensures:

- Password characters are masked during input (shown as `*`)
- Password never stored in plain text in memory
- No exposure in shell history or process listings

### C# SecureString Example

When consuming certificate passwords in C#, prefer `SecureString` to reduce the time secrets spend in plain-text memory:

```csharp
using System.Security;

public SecureString ToSecureString(string plainText)
{
    var secure = new SecureString();
    foreach (char c in plainText)
    {
        secure.AppendChar(c);
    }
    secure.MakeReadOnly();
    return secure;
}

```

### C# Authenticode Signing Example

You can also sign assemblies programmatically in C# using `X509Certificate2` and the `StrongName` or Authenticode APIs:

```csharp
using System.Security.Cryptography.X509Certificates;

public void SignAssembly(string assemblyPath, string pfxPath, SecureString password)
{
    var certificate = new X509Certificate2(pfxPath, password);
    // Use the certificate with your signing tool or SDK
    Console.WriteLine($"Signing with subject: {certificate.Subject}");
}

```

---

## Architecture & Design

### Alternatives Considered

#### 1. SecureString-Only Parameter ❌

```powershell
param([System.Security.SecureString]$Password)

```

**Rejected because:**

- Breaks GitHub Actions workflows (can't pass secrets as SecureString)
- Requires complex workarounds in CI/CD
- Would need separate scripts for CI vs local use

#### 2. Environment Variable Only ❌

```powershell
$password = $env:PFX_PASSWORD

```

**Rejected because:**

- Breaks interactive workflow (requires setting env var first)
- Less discoverable for developers
- Still exposes password in environment

#### 3. File-Based Password ❌

```powershell
$password = Get-Content password.txt

```

**Rejected because:**

- Requires managing password files
- File permissions complexity
- Still plain-text storage

#### 4. Dual-Mode with Warnings ✅ **CHOSEN**

**Benefits:**

- ✅ Backward compatible with existing CI/CD workflows
- ✅ Secure by default for interactive use
- ✅ Clear warnings educate developers
- ✅ Single script to maintain
- ✅ Detects and adapts to execution context
