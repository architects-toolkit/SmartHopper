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
using SmartHopper.Infrastructure.Settings;
using SmartHopper.Infrastructure.Utils;

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
                foreach (string providerFile in providerFiles)
                {
                    try
                    {
                        await this.LoadProviderAssemblyAsync(providerFile).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading provider assembly {providerFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error discovering providers: {ex.Message}");
            }
        }

        /// <summary>
        /// Manually triggers discovery of external AI providers.
        /// </summary>
        public async Task RefreshProvidersAsync()
        {
            Debug.WriteLine("[ProviderManager] Starting provider discovery and registration");

            try
            {
                // Discover new providers
                await this.DiscoverProvidersAsync().ConfigureAwait(false);

                // After discovery, refresh settings for all providers
                Debug.WriteLine("[ProviderManager] Provider discovery complete, refreshing settings");
                SmartHopperSettings.Instance.RefreshProvidersLocalStorage();
            }
            finally
            {
                // Mark refresh as completed regardless of provider count or errors
                // This signals that infrastructure initialization is done
                this._refreshCompleted = true;
                Debug.WriteLine("[ProviderManager] Provider refresh completed (infrastructure ready)");
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
                Debug.WriteLine($"[ProviderManager] Error verifying providers: {ex.Message}");
                return new Dictionary<string, ProviderVerificationResult>();
            }
        }

        /// <summary>
        /// Loads a provider assembly and registers any providers it contains.
        /// </summary>
        /// <param name="assemblyPath">The path to the provider assembly.</param>
        private async Task LoadProviderAssemblyAsync(string assemblyPath)
        {
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
                        Debug.WriteLine($"Authenticode signature verification failed for {assemblyPath}: {ex.Message}");
                        await Task.Run(() => RhinoApp.InvokeOnUiThread(() =>
                        {
                            StyledMessageDialog.ShowError($"Authenticode signature verification failed for provider '{Path.GetFileName(assemblyPath)}'. Please replace it with a file downloaded from official SmartHopper sources.", "SmartHopper");
                        })).ConfigureAwait(false);
                        return;
                    }
                }
                else
                {
                    Debug.WriteLine($"[ProviderManager] Skipping Authenticode verification on non-Windows platform for {Path.GetFileName(assemblyPath)}");
                }

                // SHA-256 hash verification (cross-platform)
                // In DEBUG builds, force soft check mode to allow local development
                // In RELEASE builds, use the configured integrity check mode
                try
                {
                    string platform = VersionHelper.GetPlatform();
                    string version = VersionHelper.GetDisplayVersion();

                    var hashResult = await ProviderHashVerifier.VerifyProviderAsync(assemblyPath, version, platform)
                        .ConfigureAwait(false);

                    var effectiveMode = SmartHopperSettings.Instance.EffectiveProviderIntegrityCheckMode;

                    switch (hashResult.Status)
                    {
                        case ProviderVerificationStatus.Match:
                            Debug.WriteLine($"[ProviderManager] SHA-256 verification passed for {Path.GetFileName(assemblyPath)} ({platform})");
                            break;

                        case ProviderVerificationStatus.Mismatch:
                            // Hash mismatch indicates potential tampering
                            var mmAsmName = Path.GetFileNameWithoutExtension(assemblyPath);

                            if (effectiveMode == ProviderIntegrityCheckMode.Strict ||
                                effectiveMode == ProviderIntegrityCheckMode.Hard)
                            {
                                // Strict/Hard mode: Show error and prevent loading
                                await Task.Run(() =>
                                {
                                    RhinoApp.InvokeOnUiThread(() =>
                                    {
                                        StyledMessageDialog.ShowError(
                                            $"Provider '{Path.GetFileName(assemblyPath)}' failed integrity verification.\n\n" +
                                            $"The file's SHA-256 hash does not match the published hash from official sources. " +
                                            $"This could indicate file corruption or tampering.\n\n" +
                                            $"Platform: {platform}\n" +
                                            $"Expected: {hashResult.PublicHash}\n" +
                                            $"Actual: {hashResult.LocalHash}\n\n" +
                                            "Please re-download the provider from official SmartHopper sources.",
                                            "Provider Integrity Check Failed - SmartHopper");
                                    });
                                }).ConfigureAwait(false);

                                RhinoApp.WriteLine($"[SmartHopper] Provider Integrity Check Failed: '{Path.GetFileName(assemblyPath)}' failed integrity verification and will not be loaded");
                                Debug.WriteLine($"[ProviderManager] Provider '{Path.GetFileName(assemblyPath)}' failed integrity verification and will not be loaded");

                                return;
                            }
                            else
                            {
                                // Soft mode: Show warning and continue loading
                                this._mismatchedProviders[mmAsmName] = true;

                                await Task.Run(() =>
                                {
                                    RhinoApp.InvokeOnUiThread(() =>
                                    {
                                        StyledMessageDialog.ShowWarning(
                                            $"WARNING: Provider '{Path.GetFileName(assemblyPath)}' failed integrity verification.\n\n" +
                                            $"The file's SHA-256 hash does not match the published hash from official sources. " +
                                            $"This could indicate file corruption or tampering.\n\n" +
                                            $"Platform: {platform}\n" +
                                            $"Expected: {hashResult.PublicHash}\n" +
                                            $"Actual: {hashResult.LocalHash}\n\n" +
                                            "The provider has been loaded but will show a warning when used. " +
                                            "Change 'Integrity Check Mode' to 'Hard' or 'Strict' in settings to block unverified providers.",
                                            "Provider Integrity Check Warning - SmartHopper");
                                    });
                                }).ConfigureAwait(false);

                                RhinoApp.WriteLine($"[SmartHopper] Provider Integrity Check Failed: '{Path.GetFileName(assemblyPath)}' failed integrity verification");
                                Debug.WriteLine($"[ProviderManager] Provider '{Path.GetFileName(assemblyPath)}' failed integrity verification");
                            }

                            break;

                        case ProviderVerificationStatus.Unavailable:
                            // Hash repository unavailable - network or source issue
                            if (effectiveMode == ProviderIntegrityCheckMode.Strict)
                            {
                                // Strict mode: Block when hashes are unavailable
                                await Task.Run(() => RhinoApp.InvokeOnUiThread(() =>
                                {
                                    StyledMessageDialog.ShowError(
                                        $"Provider '{Path.GetFileName(assemblyPath)}' cannot be loaded.\n\n" +
                                        $"Unable to retrieve hash verification data from the official repository. " +
                                        $"This may be due to network connectivity issues.\n\n" +
                                        $"Strict integrity check mode requires hash verification for all providers. " +
                                        "Please check your internet connection and try again, or switch to 'Hard' or 'Soft' mode in settings.",
                                        "Provider Integrity Check Failed - SmartHopper");
                                })).ConfigureAwait(false);

                                RhinoApp.WriteLine($"[SmartHopper] Provider Integrity Check Failed: Provider '{Path.GetFileName(assemblyPath)}' blocked - hash repository unavailable in Strict mode");
                                Debug.WriteLine($"[ProviderManager] Provider '{Path.GetFileName(assemblyPath)}' blocked - hash unavailable (Strict mode)");
                                return;
                            }
                            else
                            {
                                // Hard/Soft mode: Log warning but allow
                                var uaAsmName = Path.GetFileNameWithoutExtension(assemblyPath);
                                this._unavailableProviders[uaAsmName] = true;
                                RhinoApp.WriteLine($"[SmartHopper] Provider Integrity Check Failed: Could not verify provider '{Path.GetFileName(assemblyPath)}' - hash check skipped. Enable only if you trust this source.");
                                Debug.WriteLine($"[ProviderManager] Hash unavailable for {Path.GetFileName(assemblyPath)}, skipping verification");
                            }

                            break;

                        case ProviderVerificationStatus.NotFound:
                            // Hash not found - custom or third-party provider
                            var nfAsmName = Path.GetFileNameWithoutExtension(assemblyPath);

                            if (effectiveMode == ProviderIntegrityCheckMode.Strict ||
                                effectiveMode == ProviderIntegrityCheckMode.Hard)
                            {
                                // Strict/Hard mode: Block unknown providers
                                await Task.Run(() => RhinoApp.InvokeOnUiThread(() =>
                                {
                                    StyledMessageDialog.ShowError(
                                        $"Provider '{Path.GetFileName(assemblyPath)}' is not recognized.\n\n" +
                                        $"SHA-256 hash not found in official repository (platform: {platform}). " +
                                        $"This provider may be a custom/third-party provider or from a different SmartHopper version.\n\n" +
                                        $"{effectiveMode} integrity check mode only allows verified providers. " +
                                        $"Switch to 'Soft' mode in settings to allow third-party providers.",
                                        "Provider Integrity Check Failed - SmartHopper");
                                })).ConfigureAwait(false);

                                RhinoApp.WriteLine($"[SmartHopper] Provider Integrity Check Failed: '{Path.GetFileName(assemblyPath)}' blocked - hash not found in {effectiveMode} mode");
                                Debug.WriteLine($"[ProviderManager] Provider '{Path.GetFileName(assemblyPath)}' blocked - hash not found ({effectiveMode} mode)");
                                return;
                            }
                            else
                            {
                                // Soft mode: Log warning but allow
                                this._unknownProviders[nfAsmName] = true;
                                RhinoApp.WriteLine($"[SmartHopper] Provider Integrity Check Failed: '{Path.GetFileName(assemblyPath)}' is not known - enable only if you trust this source.");
                                Debug.WriteLine($"[ProviderManager] Hash not found for {Path.GetFileName(assemblyPath)}, allowing in Soft mode");
                            }

                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ProviderManager] SHA-256 verification error for {assemblyPath}: {ex.Message}");

                    // Continue loading - don't block on verification errors
                }

                var settings = SmartHopperSettings.Instance;
                var asmName = Path.GetFileNameWithoutExtension(assemblyPath);

                // Prompt user for providers with no trust entry
                Debug.WriteLine($"[ProviderManager] Checking trust for provider: {asmName}");
                Debug.WriteLine($"[ProviderManager] TrustedProviders contains '{asmName}': {settings.TrustedProviders.ContainsKey(asmName)}");

                if (!settings.TrustedProviders.ContainsKey(asmName))
                {
                    Debug.WriteLine($"[ProviderManager] Showing trust prompt for: {asmName}");

                    // Use TaskCompletionSource to properly wait for the dialog result
                    var tcs = new TaskCompletionSource<bool>();
                    RhinoApp.InvokeOnUiThread(new Action(() =>
                    {
                        try
                        {
                            Debug.WriteLine($"[ProviderManager] Displaying confirmation dialog for: {asmName}");
                            bool result = StyledMessageDialog.ShowConfirmation($"A new AI provider was detected\n'{asmName}'.\n\nDo you want to enable it?\n\nIf you do not enable it now, you can do it later in the SmartHopper settings.");
                            Debug.WriteLine($"[ProviderManager] Dialog returned: {result} for: {asmName}");
                            tcs.SetResult(result);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ProviderManager] Dialog error for {asmName}: {ex.Message}");
                            tcs.SetResult(false);
                        }
                    }));

                    bool userConfirmed = await tcs.Task.ConfigureAwait(false);
                    Debug.WriteLine($"[ProviderManager] User confirmed: {userConfirmed} for: {asmName}");
                    settings.TrustedProviders[asmName] = userConfirmed;
                    settings.Save();
                }
                else
                {
                    Debug.WriteLine($"[ProviderManager] Trust already exists for: {asmName} = {settings.TrustedProviders[asmName]}");
                }

                // Load the assembly
                var assembly = Assembly.LoadFrom(assemblyPath);

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

                        Debug.WriteLine($"Successfully registered provider: {provider.Name} from {assembly.GetName().Name}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error creating provider from factory {factoryType.FullName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading provider assembly {assemblyPath}: {ex.Message}");
            }
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
                    Debug.WriteLine($"[ProviderManager] Successfully initialized provider: {provider.Name}");
                }
                catch (Exception ex)
                {
                    var msg = $"[SmartHopper] Error initializing provider {provider.Name}: {ex.Message}";
                    Debug.WriteLine(msg);
                    Rhino.RhinoApp.WriteLine(msg);
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
                    Debug.WriteLine($"Error getting default provider: {ex.Message}");
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
                        Debug.WriteLine($"Provider '{provider.Name}' is no longer trusted, returning null.");
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
            Debug.WriteLine($"[ProviderManager] Updating settings for {providerName} with {settings?.Count ?? 0} values");

            var provider = this.GetProvider(providerName);
            if (provider == null)
            {
                Debug.WriteLine($"[ProviderManager] Provider {providerName} not found.");
                return;
            }

            var ui = ProviderManager.Instance.GetProviderSettings(providerName);
            var descriptors = ui?.GetSettingDescriptors();

            // Validate settings
            if (!ui.ValidateSettings(settings))
            {
                Debug.WriteLine($"[ProviderManager] Settings validation failed for provider {providerName}. Not updating any settings for this provider.");
                return;
            }

            // Log what's being updated
            foreach (var setting in settings)
            {
                // Check if it's a secret to avoid logging sensitive data
                var isSecret = descriptors
                    .FirstOrDefault(d => d.Name == setting.Key)?.IsSecret ?? false;

                // If value is empty, remove it
                if (string.IsNullOrWhiteSpace(setting.Value?.ToString()))
                {
                    Debug.WriteLine($"[ProviderManager] Removing {providerName}.{setting.Key}");

                    SmartHopperSettings.Instance.RemoveSetting(providerName, setting.Key);
                }
                else
                {
                    // Else, set it
                    Debug.WriteLine($"[ProviderManager] Updating {providerName}.{setting.Key} = {(isSecret ? "<secret>" : setting.Value)}");

                    SmartHopperSettings.Instance.SetSetting(providerName, setting.Key, setting.Value);
                }
            }

            // Save settings to disk
            Debug.WriteLine($"[ProviderManager] Saving settings to disk");
            SmartHopperSettings.Instance.Save();

            // Refresh the provider's cached settings (merge with existing to preserve provider-set values)
            var updatedSettings = SmartHopperSettings.Instance.GetProviderSettings(providerName);
            provider.RefreshCachedSettings(updatedSettings);
            Debug.WriteLine($"[ProviderManager] Provider {providerName} settings refreshed with updated values");
        }

        // Implement Authenticode verification using X509Certificate
        private void VerifySignature(string filePath)
        {
            // Authenticode: ensure the certificate matches the host assembly's certificate
            // X509Certificate.CreateFromSignedFile is only supported on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
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
