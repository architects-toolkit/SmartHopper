/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// High-level classification of a provider assembly as determined by the
    /// cryptographic checks in <see cref="ProviderClassifier"/>.
    ///
    /// <para>
    /// Classification is based purely on strong-name token, Authenticode certificate
    /// (Windows only), and the SmartHopper-managed SHA-256 hash manifest. It deliberately
    /// ignores provider id, assembly file name, and any metadata claim — those can be
    /// trivially spoofed and provide no security guarantee.
    /// </para>
    /// </summary>
    public enum ProviderClassification
    {
        /// <summary>
        /// The provider is cryptographically attributable to SmartHopper itself: it
        /// passes strong-name + Authenticode verification (where applicable) and/or
        /// matches the official hash manifest, with no contradicting signals.
        /// </summary>
        Official,

        /// <summary>
        /// The provider claims to be official by one signal (e.g. hash listed in the
        /// manifest) but another signal contradicts it (e.g. signature mismatch, or
        /// hash mismatch). Always blocked.
        /// </summary>
        OfficialTampered,

        /// <summary>
        /// The provider is a valid managed assembly that is NOT cryptographically tied
        /// to SmartHopper. Includes both signed-by-other-key and unsigned third-party
        /// DLLs.
        /// </summary>
        Community,

        /// <summary>
        /// The file is not a valid managed assembly, fails SDK type identity, has no
        /// <c>IAIProviderFactory</c>, has malformed metadata, or could not be loaded.
        /// Always blocked.
        /// </summary>
        Invalid,
    }

    /// <summary>
    /// Result of a single <see cref="ProviderClassifier.ClassifyAsync"/> call.
    /// </summary>
    public sealed class ProviderClassificationResult
    {
        /// <summary>The final classification.</summary>
        public ProviderClassification Classification { get; init; }

        /// <summary>The SHA-256 hash of the file (hex, lower-case), or <c>null</c> if not computed.</summary>
        public string? Sha256 { get; init; }

        /// <summary>Strong-name public key token (hex), or <c>null</c> if unsigned/unreadable.</summary>
        public string? StrongNameToken { get; init; }

        /// <summary>Whether the strong-name token matches the host's token.</summary>
        public bool StrongNameMatchesHost { get; init; }

        /// <summary>Authenticode signer subject (Windows only), or <c>null</c>.</summary>
        public string? AuthenticodeSubject { get; init; }

        /// <summary>Authenticode signer SHA-1 thumbprint (Windows only), or <c>null</c>.</summary>
        public string? AuthenticodeThumbprint { get; init; }

        /// <summary>Whether the Authenticode signature matches the host's certificate.</summary>
        public bool AuthenticodeMatchesHost { get; init; }

        /// <summary>Result of the official-hash-manifest lookup, when performed.</summary>
        public ProviderVerificationStatus HashStatus { get; init; }

        /// <summary>Diagnostic message for logs and dialogs.</summary>
        public string Diagnostic { get; init; } = string.Empty;
    }

    /// <summary>
    /// Cryptographic classifier for provider assemblies. Returns a
    /// <see cref="ProviderClassificationResult"/> the caller can act on — it never
    /// throws to block loading, and it never trusts file names or assembly metadata.
    /// </summary>
    public static class ProviderClassifier
    {
        /// <summary>
        /// Classify the provider assembly at <paramref name="assemblyPath"/>.
        /// </summary>
        /// <param name="assemblyPath">Full path to the candidate provider DLL.</param>
        public static async Task<ProviderClassificationResult> ClassifyAsync(string assemblyPath)
        {
            if (assemblyPath is null)
            {
                throw new ArgumentNullException(nameof(assemblyPath));
            }

            if (!File.Exists(assemblyPath))
            {
                return new ProviderClassificationResult
                {
                    Classification = ProviderClassification.Invalid,
                    Diagnostic = $"Provider file not found: {assemblyPath}",
                };
            }

            string? sha256 = null;
            try
            {
                sha256 = ProviderHashVerifier.CalculateFileHash(assemblyPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProviderClassifier] Failed to hash {assemblyPath}: {ex.Message}");
            }

            string? token = null;
            bool tokenMatchesHost = false;
            try
            {
                var asmName = AssemblyName.GetAssemblyName(assemblyPath);
                var rawToken = asmName.GetPublicKeyToken();
                if (rawToken is { Length: > 0 })
                {
                    token = BitConverter.ToString(rawToken).Replace("-", string.Empty).ToLowerInvariant();
                    var hostToken = typeof(ProviderClassifier).Assembly.GetName().GetPublicKeyToken();
                    if (hostToken is { Length: > 0 })
                    {
                        tokenMatchesHost = rawToken.SequenceEqual(hostToken);
                    }
                }
            }
            catch (BadImageFormatException ex)
            {
                return new ProviderClassificationResult
                {
                    Classification = ProviderClassification.Invalid,
                    Sha256 = sha256,
                    Diagnostic = $"Provider '{Path.GetFileName(assemblyPath)}' is not a valid managed assembly: {ex.Message}",
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProviderClassifier] Failed to read strong name for {assemblyPath}: {ex.Message}");
            }

            string? authSubject = null;
            string? authThumb = null;
            bool authMatchesHost = false;
            bool authSignaturePresent = false;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(assemblyPath));
                    authSubject = cert.Subject;
                    authThumb = cert.Thumbprint;
                    authSignaturePresent = true;

                    try
                    {
                        var hostCert = new X509Certificate2(X509Certificate.CreateFromSignedFile(
                            Assembly.GetExecutingAssembly().Location));
                        authMatchesHost = string.Equals(cert.Thumbprint, hostCert.Thumbprint, StringComparison.OrdinalIgnoreCase);
                    }
                    catch (Exception)
                    {
                        // Host not signed (e.g. local dev build); cannot compare.
                        authMatchesHost = false;
                    }
                }
                catch (CryptographicException)
                {
                    // No Authenticode signature; treat as unsigned.
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProviderClassifier] Authenticode read error for {assemblyPath}: {ex.Message}");
                }
            }

            // Official hash manifest lookup (cross-platform).
            var hashStatus = ProviderVerificationStatus.NotFound;
            try
            {
                var platform = VersionHelper.GetPlatform();
                var version = VersionHelper.GetDisplayVersion();
                var hashResult = await ProviderHashVerifier.VerifyProviderAsync(assemblyPath, version, platform).ConfigureAwait(false);
                hashStatus = hashResult.Status;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProviderClassifier] Hash manifest lookup failed for {assemblyPath}: {ex.Message}");
            }

            // Tampered: any positive signal of "official" combined with a negative crypto signal.
            bool hashOnAllowList = hashStatus == ProviderVerificationStatus.Match;
            bool hashMismatch = hashStatus == ProviderVerificationStatus.Mismatch;

            if (hashOnAllowList && (token is not null && !tokenMatchesHost && tokenMatchesHost == false))
            {
                // Hash claims official but strong-name token disagrees — tampered.
                if (token is not null && !tokenMatchesHost)
                {
                    return new ProviderClassificationResult
                    {
                        Classification = ProviderClassification.OfficialTampered,
                        Sha256 = sha256,
                        StrongNameToken = token,
                        StrongNameMatchesHost = tokenMatchesHost,
                        AuthenticodeSubject = authSubject,
                        AuthenticodeThumbprint = authThumb,
                        AuthenticodeMatchesHost = authMatchesHost,
                        HashStatus = hashStatus,
                        Diagnostic = $"Provider '{Path.GetFileName(assemblyPath)}' is listed in the official hash manifest but its strong-name token does not match SmartHopper. The file has been tampered with.",
                    };
                }
            }

            if (hashMismatch && tokenMatchesHost)
            {
                // Strong-name matches SmartHopper but hash mismatches — tampered official build.
                return new ProviderClassificationResult
                {
                    Classification = ProviderClassification.OfficialTampered,
                    Sha256 = sha256,
                    StrongNameToken = token,
                    StrongNameMatchesHost = tokenMatchesHost,
                    AuthenticodeSubject = authSubject,
                    AuthenticodeThumbprint = authThumb,
                    AuthenticodeMatchesHost = authMatchesHost,
                    HashStatus = hashStatus,
                    Diagnostic = $"Provider '{Path.GetFileName(assemblyPath)}' is strong-named by SmartHopper but its SHA-256 hash does not match the published value. The file has been tampered with.",
                };
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && authSignaturePresent && !authMatchesHost && tokenMatchesHost)
            {
                // Authenticode says someone else signed it, but the strong-name token claims SmartHopper.
                return new ProviderClassificationResult
                {
                    Classification = ProviderClassification.OfficialTampered,
                    Sha256 = sha256,
                    StrongNameToken = token,
                    StrongNameMatchesHost = tokenMatchesHost,
                    AuthenticodeSubject = authSubject,
                    AuthenticodeThumbprint = authThumb,
                    AuthenticodeMatchesHost = authMatchesHost,
                    HashStatus = hashStatus,
                    Diagnostic = $"Provider '{Path.GetFileName(assemblyPath)}' carries SmartHopper's strong-name token but its Authenticode certificate does not match. The file has been tampered with.",
                };
            }

            // Official: any positive crypto signal, with no contradicting signal.
            bool authOfficialOnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && authMatchesHost;
            if (tokenMatchesHost || authOfficialOnWindows || hashOnAllowList)
            {
                return new ProviderClassificationResult
                {
                    Classification = ProviderClassification.Official,
                    Sha256 = sha256,
                    StrongNameToken = token,
                    StrongNameMatchesHost = tokenMatchesHost,
                    AuthenticodeSubject = authSubject,
                    AuthenticodeThumbprint = authThumb,
                    AuthenticodeMatchesHost = authMatchesHost,
                    HashStatus = hashStatus,
                    Diagnostic = $"Provider '{Path.GetFileName(assemblyPath)}' classified as Official.",
                };
            }

            // Community: not an official signal, but still a valid managed assembly.
            return new ProviderClassificationResult
            {
                Classification = ProviderClassification.Community,
                Sha256 = sha256,
                StrongNameToken = token,
                StrongNameMatchesHost = tokenMatchesHost,
                AuthenticodeSubject = authSubject,
                AuthenticodeThumbprint = authThumb,
                AuthenticodeMatchesHost = authMatchesHost,
                HashStatus = hashStatus,
                Diagnostic = $"Provider '{Path.GetFileName(assemblyPath)}' classified as Community (not signed by SmartHopper).",
            };
        }
    }
}
