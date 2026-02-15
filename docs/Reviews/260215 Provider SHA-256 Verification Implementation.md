# Provider SHA-256 Verification Implementation Review

**Date**: February 15, 2026  
**Purpose**: Enhance provider security on macOS by implementing SHA-256 hash verification  
**Related Issue**: macOS Compatibility Review - Issue 6 (Assembly Loading Without Verification)  
**Severity**: CRITICAL

---

## Executive Summary

This document outlines the implementation plan for SHA-256 hash-based verification of provider DLLs to address the security gap on macOS where Authenticode verification is unavailable. The solution involves:

1. **Automated hash generation** during GitHub release workflow
2. **Public hash repository** accessible via HTTPS for runtime verification
3. **Multi-tier verification** with user-friendly error messages
4. **Graceful degradation** when hashes are unavailable

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│ GitHub Actions Workflow (release-4-build.yml)              │
│                                                             │
│  1. Build provider DLLs                                    │
│  2. Calculate SHA-256 for each DLL                         │
│  3. Create hashes.json manifest                            │
│  4. Commit to gh-pages branch                              │
│  5. Upload to release assets                               │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│ Public Hash Repository                                      │
│                                                             │
│  https://yourorg.github.io/SmartHopper/hashes/v1.2.3.json │
│                                                             │
│  {                                                          │
│    "version": "v1.2.3",                                    │
│    "generated": "2026-02-15T10:00:00Z",                   │
│    "providers": {                                          │
│      "SmartHopper.Providers.OpenAI.dll": "abc123...",    │
│      "SmartHopper.Providers.MistralAI.dll": "def456..."  │
│    }                                                        │
│  }                                                          │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│ ProviderManager.LoadProviderAssemblyAsync()                │
│                                                             │
│  1. Calculate local DLL SHA-256                            │
│  2. Fetch public hash from GitHub                          │
│  3. Compare hashes                                         │
│  4. Apply security policy:                                 │
│     • Match → Enable (with user trust prompt)             │
│     • Mismatch → Error dialog (security warning)          │
│     • Unavailable → Warning (proceed with caution)        │
└─────────────────────────────────────────────────────────────┘
```

---

## 1. GitHub Workflow Implementation

### 1.1 Add SHA-256 Calculation Step

**Location**: `.github/workflows/release-4-build.yml`  
**Insert After**: "Create ZIP Archives" step (line ~298)

```yaml
- name: Calculate Provider SHA-256 Hashes
  if: steps.determine_version.outputs.IS_RELEASE == 'true'
  id: calculate_hashes
  shell: pwsh
  run: |
    $version = "${{ steps.determine_version.outputs.VERSION }}"
    $platforms = @("net7.0-windows", "net7.0")
    $hashManifest = @{
      version = $version
      generated = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
      providers = @{}
    }
    
    foreach ($platform in $platforms) {
      $platformPath = Join-Path "artifacts" $platform
      
      if (Test-Path $platformPath) {
        Write-Host "Calculating hashes for platform: $platform"
        
        # Find all provider DLLs
        $providerDlls = Get-ChildItem -Path $platformPath -Filter "SmartHopper.Providers.*.dll" -File
        
        foreach ($dll in $providerDlls) {
          # Calculate SHA-256 hash
          $hash = Get-FileHash -Path $dll.FullName -Algorithm SHA256
          $hashValue = $hash.Hash.ToLower()
          
          # Store in manifest with platform suffix for cross-platform tracking
          $key = "$($dll.Name)-$platform"
          $hashManifest.providers[$key] = $hashValue
          
          Write-Host "  $($dll.Name): $hashValue"
        }
      }
    }
    
    # Convert to JSON and save
    $hashJson = ConvertTo-Json -InputObject $hashManifest -Depth 10
    $hashFile = "provider-hashes-$version.json"
    Set-Content -Path $hashFile -Value $hashJson -Encoding UTF8
    
    Write-Host "Hash manifest saved to: $hashFile"
    Get-Content $hashFile | Write-Host
    
    echo "HASH_FILE=$hashFile" >> $env:GITHUB_OUTPUT
```

### 1.2 Upload Hash Manifest to Release

**Insert After**: Calculate hashes step

```yaml
- name: Upload Provider Hash Manifest to Release
  if: steps.determine_version.outputs.IS_RELEASE == 'true'
  uses: actions/upload-release-asset@v1
  env:
    GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
  with:
    upload_url: ${{ github.event_name == 'release' && github.event.release.upload_url || steps.get_release_url.outputs.UPLOAD_URL }}
    asset_path: ${{ steps.calculate_hashes.outputs.HASH_FILE }}
    asset_name: ${{ steps.calculate_hashes.outputs.HASH_FILE }}
    asset_content_type: application/json
```

### 1.3 Commit Hash to gh-pages Branch

**Insert After**: Upload hash manifest step

```yaml
- name: Publish Hash Manifest to GitHub Pages
  if: steps.determine_version.outputs.IS_RELEASE == 'true'
  shell: pwsh
  run: |
    $version = "${{ steps.determine_version.outputs.VERSION }}"
    $hashFile = "${{ steps.calculate_hashes.outputs.HASH_FILE }}"
    
    # Configure git
    git config --global user.name "github-actions[bot]"
    git config --global user.email "github-actions[bot]@users.noreply.github.com"
    
    # Checkout gh-pages branch (create if doesn't exist)
    git fetch origin gh-pages:gh-pages || git checkout --orphan gh-pages
    git checkout gh-pages
    
    # Create hashes directory structure
    New-Item -ItemType Directory -Force -Path "hashes"
    
    # Copy hash file with version-specific name
    Copy-Item $hashFile -Destination "hashes/$version.json" -Force
    
    # Also copy as latest.json for convenience
    Copy-Item $hashFile -Destination "hashes/latest.json" -Force
    
    # Commit and push
    git add hashes/
    git commit -m "Add provider hashes for $version" || echo "No changes to commit"
    git push origin gh-pages
    
    Write-Host "Hash manifest published to: https://${{ github.repository_owner }}.github.io/${{ github.event.repository.name }}/hashes/$version.json"
```

---

## 2. Public Hash Repository Structure

### 2.1 GitHub Pages Setup

1. **Enable GitHub Pages** for the repository:
   - Settings → Pages → Source: `gh-pages` branch
   - Root directory: `/`

2. **Hash manifest will be accessible at**:
   ```
   https://yourorg.github.io/SmartHopper/hashes/v1.2.3.json
   https://yourorg.github.io/SmartHopper/hashes/latest.json
   ```

### 2.2 Hash Manifest Schema

```json
{
  "version": "v1.2.3",
  "generated": "2026-02-15T10:00:00Z",
  "providers": {
    "SmartHopper.Providers.OpenAI.dll-net7.0-windows": "a1b2c3d4...",
    "SmartHopper.Providers.OpenAI.dll-net7.0": "e5f6g7h8...",
    "SmartHopper.Providers.MistralAI.dll-net7.0-windows": "i9j0k1l2...",
    "SmartHopper.Providers.MistralAI.dll-net7.0": "m3n4o5p6..."
  }
}
```

**Key Design Decisions**:
- Platform-specific keys (`dll-platform`) to track cross-platform differences
- ISO 8601 timestamp for generation time
- Lowercase SHA-256 hashes for consistency

---

## 3. Code Implementation

### 3.1 Create ProviderHashVerifier Class

**Location**: `src/SmartHopper.Infrastructure/AIProviders/ProviderHashVerifier.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Verifies provider DLL integrity using SHA-256 hashes from public repository.
    /// </summary>
    internal class ProviderHashVerifier
    {
        private const string HashBaseUrl = "https://yourorg.github.io/SmartHopper/hashes";
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        /// <summary>
        /// Verification result containing hash comparison status and details.
        /// </summary>
        public class VerificationResult
        {
            public bool Success { get; set; }
            public VerificationStatus Status { get; set; }
            public string LocalHash { get; set; }
            public string PublicHash { get; set; }
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// Hash verification status codes.
        /// </summary>
        public enum VerificationStatus
        {
            /// <summary>Hash matches - provider is authentic</summary>
            Match,
            
            /// <summary>Hash mismatch - potential security issue</summary>
            Mismatch,
            
            /// <summary>Public hash unavailable - network or source issue</summary>
            Unavailable,
            
            /// <summary>Hash not found in public manifest</summary>
            NotFound
        }

        /// <summary>
        /// Calculates SHA-256 hash of a file.
        /// </summary>
        private static string CalculateFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// Fetches public hash manifest from GitHub Pages.
        /// </summary>
        private static async Task<Dictionary<string, string>> FetchPublicHashesAsync(string version)
        {
            try
            {
                // Try version-specific hash first, fall back to latest
                string[] urls = 
                {
                    $"{HashBaseUrl}/{version}.json",
                    $"{HashBaseUrl}/latest.json"
                };

                foreach (var url in urls)
                {
                    try
                    {
                        Debug.WriteLine($"[ProviderHashVerifier] Fetching hashes from: {url}");
                        var response = await HttpClient.GetStringAsync(url).ConfigureAwait(false);
                        var manifest = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                        
                        if (manifest.ContainsKey("providers"))
                        {
                            var providersJson = JsonConvert.SerializeObject(manifest["providers"]);
                            return JsonConvert.DeserializeObject<Dictionary<string, string>>(providersJson);
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        Debug.WriteLine($"[ProviderHashVerifier] Failed to fetch from {url}: {ex.Message}");
                        continue;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProviderHashVerifier] Error fetching public hashes: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Verifies a provider DLL against public hash manifest.
        /// </summary>
        public static async Task<VerificationResult> VerifyProviderAsync(string dllPath, string version, string platform)
        {
            var result = new VerificationResult();

            try
            {
                // Calculate local hash
                result.LocalHash = CalculateFileHash(dllPath);
                Debug.WriteLine($"[ProviderHashVerifier] Local hash for {Path.GetFileName(dllPath)}: {result.LocalHash}");

                // Fetch public hashes
                var publicHashes = await FetchPublicHashesAsync(version).ConfigureAwait(false);

                if (publicHashes == null)
                {
                    result.Status = VerificationStatus.Unavailable;
                    result.ErrorMessage = "Failed to retrieve public hash manifest. This may be due to network connectivity issues or source unavailability.";
                    Debug.WriteLine($"[ProviderHashVerifier] Public hashes unavailable for version {version}");
                    return result;
                }

                // Lookup hash with platform-specific key
                string dllName = Path.GetFileName(dllPath);
                string hashKey = $"{dllName}-{platform}";

                if (!publicHashes.TryGetValue(hashKey, out string publicHash))
                {
                    // Try without platform suffix for backward compatibility
                    if (!publicHashes.TryGetValue(dllName, out publicHash))
                    {
                        result.Status = VerificationStatus.NotFound;
                        result.ErrorMessage = $"Hash not found in public manifest for {dllName} ({platform})";
                        Debug.WriteLine($"[ProviderHashVerifier] Hash not found for {hashKey}");
                        return result;
                    }
                }

                result.PublicHash = publicHash;

                // Compare hashes
                if (string.Equals(result.LocalHash, result.PublicHash, StringComparison.OrdinalIgnoreCase))
                {
                    result.Success = true;
                    result.Status = VerificationStatus.Match;
                    Debug.WriteLine($"[ProviderHashVerifier] Hash match for {dllName}");
                }
                else
                {
                    result.Status = VerificationStatus.Mismatch;
                    result.ErrorMessage = $"SHA-256 hash mismatch detected for {dllName}. Expected: {publicHash}, Actual: {result.LocalHash}";
                    Debug.WriteLine($"[ProviderHashVerifier] Hash mismatch for {dllName}");
                }
            }
            catch (Exception ex)
            {
                result.Status = VerificationStatus.Unavailable;
                result.ErrorMessage = $"Error during hash verification: {ex.Message}";
                Debug.WriteLine($"[ProviderHashVerifier] Verification error: {ex.Message}");
            }

            return result;
        }
    }
}
```

### 3.2 Integrate into ProviderManager

**Location**: `src/SmartHopper.Infrastructure/AIProviders/ProviderManager.cs`

**Modify `LoadProviderAssemblyAsync()` method** - Insert after Authenticode verification:

```csharp
// SHA-256 hash verification (for enhanced security, especially on non-Windows platforms)
try
{
    // Determine current platform
    string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
        ? "net7.0-windows" 
        : "net7.0";
    
    // Get version from assembly metadata or settings
    string version = GetSmartHopperVersion(); // Implement this helper
    
    var hashResult = await ProviderHashVerifier.VerifyProviderAsync(assemblyPath, version, platform)
        .ConfigureAwait(false);
    
    switch (hashResult.Status)
    {
        case ProviderVerificationStatus.Match:
            Debug.WriteLine($"[ProviderManager] SHA-256 verification passed for {Path.GetFileName(assemblyPath)}");
            break;
            
        case ProviderVerificationStatus.Mismatch:
            // CRITICAL: Hash mismatch indicates potential tampering
            await Task.Run(() => RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                StyledMessageDialog.ShowError(
                    $"SECURITY WARNING: Provider '{Path.GetFileName(assemblyPath)}' failed integrity verification.\n\n" +
                    $"The file's SHA-256 hash does not match the published hash from official sources. " +
                    $"This could indicate file corruption or tampering.\n\n" +
                    $"Expected: {hashResult.PublicHash}\n" +
                    $"Actual: {hashResult.LocalHash}\n\n" +
                    $"Please re-download the provider from official SmartHopper sources.",
                    "Security Warning - SmartHopper"
                );
            }))).ConfigureAwait(false);
            return;
            
        case ProviderVerificationStatus.Unavailable:
        case ProviderVerificationStatus.NotFound:
            // WARNING: Cannot verify - proceed with caution
            await Task.Run(() => RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                var warningMessage = hashResult.Status == ProviderVerificationStatus.Unavailable
                    ? $"WARNING: Could not retrieve SHA-256 hash for '{Path.GetFileName(assemblyPath)}' from public repository.\n\n" +
                      $"This may be due to:\n" +
                      $"• Network connectivity issues\n" +
                      $"• Public hash repository unavailability\n" +
                      $"• Firewall blocking HTTPS requests\n\n" +
                      $"Hash verification will be skipped. Ensure you trust this provider's source before enabling it."
                    : $"WARNING: SHA-256 hash for '{Path.GetFileName(assemblyPath)}' not found in public repository.\n\n" +
                      $"This provider may be:\n" +
                      $"• A custom/third-party provider\n" +
                      $"• From a different SmartHopper version\n" +
                      $"• Not yet published to the hash repository\n\n" +
                      $"Ensure you trust this provider's source before enabling it.";
                
                RhinoApp.WriteLine(warningMessage);
                
                // Optional: Show user dialog for hash verification failures
                // Uncomment if you want interactive warning:
                // StyledMessageDialog.ShowWarning(warningMessage, "Provider Verification - SmartHopper");
            }))).ConfigureAwait(false);
            break;
    }
}
catch (Exception ex)
{
    Debug.WriteLine($"[ProviderManager] SHA-256 verification error for {assemblyPath}: {ex.Message}");
    // Continue loading - don't block on verification errors
}
```

### 3.3 Add Version Helper Method

**Location**: `src/SmartHopper.Infrastructure/AIProviders/ProviderManager.cs`

```csharp
/// <summary>
/// Gets the SmartHopper version for hash manifest lookup.
/// </summary>
private string GetSmartHopperVersion()
{
    try
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        
        // Convert to semantic version format (v1.2.3)
        return $"v{version.Major}.{version.Minor}.{version.Build}";
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[ProviderManager] Error getting version: {ex.Message}");
        return "latest"; // Fallback to latest manifest
    }
}
```

---

## 4. User Experience Flows

### 4.1 Hash Match (Success)

```
1. ProviderManager detects new provider DLL
2. Calculates local SHA-256
3. Fetches public hash from GitHub
4. Hashes match ✓
5. Shows trust prompt: "Detected new AI provider 'OpenAI'. Enable it?"
6. User clicks Yes → Provider enabled
```

**Log Output**:
```
[ProviderManager] SHA-256 verification passed for SmartHopper.Providers.OpenAI.dll
[ProviderHashVerifier] Hash match for SmartHopper.Providers.OpenAI.dll
```

### 4.2 Hash Mismatch (Critical Error)

```
1. ProviderManager detects provider DLL
2. Calculates local SHA-256
3. Fetches public hash from GitHub
4. Hashes DO NOT match ✗
5. Shows ERROR dialog:
   
   ┌─────────────────────────────────────────────┐
   │ SECURITY WARNING                            │
   │                                             │
   │ Provider 'OpenAI' failed integrity          │
   │ verification.                               │
   │                                             │
   │ The file's SHA-256 hash does not match the │
   │ published hash. This could indicate file   │
   │ corruption or tampering.                    │
   │                                             │
   │ Expected: a1b2c3d4...                      │
   │ Actual: x9y8z7w6...                        │
   │                                             │
   │ Please re-download from official sources.   │
   │                                             │
   │              [ OK ]                         │
   └─────────────────────────────────────────────┘
   
6. Provider NOT loaded
```

### 4.3 Hash Unavailable (Warning)

```
1. ProviderManager detects provider DLL
2. Calculates local SHA-256
3. Attempts to fetch public hash from GitHub
4. Network error / source unavailable
5. Shows WARNING in Rhino command line:
   
   "WARNING: Could not retrieve SHA-256 hash for 'OpenAI'
    from public repository. This may be due to network
    connectivity issues. Hash verification skipped.
    Ensure you trust this provider's source."
   
6. Shows trust prompt: "Detected new AI provider 'OpenAI'. Enable it?"
7. User decides whether to trust
```

### 4.4 Hash Not Found (Warning)

```
1. ProviderManager detects provider DLL
2. Calculates local SHA-256
3. Fetches public hash manifest successfully
4. DLL not found in manifest (custom/third-party provider)
5. Shows WARNING in Rhino command line:
   
   "WARNING: SHA-256 hash for 'CustomProvider' not found
    in public repository. This may be a custom/third-party
    provider. Ensure you trust this provider's source."
   
6. Shows trust prompt: "Detected new AI provider 'CustomProvider'. Enable it?"
7. User decides whether to trust
```

---

## 5. Security Considerations

### 5.1 Threat Model

**Threats Mitigated**:
- ✅ **Tampered provider DLLs**: Hash mismatch detection prevents execution
- ✅ **Man-in-the-middle attacks on downloads**: Public hash from HTTPS source provides verification
- ✅ **Corrupted files**: Integrity check detects accidental corruption

**Threats NOT Mitigated**:
- ❌ **Compromised GitHub account**: If attacker controls the repository, they can publish matching hashes
- ❌ **Supply chain attacks**: If build pipeline is compromised, malicious DLLs get legitimate hashes
- ❌ **Local privilege escalation**: Attacker with local write access can modify both DLL and hash file

### 5.2 Defense-in-Depth Layers

1. **Layer 1**: Strong-name token matching (identity verification)
2. **Layer 2**: Authenticode signature (Windows only - cryptographic verification)
3. **Layer 3**: SHA-256 hash verification (all platforms - integrity verification)
4. **Layer 4**: User trust prompt (manual approval)

### 5.3 Recommendations

1. **Enable GitHub branch protection** for `gh-pages` branch
2. **Require code review** for workflow changes
3. **Use GitHub Actions environments** with approval gates for releases
4. **Consider code signing certificates** for additional authenticity
5. **Monitor hash manifest access** via GitHub Pages analytics
6. **Implement hash rotation** on security incidents

---

## 6. Testing Plan

### 6.1 Unit Tests

**File**: `src/SmartHopper.Infrastructure.Test/AIProviders/ProviderHashVerifierTests.cs`

```csharp
[Fact]
public void CalculateFileHash_SameFile_SameHash()
{
    // Create test DLL
    // Calculate hash twice
    // Assert hashes match
}

[Fact]
public async Task VerifyProvider_MatchingHash_ReturnsSuccess()
{
    // Mock HTTP response with matching hash
    // Call VerifyProviderAsync
    // Assert Success = true, Status = Match
}

[Fact]
public async Task VerifyProvider_MismatchingHash_ReturnsMismatch()
{
    // Mock HTTP response with different hash
    // Call VerifyProviderAsync
    // Assert Status = Mismatch
}

[Fact]
public async Task VerifyProvider_UnavailableSource_ReturnsUnavailable()
{
    // Mock HTTP failure
    // Call VerifyProviderAsync
    // Assert Status = Unavailable
}
```

### 6.2 Integration Tests

1. **Workflow Test**: Trigger manual build, verify hash manifest created
2. **GitHub Pages Test**: Verify hash manifest accessible via HTTPS
3. **Provider Loading Test**: Install provider, verify hash check executes
4. **Network Failure Test**: Disconnect network, verify graceful degradation
5. **Hash Mismatch Test**: Modify DLL, verify error dialog appears

### 6.3 Manual Testing Checklist

- [ ] Build workflow generates hash manifest correctly
- [ ] Hash manifest uploaded to release assets
- [ ] Hash manifest published to GitHub Pages
- [ ] Hash manifest accessible via public URL
- [ ] Provider with matching hash loads successfully
- [ ] Provider with mismatched hash shows error and blocks loading
- [ ] Network failure shows warning but allows user choice
- [ ] Custom provider (not in manifest) shows warning but allows user choice
- [ ] macOS platform-specific hashes work correctly
- [ ] Windows platform-specific hashes work correctly

---

## 7. Deployment Checklist

### Pre-Deployment

- [ ] Review and test workflow changes in a test repository
- [ ] Enable GitHub Pages for production repository
- [ ] Configure branch protection for `gh-pages` branch
- [ ] Update repository URL in `ProviderHashVerifier.cs`
- [ ] Add comprehensive unit tests
- [ ] Test on both Windows and macOS

### Deployment

- [ ] Merge workflow changes to `main` branch
- [ ] Create new release to trigger workflow
- [ ] Verify hash manifest appears in release assets
- [ ] Verify hash manifest published to GitHub Pages
- [ ] Test public URL accessibility

### Post-Deployment

- [ ] Monitor error rates in application logs
- [ ] Monitor hash manifest access patterns
- [ ] Document for users in README
- [ ] Update security documentation
- [ ] Train support team on new error messages

---

## 8. Future Enhancements

### 8.1 Short-Term (Next Release)

1. **Hash caching**: Cache fetched hashes to reduce network calls
2. **Offline mode**: Bundle recent hash manifests with installer
3. **User notifications**: Inform users when hash verification adds security
4. **Metrics collection**: Track verification success/failure rates

### 8.2 Medium-Term (Next Quarter)

1. **Multi-source verification**: Verify from multiple CDNs/mirrors
2. **Signed hash manifests**: Digitally sign JSON manifests
3. **Provider catalog**: Central registry with additional metadata
4. **Auto-update**: Suggest updates when newer verified versions exist

### 8.3 Long-Term (Future)

1. **Blockchain verification**: Immutable hash records
2. **Reproducible builds**: Enable community verification
3. **Transparency logs**: Public audit trail of all releases
4. **Zero-trust architecture**: Continuous runtime verification

---

## 9. References

- **OWASP Top 10**: https://owasp.org/www-project-top-ten/
- **NIST SP 800-218**: Secure Software Development Framework (SSDF)
- **GitHub Actions Security**: https://docs.github.com/en/actions/security-guides
- **SHA-256 Algorithm**: FIPS 180-4 specification
- **macOS Code Signing**: https://developer.apple.com/documentation/security/code_signing_services

---

## Appendix A: Sample Hash Manifest

**File**: `hashes/v1.2.3.json`

```json
{
  "version": "v1.2.3",
  "generated": "2026-02-15T10:30:45Z",
  "algorithm": "SHA-256",
  "providers": {
    "SmartHopper.Providers.OpenAI.dll-net7.0-windows": "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6",
    "SmartHopper.Providers.OpenAI.dll-net7.0": "b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6a1",
    "SmartHopper.Providers.MistralAI.dll-net7.0-windows": "c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6a1b2",
    "SmartHopper.Providers.MistralAI.dll-net7.0": "d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6a1b2c3",
    "SmartHopper.Providers.DeepSeek.dll-net7.0-windows": "e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6a1b2c3d4",
    "SmartHopper.Providers.DeepSeek.dll-net7.0": "f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6a1b2c3d4e5"
  },
  "metadata": {
    "buildNumber": "123",
    "commitSha": "abc123def456",
    "releaseUrl": "https://github.com/yourorg/SmartHopper/releases/tag/v1.2.3"
  }
}
```

---

## Appendix B: Error Messages

### Critical Error (Hash Mismatch)

```
Title: Security Warning - SmartHopper

SECURITY WARNING: Provider 'SmartHopper.Providers.OpenAI.dll' failed integrity verification.

The file's SHA-256 hash does not match the published hash from official sources. This could indicate file corruption or tampering.

Expected: a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0u1v2w3x4y5z6
Actual: x9y8z7w6v5u4t3s2r1q0p9o8n7m6l5k4j3i2h1g0f9e8d7c6b5a4

Please re-download the provider from official SmartHopper sources.

[OK]
```

### Warning (Hash Unavailable)

```
WARNING: Could not retrieve SHA-256 hash for 'SmartHopper.Providers.OpenAI.dll' from public repository.

This may be due to:
• Network connectivity issues
• Public hash repository unavailability
• Firewall blocking HTTPS requests

Hash verification will be skipped. Ensure you trust this provider's source before enabling it.
```

### Warning (Hash Not Found)

```
WARNING: SHA-256 hash for 'SmartHopper.Providers.CustomProvider.dll' not found in public repository.

This provider may be:
• A custom/third-party provider
• From a different SmartHopper version
• Not yet published to the hash repository

Ensure you trust this provider's source before enabling it.
```

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-15 | Code Review (Cascade) | Initial review document |

---

**END OF DOCUMENT**
