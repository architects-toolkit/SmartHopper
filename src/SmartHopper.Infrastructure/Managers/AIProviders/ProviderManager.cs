/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Rhino;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Infrastructure.Managers.AIProviders
{
    /// <summary>
    /// Manages the discovery and loading of AI providers.
    /// </summary>
    public class ProviderManager
    {
        private static readonly Lazy<ProviderManager> _instance = new Lazy<ProviderManager>(() => new ProviderManager());

        public static ProviderManager Instance => _instance.Value;

        private readonly Dictionary<string, IAIProvider> _providers = new Dictionary<string, IAIProvider>();
        private readonly Dictionary<string, IAIProviderSettings> _providerSettings = new Dictionary<string, IAIProviderSettings>();
        private readonly Dictionary<string, Assembly> _providerAssemblies = new Dictionary<string, Assembly>();

        private ProviderManager()
        {
            // NOTE: Do NOT automatically call RefreshProviders() here to avoid circular dependencies
            // RefreshProviders() should be called explicitly after initialization
        }

        /// <summary>
        /// Discovers and loads provider assemblies from the same directory as the main application.
        /// </summary>
        private void DiscoverProviders()
        {
            try
            {
                // Get the directory where the current assembly is located
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                string baseDirectory = Path.GetDirectoryName(assemblyLocation);

                // Find all external provider DLLs
                string[] providerFiles = Directory.GetFiles(baseDirectory, "SmartHopper.Providers.*.dll");
                var settings = SmartHopperSettings.Instance;
                foreach (string providerFile in providerFiles)
                {
                    var asmName = Path.GetFileNameWithoutExtension(providerFile);
                    // Skip providers the user has rejected
                    if (settings.TrustedProviders.TryGetValue(asmName, out var isAllowed) && !isAllowed)
                    {
                        Debug.WriteLine($"Provider '{asmName}' rejected, skipping.");
                        continue;
                    }

                    try
                    {
                        LoadProviderAssembly(providerFile);
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
        public void RefreshProviders()
        {
            Debug.WriteLine("[ProviderManager] Starting provider discovery and registration");
            DiscoverProviders();

            // After discovery, refresh settings for all providers
            Debug.WriteLine("[ProviderManager] Provider discovery complete, refreshing settings");
            SmartHopperSettings.Instance.RefreshProvidersLocalStorage();
        }

        /// <summary>
        /// Loads a provider assembly and registers any providers it contains.
        /// </summary>
        /// <param name="assemblyPath">The path to the provider assembly.</param>
        private void LoadProviderAssembly(string assemblyPath)
        {
            try
            {
                // Authenticode signature validation
                try
                {
                    VerifySignature(assemblyPath);
                }
                catch (CryptographicException ex)
                {
                    Debug.WriteLine($"Authenticode signature verification failed for {assemblyPath}: {ex.Message}");
                    RhinoApp.InvokeOnUiThread(new Action(() =>
                    {
                        StyledMessageDialog.ShowError($"Authenticode signature verification failed for provider '{Path.GetFileName(assemblyPath)}'. Please replace it with a file downloaded from official SmartHopper sources.", "SmartHopper");
                    }));
                    return;
                }
                var settings = SmartHopperSettings.Instance;
                var asmName = Path.GetFileNameWithoutExtension(assemblyPath);

                // Skip providers the user has previously rejected
                if (settings.TrustedProviders.TryGetValue(asmName, out var isAllowed) && !isAllowed)
                {
                    Debug.WriteLine($"Provider '{asmName}' previously rejected, skipping.");
                    return;
                }

                // Prompt user for providers with no trust entry
                if (!settings.TrustedProviders.ContainsKey(asmName))
                {
                    var tcs = new TaskCompletionSource<bool>();
                    RhinoApp.InvokeOnUiThread(new Action(() =>
                    {
                        tcs.SetResult(StyledMessageDialog.ShowConfirmation($"Detected new AI provider '{asmName}'. Enable it?"));
                    }));
                    if (tcs.Task.Result)
                    {
                        settings.TrustedProviders[asmName] = true;
                        settings.Save();
                    }
                    else
                    {
                        settings.TrustedProviders[asmName] = false;
                        settings.Save();
                        Debug.WriteLine($"Provider '{asmName}' not allowed by user, skipping.");
                        return;
                    }
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
                        RegisterProvider(provider, providerSettings, assembly);

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
            _providers[provider.Name] = provider;
            _providerSettings[provider.Name] = settings;

            // Store the assembly for future reference
            if (assembly != null && !_providerAssemblies.ContainsKey(provider.Name))
            {
                _providerAssemblies[provider.Name] = assembly;
            }

            provider.InitializeProvider();
        }

        /// <summary>
        /// Gets all registered providers.
        /// </summary>
        /// <returns>A collection of registered providers.</returns>
        public IEnumerable<IAIProvider> GetProviders()
        {
            return _providers.Values;
        }

        /// <summary>
        /// Gets a provider by name.
        /// </summary>
        /// <param name="providerName">Name of the provider to get.</param>
        /// <returns>The provider, or null if not found.</returns>
        public IAIProvider GetProvider(string providerName)
        {
            if (string.IsNullOrEmpty(providerName)) return null;

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
                    return _providers.Values.FirstOrDefault();
                }
                providerName = defaultProviderName;
            }

            // Try to get the provider
            if (_providers.TryGetValue(providerName, out var provider))
            {
                // NOTE: Don't refresh settings here to avoid circular dependencies
                // The provider's settings will be refreshed when needed by specific operations
                return provider;
            }

            return null;
        }

        /// <summary>
        /// Refreshes a provider with current settings from SmartHopperSettings.
        /// </summary>
        /// <param name="provider">The provider to refresh.</param>
        private void RefreshProviderSettings(IAIProvider provider)
        {
            try
            {
                if (provider == null) return;

                // Check if settings are available before calling GetProviderSettings
                if (SmartHopperSettings.Instance != null)
                {
                    provider.InitializeProvider();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProviderManager] Error refreshing provider settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the settings for a provider.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>The provider settings, or null if not found.</returns>
        public IAIProviderSettings GetProviderSettings(string providerName)
        {
            return _providerSettings.TryGetValue(providerName, out var settings) ? settings : null;
        }

        /// <summary>
        /// Gets the assembly containing a provider.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>The assembly, or null if not found.</returns>
        public Assembly GetProviderAssembly(string providerName)
        {
            return _providerAssemblies.TryGetValue(providerName, out var assembly) ? assembly : null;
        }

        /// <summary>
        /// Gets the icon for the specified AI provider
        /// </summary>
        /// <param name="providerName">Name of the provider</param>
        /// <returns>The provider's icon or null if not found</returns>
        public Image GetProviderIcon(string providerName)
        {
            var provider = GetProvider(providerName);
            return provider?.Icon;
        }

        /// <summary>
        /// Gets the default AI provider from settings, or the first available provider if not set.
        /// </summary>
        /// <returns>The default AI provider name</returns>
        public string GetDefaultAIProvider()
        {
            var settings = SmartHopperSettings.Instance;
            if (!string.IsNullOrWhiteSpace(settings.DefaultAIProvider) && _providers.ContainsKey(settings.DefaultAIProvider))
            {
                return settings.DefaultAIProvider;
            }

            // Fallback to first provider if default not set or invalid
            return _providers.Keys.FirstOrDefault() ?? string.Empty;
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
                // Else, set it
                else
                {
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
            catch (Exception ex)
            {
                throw new CryptographicException($"Authenticode signature verification failed for {Path.GetFileName(filePath)}: {ex.Message}", ex);
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
            catch (Exception ex)
            {
                throw new SecurityException($"Strong-name signature verification failed for {Path.GetFileName(filePath)}: {ex.Message}", ex);
            }
        }
    }
}
