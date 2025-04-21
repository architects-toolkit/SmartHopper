/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Config.Configuration;
using SmartHopper.Config.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Rhino;
using Eto.Forms;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.Pkcs;
using System.Drawing;

namespace SmartHopper.Config.Managers
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
            // Register built-in providers
            RegisterBuiltInProviders();

            // Discover and load external providers
            DiscoverProviders();
        }

        /// <summary>
        /// Registers the built-in providers that are part of the core SmartHopper.Config assembly.
        /// </summary>
        private void RegisterBuiltInProviders()
        {
            // This method would register any providers that are still part of the core assembly
            var assembly = Assembly.GetExecutingAssembly();
            var providerTypes = assembly.GetTypes()
                .Where(t => typeof(IAIProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in providerTypes)
            {
                try
                {
                    var instanceProperty = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProperty != null)
                    {
                        var provider = instanceProperty.GetValue(null) as IAIProvider;
                        if (provider != null)
                        {
                            // For built-in providers, we need to create settings manually
                            // This is a temporary solution until all providers are moved to separate assemblies
                            var settingsTypeName = $"{provider.Name}Settings";
                            var settingsType = assembly.GetTypes()
                                .FirstOrDefault(t => typeof(IAIProviderSettings).IsAssignableFrom(t) && 
                                                   !t.IsInterface && 
                                                   !t.IsAbstract && 
                                                   t.Name == settingsTypeName);

                            if (settingsType != null)
                            {
                                var settings = Activator.CreateInstance(settingsType, provider) as IAIProviderSettings;
                                if (settings != null)
                                {
                                    RegisterProvider(provider, settings, assembly);
                                    Debug.WriteLine($"Registered built-in provider: {provider.Name}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error registering built-in provider {type.Name}: {ex.Message}");
                }
            }
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
                var settings = SmartHopperSettings.Load();
                foreach (string providerFile in providerFiles)
                {
                    var asmName = Path.GetFileNameWithoutExtension(providerFile);
                    if (!settings.AllowedProviders.Contains(asmName))
                    {
                        Debug.WriteLine($"Provider '{asmName}' not allowed, skipping.");
                        continue;
                    }

                    // Removed manifest verification in favor of authenticode-signed providers
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
        /// Loads a provider assembly and registers any providers it contains.
        /// </summary>
        /// <param name="assemblyPath">The path to the provider assembly.</param>
        private void LoadProviderAssembly(string assemblyPath)
        {
            try
            {
                // Authenticode signature validation
                VerifySignature(assemblyPath);
                var settings = SmartHopperSettings.Load();
                var asmName = Path.GetFileNameWithoutExtension(assemblyPath);
                if (!settings.AllowedProviders.Contains(asmName))
                {
                    Debug.WriteLine($"Provider '{asmName}' not allowed, skipping.");
                    return;
                }
                
                // Prompt user to trust newly discovered external AI providers
                var tcs = new TaskCompletionSource<bool>();
                RhinoApp.InvokeOnUiThread(new Action(() => {
                    var result = MessageBox.Show($"Detected new AI provider '{asmName}'. Enable it?", MessageBoxButtons.YesNo);
                    tcs.SetResult(result == DialogResult.Yes);
                }));
                if (!tcs.Task.Result)
                {
                    Debug.WriteLine($"Provider '{asmName}' not allowed by user, skipping.");
                    return;
                }
                settings.AllowedProviders.Add(asmName);
                settings.Save();
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
        /// Registers a provider and its settings.
        /// </summary>
        /// <param name="provider">The provider to register.</param>
        /// <param name="settings">The provider settings to register.</param>
        /// <param name="assembly">The assembly containing the provider.</param>
        private void RegisterProvider(IAIProvider provider, IAIProviderSettings settings, Assembly assembly)
        {
            // Security check: only accept providers from assemblies signed with our key
            var pkt = assembly.GetName().GetPublicKeyToken();
            var trustedPkt = Assembly.GetExecutingAssembly().GetName().GetPublicKeyToken();
            if (pkt == null || trustedPkt == null || !pkt.SequenceEqual(trustedPkt))
            {
                Debug.WriteLine($"Untrusted provider assembly '{assembly.FullName}', skipping registration.");
                return;
            }
            if (provider == null || settings == null)
                return;

            // Only register providers that are enabled
            if (!provider.IsEnabled)
            {
                Debug.WriteLine($"Provider {provider.Name} is disabled and will not be registered.");
                return;
            }

            string providerName = provider.Name;
            if (!_providers.ContainsKey(providerName))
            {
                _providers[providerName] = provider;
                _providerSettings[providerName] = settings;
                _providerAssemblies[providerName] = assembly;
                Debug.WriteLine($"Registered provider: {providerName}");
            }
            else
            {
                Debug.WriteLine($"Provider {providerName} is already registered.");
                // Log a more visible warning when a duplicate provider is encountered
                Rhino.RhinoApp.WriteLine($"WARNING: Duplicate AI provider '{providerName}' detected. Only the first registered provider will be used.");
            }
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
        /// <param name="name">The name of the provider.</param>
        /// <returns>The provider, or null if not found.</returns>
        public IAIProvider GetProvider(string name)
        {
            return _providers.TryGetValue(name, out var provider) ? provider : null;
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
            if (string.IsNullOrEmpty(providerName))
                return null;

            var provider = GetProvider(providerName);
            return provider?.Icon;
        }

        /// <summary>
        /// Gets the default AI provider from settings, or the first available provider if not set.
        /// </summary>
        /// <returns>The default AI provider name</returns>
        public string GetDefaultAIProvider()
        {
            var settings = SmartHopperSettings.Load();
            var providers = GetProviders().ToList();
            
            // If the DefaultAIProvider is set and exists in the available providers, use it
            if (!string.IsNullOrEmpty(settings.DefaultAIProvider) && 
                providers.Any(p => p.Name == settings.DefaultAIProvider))
            {
                return settings.DefaultAIProvider;
            }
            
            // Otherwise, return the first available provider or empty string if none
            return providers.Any() ? providers.First().Name : string.Empty;
        }

        // Implement Authenticode verification using SignedCms
        private static void VerifySignature(string assemblyPath)
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            var certDir = peReader.PEHeaders.PEHeader.CertificateTableDirectory;
            if (certDir.Size == 0)
                throw new CryptographicException($"No Authenticode signature found on {assemblyPath}");
            stream.Position = certDir.RelativeVirtualAddress;
            var rawData = new byte[certDir.Size];
            stream.Read(rawData, 0, rawData.Length);
            var pkcs7Data = new byte[certDir.Size - 8];
            Array.Copy(rawData, 8, pkcs7Data, 0, pkcs7Data.Length);
            var cms = new SignedCms();
            cms.Decode(pkcs7Data);
            cms.CheckSignature(true);
        }
    }
}
