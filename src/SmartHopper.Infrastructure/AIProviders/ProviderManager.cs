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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Rhino;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Security;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.Infrastructure.Utils;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Logging;
using SmartHopper.ProviderSdk.Settings;
using SmartHopper.ProviderSdk.Utils;
namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Manages the discovery and loading of AI providers.
    /// </summary>
    public class ProviderManager
    {
        private static readonly Lazy<ProviderManager> _instance = new Lazy<ProviderManager>(() => new ProviderManager());

        public static ProviderManager Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, IAIProvider> _providers = new ConcurrentDictionary<string, IAIProvider>();
        private readonly ConcurrentDictionary<string, IAIProviderSettings> _providerSettings = new ConcurrentDictionary<string, IAIProviderSettings>();
        private readonly ConcurrentDictionary<string, Assembly> _providerAssemblies = new ConcurrentDictionary<string, Assembly>();
        private volatile bool _refreshCompleted = false;
        private readonly ConcurrentDictionary<string, bool> _mismatchedProviders = new ConcurrentDictionary<string, bool>(); // Tracks providers with hash mismatches
        private readonly ConcurrentDictionary<string, bool> _unavailableProviders = new ConcurrentDictionary<string, bool>(); // Tracks providers where hash check was unavailable (network issues)
        private readonly ConcurrentDictionary<string, bool> _unknownProviders = new ConcurrentDictionary<string, bool>(); // Tracks providers not found in hash manifest (custom/third-party)
        private readonly ConcurrentDictionary<string, bool> _officialProviders = new ConcurrentDictionary<string, bool>(); // Tracks providers classified as official
        private readonly ConcurrentDictionary<string, bool> _unsignedProviders = new ConcurrentDictionary<string, bool>(); // Tracks providers loaded without a strong-name signature
        private bool _trustRecordsDirty; // Set when ProviderTrustRecords was written without an immediate Save()
        private volatile ProviderIntegrityIssue[]? _lastIntegrityFindings; // Latest integrity findings snapshot, for non-modal surfaces
        private readonly ProviderIntegrityReportGate _integrityReportGate = new ProviderIntegrityReportGate();

        private ProviderManager()
        {
            // NOTE: Do NOT automatically call RefreshProviders() here to avoid circular dependencies
            // RefreshProviders() should be called explicitly after initialization
        }

        /// <summary>
        /// Discovers and loads provider assemblies from the same directory as the main application.
        /// </summary>
        private async Task DiscoverProvidersAsync()
        {
            try
            {
                // Clear tracking sets to ensure fresh state when providers are refreshed
                this._mismatchedProviders.Clear();
                this._unavailableProviders.Clear();
                this._unknownProviders.Clear();

                // Get the directory where the current assembly is located
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                string baseDirectory = Path.GetDirectoryName(assemblyLocation);

                // Find all external provider DLLs
                string[] providerFiles = Directory.GetFiles(baseDirectory, "SmartHopper.Providers.*.dll");

                // Decide trust for all newly detected providers in one dialog before loading
                var settings = SmartHopperSettings.Instance;
                this._trustRecordsDirty = false;
                var untrustedFiles = providerFiles
                    .Where(f => !settings.TrustedProviders.ContainsKey(Path.GetFileNameWithoutExtension(f)))
                    .ToList();
                if (untrustedFiles.Count > 0)
                {
                    var candidates = await BuildNewProviderCandidatesAsync(untrustedFiles).ConfigureAwait(false);
                    var decisions = await PromptNewProvidersAsync(candidates).ConfigureAwait(false);
                    foreach (var candidate in candidates)
                    {
                        settings.TrustedProviders[candidate.AssemblyName] =
                            decisions.TryGetValue(candidate.AssemblyName, out var enabled) && enabled;

                        // Snapshot the bytes consented to — verification-conditional trust
                        // only applies while the file keeps matching them.
                        this.MintTrustRecord(
                            settings,
                            candidate.AssemblyName,
                            candidate.Verification?.LocalHash ?? TryComputeLocalHash(candidate.AssemblyPath),
                            candidate.Verification?.Status == ProviderVerificationStatus.Match);
                    }

                    settings.Save();
                    this._trustRecordsDirty = false;
                }

                // Integrity findings are collected and reported in one dialog after
                // loading instead of one modal per provider.
                var integrityIssues = new List<ProviderIntegrityIssue>();
                var reconsentPaths = new List<string>();
                foreach (string providerFile in providerFiles)
                {
                    try
                    {
                        if (await this.LoadProviderAssemblyAsync(providerFile, integrityIssues).ConfigureAwait(false) == ProviderLoadOutcome.NeedsReconsent)
                        {
                            reconsentPaths.Add(providerFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.For<ProviderManager>().Exception(ex, $"Error loading provider assembly {providerFile}: {ex.Message}");
                    }
                }

                // Trusted files whose bytes changed without verification get one batched
                // re-consent prompt — declined providers are disabled outright.
                if (reconsentPaths.Count > 0)
                {
                    var changed = await BuildNewProviderCandidatesAsync(reconsentPaths).ConfigureAwait(false);
                    foreach (var candidate in changed)
                    {
                        candidate.TrustNote = "Changed since you enabled it and still cannot be verified — re-confirm to keep it enabled.";
                    }

                    var decisions = await PromptNewProvidersAsync(changed, reconsent: true).ConfigureAwait(false);
                    foreach (var candidate in changed)
                    {
                        if (decisions.TryGetValue(candidate.AssemblyName, out var keep) && keep)
                        {
                            this.MintTrustRecord(
                                settings,
                                candidate.AssemblyName,
                                candidate.Verification?.LocalHash ?? TryComputeLocalHash(candidate.AssemblyPath),
                                candidate.Verification?.Status == ProviderVerificationStatus.Match);
                            settings.TrustedProviders[candidate.AssemblyName] = true;
                        }
                        else
                        {
                            settings.TrustedProviders[candidate.AssemblyName] = false;
                            settings.ProviderTrustRecords.Remove(candidate.AssemblyName);
                            this._trustRecordsDirty = true;
                        }
                    }

                    settings.Save();
                    this._trustRecordsDirty = false;

                    foreach (var candidate in changed)
                    {
                        if (settings.TrustedProviders.TryGetValue(candidate.AssemblyName, out var enabled) && enabled)
                        {
                            try
                            {
                                await this.LoadProviderAssemblyAsync(candidate.AssemblyPath, integrityIssues).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Logger.For<ProviderManager>().Exception(ex, $"Error loading provider assembly {candidate.AssemblyPath}: {ex.Message}");
                            }
                        }
                    }
                }

                // Persist silent re-mints (official updates, consent baselines) and
                // tamper-driven disables accumulated during the load pass.
                if (this._trustRecordsDirty)
                {
                    settings.Save();
                    this._trustRecordsDirty = false;
                }

                // Findings stay on the manager so non-modal surfaces (settings
                // banner, assistant no-provider error) can explain blocked state.
                this._lastIntegrityFindings = integrityIssues.Count > 0 ? integrityIssues.ToArray() : null;

                // One unified report per findings set — an unchanged set is not
                // re-shown on every refresh, a clean scan re-arms the gate.
                if (this._integrityReportGate.ShouldReport(integrityIssues))
                {
                    await ReportIntegrityIssuesAsync(integrityIssues).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.For<ProviderManager>().Exception(ex, $"Error discovering providers: {ex.Message}");
            }
        }

        /// <summary>
        /// Manually triggers discovery of external AI providers.
        /// </summary>
        public async Task RefreshProvidersAsync()
        {
            Logger.For<ProviderManager>().Trace("[ProviderManager] Starting provider discovery and registration");

            try
            {
                // Discover new providers
                await this.DiscoverProvidersAsync().ConfigureAwait(false);

                // After discovery, refresh settings for all providers
                Logger.For<ProviderManager>().Trace("[ProviderManager] Provider discovery complete, refreshing settings");
                SmartHopperSettings.Instance.RefreshProvidersLocalStorage();
                ProviderSdk.AICall.Validation.ProviderTrustPolicy.InvalidateCache();
            }
            finally
            {
                // Mark refresh as completed regardless of provider count or errors
                // This signals that infrastructure initialization is done
                this._refreshCompleted = true;
                Logger.For<ProviderManager>().Trace("[ProviderManager] Provider refresh completed (infrastructure ready)");
            }
        }

        /// <summary>
        /// Manually triggers discovery of external AI providers (synchronous wrapper).
        /// For backward compatibility. Prefer RefreshProvidersAsync() when possible.
        /// </summary>
        public void RefreshProviders()
        {
            // Run async method synchronously for backward compatibility
            // This is safe when called from non-UI threads
            this.RefreshProvidersAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Verifies all provider DLLs in the provider directory using SHA-256 hashes.
        /// </summary>
        /// <returns>Dictionary of DLL names to verification results</returns>
        public async Task<Dictionary<string, ProviderVerificationResult>> VerifyAllProvidersAsync()
        {
            try
            {
                // Get the directory where the current assembly is located (same as DiscoverProvidersAsync)
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                string baseDirectory = Path.GetDirectoryName(assemblyLocation);

                // Get platform and version
                string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "net7.0-windows"
                    : "net7.0";
                string version = VersionHelper.GetDisplayVersion();

                // Verify all providers
                return await ProviderHashVerifier.VerifyAllProvidersAsync(baseDirectory, version, platform)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.For<ProviderManager>().Exception(ex, $"[ProviderManager] Error verifying providers: {ex.Message}");
                return new Dictionary<string, ProviderVerificationResult>();
            }
            finally
            {
                ProviderSdk.AICall.Validation.ProviderTrustPolicy.InvalidateCache();
            }
        }

        /// <summary>Result of <see cref="LoadProviderAssemblyAsync"/> for one provider file.</summary>
        private enum ProviderLoadOutcome
        {
            /// <summary>The assembly was loaded and its providers registered.</summary>
            Loaded,

            /// <summary>The file was skipped or blocked without executing its code.</summary>
            Skipped,

            /// <summary>A trusted file's bytes changed without verification — consent must be re-confirmed.</summary>
            NeedsReconsent,
        }

        /// <summary>Decision produced by <see cref="EvaluateStoredTrust"/> for a trusted provider file.</summary>
        internal enum StoredTrustDecision
        {
            /// <summary>Verified official binary — consent follows silently; re-mint the record.</summary>
            LoadVerified,

            /// <summary>Unverified, but consent still applies: unchanged bytes or a fresh baseline.</summary>
            LoadUnverified,

            /// <summary>Trusted file fails the published hash — potential tampering; disable, block and alert.</summary>
            BlockTampered,

            /// <summary>Bytes changed since unverified consent — re-confirm with the user.</summary>
            NeedsReconsent,

            /// <summary>Bytes changed and the active mode does not allow re-consenting unverified providers.</summary>
            BlockUnverifiedChanged,
        }

        /// <summary>
        /// Decides how a trusted provider file may load given its verification status, its
        /// current bytes and the consent snapshot stored in <c>ProviderTrustRecords</c>.
        /// Pure — every input is a parameter so the whole matrix is unit-testable.
        /// </summary>
        /// <param name="status">Verification outcome; <see langword="null"/> when the check itself failed.</param>
        /// <param name="localHash">SHA-256 of the file on disk.</param>
        /// <param name="record">Consent snapshot; <see langword="null"/> for entries without one.</param>
        /// <param name="mode">User-selected integrity check mode.</param>
        /// <param name="debugBuild"><see langword="true"/> in DEBUG builds — local rebuilds keep trust silently and never re-prompt.</param>
        internal static StoredTrustDecision EvaluateStoredTrust(
            ProviderVerificationStatus? status,
            string? localHash,
            ProviderTrustRecord? record,
            ProviderIntegrityCheckMode mode,
            bool debugBuild)
        {
            switch (status)
            {
                case ProviderVerificationStatus.Match:
                    // Verified official binary — consent follows silent official updates.
                    return StoredTrustDecision.LoadVerified;

                case ProviderVerificationStatus.Mismatch:
                    // An official-name file with wrong bytes is potential tampering —
                    // except on DEBUG, where developer rebuilds mismatch by definition.
                    return debugBuild ? StoredTrustDecision.LoadUnverified : StoredTrustDecision.BlockTampered;
            }

            // NotFound / Unavailable / verification failure — unverified path.
            if (debugBuild)
            {
                // Local rebuilds change bytes constantly — never re-prompt on DEBUG.
                return StoredTrustDecision.LoadUnverified;
            }

            if (record == null
                || string.IsNullOrEmpty(record.ConsentedHash)
                || !string.Equals(record.ConsentedHash, localHash, StringComparison.OrdinalIgnoreCase))
            {
                // No consent baseline (legacy or forged entry) or bytes changed since
                // consent — Soft re-asks the user, Hard/Strict block the unverified file.
                return mode == ProviderIntegrityCheckMode.Soft
                    ? StoredTrustDecision.NeedsReconsent
                    : StoredTrustDecision.BlockUnverifiedChanged;
            }

            return StoredTrustDecision.LoadUnverified;
        }

        /// <summary>
        /// Whether the running build is a DEBUG build — passed into
        /// <see cref="EvaluateStoredTrust"/> so the compile-time check stays in one place.
        /// </summary>
        internal static bool IsDebugBuild
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Loads a provider assembly and registers any providers it contains.
        /// </summary>
        /// <param name="assemblyPath">The path to the provider assembly.</param>
        /// <param name="issues">Collects integrity findings for the caller's unified report — this method never shows UI.</param>
        /// <returns>The load outcome — files needing re-consent are handled by the caller.</returns>
        private async Task<ProviderLoadOutcome> LoadProviderAssemblyAsync(string assemblyPath, ICollection<ProviderIntegrityIssue> issues)
        {
            var asmName = Path.GetFileNameWithoutExtension(assemblyPath);
            var settings = SmartHopperSettings.Instance;

            // Disabled providers must never execute — skip before any verification or load.
            if (settings.TrustedProviders.TryGetValue(asmName, out var enabled) && !enabled)
            {
                Logger.For<ProviderManager>().Trace($"[ProviderManager] Provider '{asmName}' is disabled — skipping load.");
                return ProviderLoadOutcome.Skipped;
            }

            try
            {
                // Authenticode signature validation (Windows-only, skip on macOS)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        this.VerifySignature(assemblyPath);
                    }
                    catch (CryptographicException ex)
                    {
                        Logger.For<ProviderManager>().Exception(ex, $"Authenticode signature verification failed for {assemblyPath}: {ex.Message}");
                        issues.Add(new ProviderIntegrityIssue
                        {
                            FileName = Path.GetFileName(assemblyPath),
                            Severity = ProviderIntegrityIssueSeverity.Error,
                            Status = "Authenticode signature verification failed — provider blocked",
                            Detail = "Please replace it with a file downloaded from official SmartHopper sources.",
                        });
                        return ProviderLoadOutcome.Skipped;
                    }
                }
                else
                {
                    Logger.For<ProviderManager>().Warning($"[ProviderManager] Skipping Authenticode verification on non-Windows platform for {Path.GetFileName(assemblyPath)}");
                }

                // SHA-256 hash verification (cross-platform)
                // In DEBUG builds, force soft check mode to allow local development
                // In RELEASE builds, use the configured integrity check mode
                ProviderVerificationResult? hashResult = null;
                string platform = VersionHelper.GetPlatform();
                try
                {
                    hashResult = await ProviderHashVerifier.VerifyProviderAsync(assemblyPath, VersionHelper.GetDisplayVersion(), platform)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.For<ProviderManager>().Exception(ex, $"[ProviderManager] SHA-256 verification error for {assemblyPath}: {ex.Message}");

                    // Continue — a failed check is itself unverified state below.
                }

                var effectiveMode = SmartHopperSettings.Instance.EffectiveProviderIntegrityCheckMode;

                // Stored-trust evaluation: consent binds to the bytes consented to, not
                // the assembly name — a trusted file is re-verified on every load, even
                // when the catalogue check itself failed. Runs before the mode gate so
                // a tampered trusted file can never ride Soft mode's warn-and-load path.
                if (settings.TrustedProviders.TryGetValue(asmName, out var trusted) && trusted)
                {
                    var localHash = hashResult?.LocalHash ?? TryComputeLocalHash(assemblyPath);
                    settings.ProviderTrustRecords.TryGetValue(asmName, out var trustRecord);
                    var trustDecision = EvaluateStoredTrust(
                        hashResult?.Status, localHash, trustRecord, effectiveMode, IsDebugBuild);

                    switch (trustDecision)
                    {
                        case StoredTrustDecision.BlockTampered:
                            // Disable so the file stays blocked across restarts until the user acts.
                            settings.TrustedProviders[asmName] = false;
                            this._trustRecordsDirty = true;

                            issues.Add(new ProviderIntegrityIssue
                            {
                                FileName = Path.GetFileName(assemblyPath),
                                Severity = ProviderIntegrityIssueSeverity.Error,
                                Status = "Previously trusted — file no longer matches the published hash — disabled and blocked",
                                Detail =
                                    "The file may have been tampered with or corrupted.\n\n" +
                                    $"Expected: {hashResult?.PublicHash}\n" +
                                    $"Actual: {localHash}\n\n" +
                                    "Re-download the provider from official SmartHopper sources, then re-enable it in SmartHopper → Settings → Providers.",
                            });

                            RhinoApp.WriteLine($"[SmartHopper] SECURITY: Provider '{Path.GetFileName(assemblyPath)}' failed hash verification after being trusted — disabled and blocked.");
                            Logger.For<ProviderManager>().Warning($"[ProviderManager] Trusted provider '{asmName}' failed hash verification — disabled and blocked.");
                            return ProviderLoadOutcome.Skipped;

                        case StoredTrustDecision.NeedsReconsent:
                            return ProviderLoadOutcome.NeedsReconsent;

                        case StoredTrustDecision.BlockUnverifiedChanged:
                            issues.Add(new ProviderIntegrityIssue
                            {
                                FileName = Path.GetFileName(assemblyPath),
                                Severity = ProviderIntegrityIssueSeverity.Error,
                                Status = $"Cannot be verified against the consent on record — blocked ({effectiveMode} mode)",
                                Detail =
                                    $"Either the file changed since you enabled it, or no consent record exists — and it is not in the official hash manifest. " +
                                    $"{effectiveMode} integrity check mode only allows re-trusting unverified providers in 'Soft' mode.\n\n" +
                                    "The provider has been blocked. Switch to 'Soft' mode to review and confirm it.",
                            });

                            RhinoApp.WriteLine($"[SmartHopper] Provider Integrity Check Failed: '{Path.GetFileName(assemblyPath)}' — unverified consent mismatch, blocked in {effectiveMode} mode.");
                            Logger.For<ProviderManager>().Warning($"[ProviderManager] Provider '{asmName}' — unverified consent mismatch, blocked ({effectiveMode} mode).");
                            return ProviderLoadOutcome.Skipped;

                        case StoredTrustDecision.LoadVerified:
                            this.MintTrustRecord(settings, asmName, localHash, verified: true);
                            break;

                        default: // LoadUnverified — ensure a consent baseline exists
                            this.MintTrustRecord(settings, asmName, localHash, verified: false);
                            break;
                    }
                }

                if (hashResult != null)
                {
                    switch (hashResult.Status)
                    {
                        case ProviderVerificationStatus.Match:
                            Logger.For<ProviderManager>().Trace($"[ProviderManager] SHA-256 verification passed for {Path.GetFileName(assemblyPath)} ({platform})");
                            break;

                        case ProviderVerificationStatus.Mismatch:
                            // Hash mismatch indicates potential tampering
                            var mmAsmName = Path.GetFileNameWithoutExtension(assemblyPath);

                            if (effectiveMode == ProviderIntegrityCheckMode.Strict ||
                                effectiveMode == ProviderIntegrityCheckMode.Hard)
                            {
                                // Strict/Hard mode: report the failure and prevent loading
                                issues.Add(new ProviderIntegrityIssue
                                {
                                    FileName = Path.GetFileName(assemblyPath),
                                    Severity = ProviderIntegrityIssueSeverity.Error,
                                    Status = "SHA-256 hash does not match the published hash — blocked",
                                    Detail =
                                        "This could indicate file corruption or tampering.\n\n" +
                                        $"Platform: {platform}\n" +
                                        $"Expected: {hashResult.PublicHash}\n" +
                                        $"Actual: {hashResult.LocalHash}\n\n" +
                                        "Please re-download the provider from official SmartHopper sources.",
                                });

                                RhinoApp.WriteLine($"[SmartHopper] Provider Integrity Check Failed: '{Path.GetFileName(assemblyPath)}' failed integrity verification and will not be loaded");
                                Logger.For<ProviderManager>().Warning($"[ProviderManager] Provider '{Path.GetFileName(assemblyPath)}' failed integrity verification and will not be loaded");

                                return ProviderLoadOutcome.Skipped;
                            }
                            else
                            {
                                // Soft mode: report a warning and continue loading
                                this._mismatchedProviders[mmAsmName] = true;

                                issues.Add(new ProviderIntegrityIssue
                                {
                                    FileName = Path.GetFileName(assemblyPath),
                                    Severity = ProviderIntegrityIssueSeverity.Warning,
                                    Status = "SHA-256 hash does not match the published hash — loaded with warning",
                                    Detail =
                                        "This could indicate file corruption or tampering.\n\n" +
                                        $"Platform: {platform}\n" +
                                        $"Expected: {hashResult.PublicHash}\n" +
                                        $"Actual: {hashResult.LocalHash}\n\n" +
                                        "The provider has been loaded but will show a warning when used. " +
                                        "Change 'Integrity Check Mode' to 'Hard' or 'Strict' in settings to block unverified providers.",
                                });

                                RhinoApp.WriteLine($"[SmartHopper] Provider Integrity Check Failed: '{Path.GetFileName(assemblyPath)}' failed integrity verification");
                                Logger.For<ProviderManager>().Warning($"[ProviderManager] Provider '{Path.GetFileName(assemblyPath)}' failed integrity verification");
                            }

                            break;

                        case ProviderVerificationStatus.Unavailable:
                            // Hash repository unavailable - network or source issue
                            if (effectiveMode == ProviderIntegrityCheckMode.Strict)
                            {
                                // Strict mode: Block when hashes are unavailable
                                issues.Add(new ProviderIntegrityIssue
                                {
                                    FileName = Path.GetFileName(assemblyPath),
                                    Severity = ProviderIntegrityIssueSeverity.Error,
                                    Status = "Hash verification data unavailable — blocked (Strict mode)",
                                    Detail =
                                        "Unable to retrieve hash verification data from the official repository. " +
                                        "This may be due to network connectivity issues.\n\n" +
                                        "Strict integrity check mode requires hash verification for all providers. " +
                                        "Please check your internet connection and try again, or switch to 'Hard' or 'Soft' mode in settings.",
                                });

                                RhinoApp.WriteLine($"[SmartHopper] Provider Integrity Check Failed: Provider '{Path.GetFileName(assemblyPath)}' blocked - hash repository unavailable in Strict mode");
                                Logger.For<ProviderManager>().Warning($"[ProviderManager] Provider '{Path.GetFileName(assemblyPath)}' blocked - hash unavailable (Strict mode)");
                                return ProviderLoadOutcome.Skipped;
                            }
                            else
                            {
                                // Hard/Soft mode: Log warning but allow
                                var uaAsmName = Path.GetFileNameWithoutExtension(assemblyPath);
                                this._unavailableProviders[uaAsmName] = true;
                                RhinoApp.WriteLine($"[SmartHopper] Provider Integrity Check Failed: Could not verify provider '{Path.GetFileName(assemblyPath)}' - hash check skipped. Enable only if you trust this source.");
                                Logger.For<ProviderManager>().Warning($"[ProviderManager] Hash unavailable for {Path.GetFileName(assemblyPath)}, skipping verification");
                            }

                            break;

                        case ProviderVerificationStatus.NotFound:
                            // Hash not found - custom or third-party provider
                            var nfAsmName = Path.GetFileNameWithoutExtension(assemblyPath);

                            if (effectiveMode == ProviderIntegrityCheckMode.Strict ||
                                effectiveMode == ProviderIntegrityCheckMode.Hard)
                            {
                                // Strict/Hard mode: Block unknown providers
                                issues.Add(new ProviderIntegrityIssue
                                {
                                    FileName = Path.GetFileName(assemblyPath),
                                    Severity = ProviderIntegrityIssueSeverity.Error,
                                    Status = $"Not in the official manifest — blocked ({effectiveMode} mode)",
                                    Detail =
                                        $"SHA-256 hash not found in official repository (platform: {platform}). " +
                                        "This provider may be a custom/third-party provider or from a different SmartHopper version.\n\n" +
                                        $"{effectiveMode} integrity check mode only allows verified providers. " +
                                        "Switch to 'Soft' mode in settings to allow third-party providers.",
                                });

                                RhinoApp.WriteLine($"[SmartHopper] Provider Integrity Check Failed: '{Path.GetFileName(assemblyPath)}' blocked - hash not found in {effectiveMode} mode");
                                Logger.For<ProviderManager>().Warning($"[ProviderManager] Provider '{Path.GetFileName(assemblyPath)}' blocked - hash not found ({effectiveMode} mode)");
                                return ProviderLoadOutcome.Skipped;
                            }
                            else
                            {
                                // Soft mode: Log warning but allow
                                this._unknownProviders[nfAsmName] = true;
                                RhinoApp.WriteLine($"[SmartHopper] Provider Integrity Check Failed: '{Path.GetFileName(assemblyPath)}' is not known - enable only if you trust this source.");
                                Logger.For<ProviderManager>().Warning($"[ProviderManager] Hash not found for {Path.GetFileName(assemblyPath)}, allowing in Soft mode");
                            }

                            break;
                    }
                }

                // Load the assembly
                var assembly = Assembly.LoadFrom(assemblyPath);
                var loadedAsmName = assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(assemblyPath);

                // Trust is decided up-front by the unified new-providers dialog in
                // DiscoverProvidersAsync — every path reaching this point has an entry.
                Logger.For<ProviderManager>().Trace($"[ProviderManager] Trust for '{asmName}': {(trusted ? "trusted" : "no entry")}");

                // Track official/unsigned state for trust reporting
                var providerToken = assembly.GetName().GetPublicKeyToken();
                var hostToken = typeof(ProviderManager).Assembly.GetName().GetPublicKeyToken();
                bool tokenMatchesHost = providerToken is { Length: > 0 } && hostToken is { Length: > 0 } && providerToken.SequenceEqual(hostToken);
                bool isOfficial = (hashResult?.Status == ProviderVerificationStatus.Match) || tokenMatchesHost;
                bool isUnsigned = providerToken is null || providerToken.Length == 0;

                if (isOfficial)
                {
                    this._officialProviders[loadedAsmName] = true;
                }

                if (isUnsigned)
                {
                    this._unsignedProviders[loadedAsmName] = true;
                }

                // Find all types that implement IAIProviderFactory
                var factoryTypes = assembly.GetTypes()
                    .Where(t => typeof(IAIProviderFactory).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();

                foreach (var factoryType in factoryTypes)
                {
                    try
                    {
                        // Create an instance of the factory
                        var factory = (IAIProviderFactory)Activator.CreateInstance(factoryType);

                        // Create provider and settings instances
                        var provider = factory.CreateProvider();
                        var providerSettings = factory.CreateProviderSettings();

                        // Register the provider
                        this.RegisterProvider(provider, providerSettings, assembly);

                        Logger.For<ProviderManager>().Trace($"Successfully registered provider: {provider.Name} from {assembly.GetName().Name}");
                    }
                    catch (Exception ex)
                    {
                        Logger.For<ProviderManager>().Exception(ex, $"Error creating provider from factory {factoryType.FullName}: {ex.Message}");
                    }
                }

                return ProviderLoadOutcome.Loaded;
            }
            catch (Exception ex)
            {
                Logger.For<ProviderManager>().Exception(ex, $"Error loading provider assembly {assemblyPath}: {ex.Message}");
                return ProviderLoadOutcome.Skipped;
            }
        }

        /// <summary>Computes the SHA-256 of a provider file, returning <see langword="null"/> on failure.</summary>
        private static string? TryComputeLocalHash(string assemblyPath)
        {
            try
            {
                return ProviderHashVerifier.CalculateFileHash(assemblyPath);
            }
            catch (Exception ex)
            {
                Logger.For<ProviderManager>().Trace($"[ProviderManager] Could not hash {assemblyPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Writes the consent snapshot for a provider (the bytes consented to and whether
        /// consent followed a catalogue match). Idempotent — no write when the stored
        /// record already matches, so official updates re-mint silently without churn.
        /// </summary>
        /// <returns><see langword="true"/> when the stored record changed.</returns>
        private bool MintTrustRecord(SmartHopperSettings settings, string asmName, string? localHash, bool verified)
        {
            if (string.IsNullOrEmpty(localHash))
            {
                return false;
            }

            if (settings.ProviderTrustRecords.TryGetValue(asmName, out var existing)
                && string.Equals(existing.ConsentedHash, localHash, StringComparison.OrdinalIgnoreCase)
                && existing.VerifiedTrust == verified)
            {
                return false;
            }

            settings.ProviderTrustRecords[asmName] = new ProviderTrustRecord
            {
                ConsentedHash = localHash,
                VerifiedTrust = verified,
            };
            this._trustRecordsDirty = true;
            return true;
        }

        /// <summary>
        /// Builds the metadata rows for the unified new-providers dialog: hash
        /// verification and bounded embedded manifest data. No provider code is
        /// executed before the user grants trust; missing metadata uses the filename.
        /// </summary>
        /// <param name="assemblyPaths">Provider DLL paths lacking a trust entry.</param>
        /// <param name="verify">Optional integrity verifier for isolated, network-free contract tests.</param>
        internal static async Task<List<NewProviderCandidate>> BuildNewProviderCandidatesAsync(
            List<string> assemblyPaths,
            Func<string, Task<ProviderVerificationResult>>? verify = null)
        {
            var candidates = new List<NewProviderCandidate>();
            foreach (var assemblyPath in assemblyPaths)
            {
                var asmName = Path.GetFileNameWithoutExtension(assemblyPath);
                var candidate = new NewProviderCandidate
                {
                    AssemblyName = asmName,
                    AssemblyPath = assemblyPath,
                    DisplayName = asmName,
                    LastUpdate = File.GetLastWriteTime(assemblyPath),
                };

                try
                {
                    candidate.Verification = await (verify != null
                        ? verify(assemblyPath)
                        : ProviderHashVerifier.VerifyProviderAsync(assemblyPath, VersionHelper.GetDisplayVersion(), VersionHelper.GetPlatform()))
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.For<ProviderManager>().Trace($"[ProviderManager] Hash check failed for candidate {asmName}: {ex.Message}");
                }

                var manifest = ProviderManifest.Read(assemblyPath);
                if (manifest != null)
                {
                    candidate.DisplayName = manifest.DisplayName;
                    candidate.ModelCount = manifest.ModelCount;
                }

                candidates.Add(candidate);
            }

            return candidates;
        }

        /// <summary>Shows the unified providers dialog on the UI thread and returns the enable decisions.</summary>
        /// <param name="candidates">Provider candidates to review.</param>
        /// <param name="reconsent"><see langword="true"/> when the prompt re-confirms previously trusted providers whose files changed.</param>
        private static Task<Dictionary<string, bool>> PromptNewProvidersAsync(List<NewProviderCandidate> candidates, bool reconsent = false)
        {
            var tcs = new TaskCompletionSource<Dictionary<string, bool>>();
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    tcs.SetResult(ProviderTrustDialog.Decide(candidates, reconsent));
                }
                catch (Exception ex)
                {
                    Logger.For<ProviderManager>().Exception(ex, "[ProviderManager] New-providers dialog error");
                    tcs.SetResult(candidates.ToDictionary(c => c.AssemblyName, _ => false));
                }
            }));
            return tcs.Task;
        }

        /// <summary>
        /// Shows every integrity finding collected during provider loading in a single
        /// <see cref="ProviderTrustDialog"/> report. Dialog failures are logged and never
        /// block provider discovery.
        /// </summary>
        private static Task ReportIntegrityIssuesAsync(IReadOnlyList<ProviderIntegrityIssue> issues)
        {
            var tcs = new TaskCompletionSource<bool>();
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    ProviderTrustDialog.ReportIssues(issues);
                }
                catch (Exception ex)
                {
                    Logger.For<ProviderManager>().Exception(ex, "[ProviderManager] Integrity-report dialog error");
                }
                finally
                {
                    tcs.TrySetResult(true);
                }
            }));
            return tcs.Task;
        }

        /// <summary>
        /// Registers a provider with the manager.
        /// </summary>
        /// <param name="provider">The provider to register.</param>
        /// <param name="settings">The provider settings.</param>
        /// <param name="assembly">The assembly containing the provider.</param>
        private void RegisterProvider(IAIProvider provider, IAIProviderSettings settings, Assembly assembly)
        {
            if (provider == null || string.IsNullOrEmpty(provider.Name))
                return;

            // Add the provider and its settings to our dictionaries
            this._providers[provider.Name] = provider;
            this._providerSettings[provider.Name] = settings;

            // Store the assembly for future reference
            if (assembly != null && !this._providerAssemblies.ContainsKey(provider.Name))
            {
                this._providerAssemblies[provider.Name] = assembly;
            }

            // Initialize provider asynchronously without blocking
            _ = Task.Run(async () =>
            {
                try
                {
                    await provider.InitializeProviderAsync().ConfigureAwait(false);
                    Logger.For<ProviderManager>().Trace($"[ProviderManager] Successfully initialized provider: {provider.Name}");
                }
                catch (Exception ex)
                {
                    Logger.For<ProviderManager>().Exception(ex, $"[ProviderManager] Error initializing provider {provider.Name}: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Gets all providers, optionally including untrusted ones.
        /// </summary>
        /// <param name="includeUntrusted">If true, includes untrusted providers. If false (default), only returns trusted providers.</param>
        /// <returns>A collection of providers based on trust filter.</returns>
        public IEnumerable<IAIProvider> GetProviders(bool includeUntrusted = false)
        {
            if (includeUntrusted)
            {
                // Return all providers regardless of trust status
                return this._providers.Values;
            }

            // Return only trusted providers (existing behavior)
            var settings = SmartHopperSettings.Instance;
            return this._providers.Values.Where(provider =>
            {
                // Get assembly name for trust checking
                if (this._providerAssemblies.TryGetValue(provider.Name, out var assembly))
                {
                    var asmName = assembly.GetName().Name;

                    // If provider is explicitly untrusted, exclude it
                    if (settings.TrustedProviders.TryGetValue(asmName, out var isAllowed) && !isAllowed)
                    {
                        return false;
                    }
                }

                return true;
            });
        }

        /// <summary>
        /// Gets the count of registered providers.
        /// </summary>
        /// <returns>The number of providers currently registered.</returns>
        public int GetProviderCount()
        {
            return this._providers.Count;
        }

        /// <summary>
        /// Gets whether the provider infrastructure has completed initialization.
        /// This flag is set to true after RefreshProvidersAsync completes, regardless of provider count.
        /// </summary>
        public bool IsInfrastructureReady => this._refreshCompleted;

        /// <summary>
        /// Gets the provider integrity findings collected during the last discovery
        /// pass, or <see langword="null"/> when the last scan was clean. Lets
        /// non-modal surfaces (settings pages, provider-resolution failures)
        /// explain blocked providers without re-running verification.
        /// </summary>
        internal IReadOnlyList<ProviderIntegrityIssue>? LastIntegrityFindings => this._lastIntegrityFindings;

        /// <summary>
        /// Gets a provider by name.
        /// </summary>
        /// <param name="providerName">Name of the provider to get.</param>
        /// <returns>The provider, or null if not found.</returns>
        public IAIProvider GetProvider(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                return null;
            }

            // Handle "Default" provider name
            if (providerName == "Default")
            {
                // Avoid calling SmartHopperSettings.Instance to prevent circular dependency
                // Instead use a static field or direct lookup from the dictionary
                string defaultProviderName = null;
                try
                {
                    // Try to get default provider name without causing circular dependency
                    defaultProviderName = SmartHopperSettings.Instance?.DefaultAIProvider;
                }
                catch (Exception ex)
                {
                    Logger.For<ProviderManager>().Exception(ex, $"Error getting default provider: {ex.Message}");
                }

                if (string.IsNullOrEmpty(defaultProviderName))
                {
                    // No default set, return first available provider
                    return this._providers.Values.FirstOrDefault();
                }

                providerName = defaultProviderName;
            }

            // Try to get the provider
            if (this._providers.TryGetValue(providerName, out var provider))
            {
                // Verify the provider is still trusted before returning it
                if (this._providerAssemblies.TryGetValue(provider.Name, out var assembly))
                {
                    var asmName = assembly.GetName().Name;
                    var settings = SmartHopperSettings.Instance;

                    // If provider is explicitly untrusted, don't return it
                    if (settings.TrustedProviders.TryGetValue(asmName, out var isAllowed) && !isAllowed)
                    {
                        Logger.For<ProviderManager>().Warning($"Provider '{provider.Name}' is no longer trusted, returning null.");
                        return null;
                    }
                }

                // NOTE: Don't refresh settings here to avoid circular dependencies
                // The provider's settings will be refreshed when needed by specific operations
                return provider;
            }

            return null;
        }

        /// <summary>
        /// Gets the settings for a provider.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>The provider settings, or null if not found.</returns>
        public IAIProviderSettings GetProviderSettings(string providerName)
        {
            return this._providerSettings.TryGetValue(providerName, out var settings) ? settings : null;
        }

        /// <summary>
        /// Gets the extra parameter descriptors for a provider.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>An enumerable of <see cref="AIExtraDescriptor"/> instances, or empty if provider not found.</returns>
        public IEnumerable<AIExtraDescriptor> GetExtraDescriptors(string providerName)
        {
            var provider = this.GetProvider(providerName);
            return provider?.GetExtraDescriptors() ?? Enumerable.Empty<AIExtraDescriptor>();
        }

        /// <summary>
        /// Gets the assembly containing a provider.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>The assembly, or null if not found.</returns>
        public Assembly GetProviderAssembly(string providerName)
        {
            return this._providerAssemblies.TryGetValue(providerName, out var assembly) ? assembly : null;
        }

        /// <summary>
        /// Gets the icon for the specified AI provider
        /// </summary>
        /// <param name="providerName">Name of the provider</param>
        /// <returns>The provider's icon or null if not found</returns>
        public Image GetProviderIcon(string providerName)
        {
            var provider = this.GetProvider(providerName);
            return provider?.Icon;
        }

        /// <summary>
        /// Checks if a provider has failed integrity verification (hash mismatch).
        /// </summary>
        /// <param name="providerName">The name of the provider to check.</param>
        /// <returns>True if the provider has a hash mismatch and was loaded with soft verification; otherwise, false.</returns>
        public bool IsProviderMismatched(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                return false;
            }

            // Get the assembly name for this provider
            if (this._providerAssemblies.TryGetValue(providerName, out var assembly))
            {
                var asmName = assembly.GetName().Name;
                return this._mismatchedProviders.ContainsKey(asmName);
            }

            return false;
        }

        /// <summary>
        /// Checks if a provider had unavailable hash verification (network/repository issues).
        /// </summary>
        /// <param name="providerName">The name of the provider to check.</param>
        /// <returns>True if the provider's hash could not be verified due to network/repository unavailability; otherwise, false.</returns>
        public bool IsProviderUnavailable(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                return false;
            }

            // Get the assembly name for this provider
            if (this._providerAssemblies.TryGetValue(providerName, out var assembly))
            {
                var asmName = assembly.GetName().Name;
                return this._unavailableProviders.ContainsKey(asmName);
            }

            return false;
        }

        /// <summary>
        /// Checks if a provider is unknown (not found in hash manifest - custom/third-party).
        /// </summary>
        /// <param name="providerName">The name of the provider to check.</param>
        /// <returns>True if the provider is not in the official hash manifest; otherwise, false.</returns>
        public bool IsProviderUnknown(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                return false;
            }

            // Get the assembly name for this provider
            if (this._providerAssemblies.TryGetValue(providerName, out var assembly))
            {
                var asmName = assembly.GetName().Name;
                return this._unknownProviders.ContainsKey(asmName);
            }

            return false;
        }

        /// <summary>
        /// Checks if a provider is classified as community / non-official.
        /// </summary>
        /// <param name="providerName">The name of the provider to check.</param>
        /// <returns>True if the provider is valid but not cryptographically attributable to SmartHopper.</returns>
        public bool IsProviderCommunity(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                return false;
            }

            if (this._providerAssemblies.TryGetValue(providerName, out var assembly))
            {
                var asmName = assembly.GetName().Name;
                return !this._officialProviders.ContainsKey(asmName)
                    && !this._mismatchedProviders.ContainsKey(asmName)
                    && !this.IsProviderUnknown(providerName);
            }

            return false;
        }

        /// <summary>
        /// Checks if a provider assembly was loaded without a strong-name signature.
        /// </summary>
        /// <param name="providerName">The name of the provider to check.</param>
        /// <returns>True if the provider assembly has no public key token.</returns>
        public bool IsProviderUnsigned(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
            {
                return false;
            }

            if (this._providerAssemblies.TryGetValue(providerName, out var assembly))
            {
                var asmName = assembly.GetName().Name;
                return this._unsignedProviders.ContainsKey(asmName);
            }

            return false;
        }

        /// <summary>
        /// Gets the default AI provider from settings, or the first available provider if not set.
        /// </summary>
        /// <returns>The default AI provider name</returns>
        public string GetDefaultAIProvider()
        {
            var settings = SmartHopperSettings.Instance;
            if (!string.IsNullOrWhiteSpace(settings.DefaultAIProvider) && this._providers.ContainsKey(settings.DefaultAIProvider))
            {
                return settings.DefaultAIProvider;
            }

            // Fallback to first provider if default not set or invalid
            return this._providers.Keys.FirstOrDefault() ?? string.Empty;
        }

        /// <summary>
        /// Updates settings for a specific provider.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <param name="settings">The settings to update.</param>
        public void UpdateProviderSettings(string providerName, Dictionary<string, object> settings)
        {
            Logger.For<ProviderManager>().Trace($"[ProviderManager] Updating settings for {providerName} with {settings?.Count ?? 0} values");

            var provider = this.GetProvider(providerName);
            if (provider == null)
            {
                Logger.For<ProviderManager>().Trace($"[ProviderManager] Provider {providerName} not found.");
                return;
            }

            var ui = ProviderManager.Instance.GetProviderSettings(providerName);
            // Validate settings
            if (!ui.ValidateSettings(settings))
            {
                Logger.For<ProviderManager>().Warning($"[ProviderManager] Settings validation failed for provider {providerName}. Not updating any settings for this provider.");
                return;
            }

            SmartHopperSettings.Instance.ApplyProviderSettingUpdates(providerName, settings);

            // Save settings to disk
            Logger.For<ProviderManager>().Trace($"[ProviderManager] Saving settings to disk");
            SmartHopperSettings.Instance.Save();

            // Refresh the provider's cached settings (merge with existing to preserve provider-set values)
            var updatedSettings = SmartHopperSettings.Instance.GetProviderSettings(providerName);
            provider.RefreshCachedSettings(updatedSettings);
            Logger.For<ProviderManager>().Trace($"[ProviderManager] Provider {providerName} settings refreshed with updated values");
        }

        // Implement Authenticode verification: real WinVerifyTrust signature validation
        // (the signature must cover the file bytes) plus publisher pinning against the
        // host assembly's signer certificate. Windows-only; other platforms rely on the
        // strong-name token and the SHA-256 hash manifest.
        private void VerifySignature(string filePath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var signatureStatus = AuthenticodeSignatureVerifier.Verify(filePath, out var rawStatus);
                    if (signatureStatus is AuthenticodeSignatureStatus.Unsigned or AuthenticodeSignatureStatus.Invalid)
                        throw new CryptographicException($"Authenticode signature verification failed for {Path.GetFileName(filePath)} (status {signatureStatus}, 0x{rawStatus:X8}).");

                    var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
                    var baseCert = new X509Certificate2(X509Certificate.CreateFromSignedFile(
                        Assembly.GetExecutingAssembly().Location));
                    if (!string.Equals(cert.Thumbprint, baseCert.Thumbprint, StringComparison.OrdinalIgnoreCase))
                        throw new CryptographicException($"Authenticode certificate mismatch for {Path.GetFileName(filePath)}.");
                }
                catch (CryptographicException)
                {
                    throw;
                }
                catch (FileNotFoundException ex)
                {
                    throw new CryptographicException($"File is not a valid signed assembly: {Path.GetFileName(filePath)}", ex);
                }
                catch (BadImageFormatException ex)
                {
                    throw new CryptographicException($"File is not a valid assembly format: {Path.GetFileName(filePath)}", ex);
                }
                catch (Exception ex)
                {
                    throw new CryptographicException($"Authenticode signature verification failed for {Path.GetFileName(filePath)}: {ex.Message}", ex);
                }
            }

            // Strong-name: ensure public key token matches the host assembly's token
            try
            {
                var asmName = AssemblyName.GetAssemblyName(filePath);
                var token = asmName.GetPublicKeyToken();
                var baseToken = Assembly.GetExecutingAssembly().GetName().GetPublicKeyToken();
                if (!Enumerable.SequenceEqual(token, baseToken))
                    throw new SecurityException($"Strong-name public key token mismatch for {Path.GetFileName(filePath)}.");
            }
            catch (SecurityException)
            {
                throw;
            }
            catch (FileNotFoundException ex)
            {
                throw new CryptographicException($"File is not a valid assembly: {Path.GetFileName(filePath)}", ex);
            }
            catch (BadImageFormatException ex)
            {
                throw new CryptographicException($"File is not a valid assembly format: {Path.GetFileName(filePath)}", ex);
            }
            catch (Exception ex)
            {
                throw new SecurityException($"Strong-name signature verification failed for {Path.GetFileName(filePath)}: {ex.Message}", ex);
            }
        }
    }
}
