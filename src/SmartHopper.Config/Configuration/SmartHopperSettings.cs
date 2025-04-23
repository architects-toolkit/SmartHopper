/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Managers;
using SmartHopper.Config.Models;

namespace SmartHopper.Config.Configuration
{
    public class SmartHopperSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Grasshopper",
            "SmartHopper.json");

        internal Dictionary<string, Dictionary<string, object>> ProviderSettings { get; set; }

        public int DebounceTime { get; set; }

        /// <summary>
        /// The default AI provider to use when not explicitly specified in components.
        /// If not set or the provider doesn't exist, the first available provider will be used.
        /// </summary>
        public string DefaultAIProvider { get; set; }

        /// <summary>
        /// Maps provider names to trust status: true=allowed, false=disallowed.
        /// </summary>
        public Dictionary<string, bool> TrustedProviders { get; set; }

        public SmartHopperSettings()
        {
            ProviderSettings = new Dictionary<string, Dictionary<string, object>>();
            DebounceTime = 1000;
            DefaultAIProvider = string.Empty;
            TrustedProviders = new Dictionary<string, bool>();
        }

        // Use a constant key and IV for encryption (TODO: these could be moved to secure configuration)
        private static readonly byte[] _key = new byte[] { 132, 42, 53, 84, 75, 46, 97, 88, 109, 110, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
        private static readonly byte[] _iv = new byte[] { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116 };

        private static string ProtectString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = _key;
                    aes.IV = _iv;

                    using (var encryptor = aes.CreateEncryptor())
                    using (var msEncrypt = new MemoryStream())
                    {
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        using (var swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }

                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
            catch
            {
                // If encryption fails, return the original string
                return plainText;
            }
        }

        private static string UnprotectString(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return encryptedText;

            try
            {
                byte[] cipherText = Convert.FromBase64String(encryptedText);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = _key;
                    aes.IV = _iv;

                    using (var decryptor = aes.CreateDecryptor())
                    using (var msDecrypt = new MemoryStream(cipherText))
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
            catch
            {
                // If decryption fails, return the original string
                return encryptedText;
            }
        }

        private static Dictionary<string, Dictionary<string, object>> EncryptSensitiveSettings(Dictionary<string, Dictionary<string, object>> settings)
        {
            var encryptedSettings = new Dictionary<string, Dictionary<string, object>>();

            foreach (var provider in settings)
            {
                encryptedSettings[provider.Key] = new Dictionary<string, object>();
                var descriptors = GetProviderDescriptors(provider.Key);

                foreach (var setting in provider.Value)
                {
                    var descriptor = descriptors.FirstOrDefault(d => d.Name == setting.Key);
                    if (descriptor?.IsSecret == true && setting.Value != null)
                    {
                        encryptedSettings[provider.Key][setting.Key] = ProtectString(setting.Value.ToString());
                    }
                    else
                    {
                        encryptedSettings[provider.Key][setting.Key] = setting.Value;
                    }
                }
            }

            return encryptedSettings;
        }

        private Dictionary<string, Dictionary<string, object>> DecryptSensitiveSettings(Dictionary<string, Dictionary<string, object>> settings)
        {
            var decryptedSettings = new Dictionary<string, Dictionary<string, object>>();

            foreach (var provider in settings)
            {
                decryptedSettings[provider.Key] = new Dictionary<string, object>();
                var descriptors = GetProviderDescriptors(provider.Key);
                Debug.WriteLine($"[SmartHopperSettings] Decrypting settings for provider {provider.Key}. Found {provider.Value.Count} settings");

                foreach (var setting in provider.Value)
                {
                    var descriptor = descriptors.FirstOrDefault(d => d.Name == setting.Key);
                    string settingType = setting.Value?.GetType().Name ?? "null";
                    Debug.WriteLine($"[SmartHopperSettings] Processing setting {setting.Key}, type: {settingType}, IsSecret: {descriptor?.IsSecret}");
                    
                    if (descriptor?.IsSecret == true && setting.Value != null)
                    {
                        string decrypted = UnprotectString(setting.Value.ToString());
                        // For API keys, we need to ensure we keep the decrypted value, not just a boolean indicator
                        decryptedSettings[provider.Key][setting.Key] = decrypted;
                        Debug.WriteLine($"[SmartHopperSettings] Decrypted secret setting {setting.Key}");
                    }
                    else
                    {
                        decryptedSettings[provider.Key][setting.Key] = setting.Value;
                        Debug.WriteLine($"[SmartHopperSettings] Copied non-secret setting {setting.Key}: {setting.Value}");
                    }
                }
            }

            return decryptedSettings;
        }

        private static IEnumerable<SettingDescriptor> GetProviderDescriptors(string providerName)
        {
            var provider = ProviderManager.Instance.GetProvider(providerName);
            if (provider != null)
            {
                return provider.GetSettingDescriptors();
            }

            // Fallback to old method for backward compatibility
            var assembly = Assembly.GetExecutingAssembly();
            var providerType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IAIProvider).IsAssignableFrom(t) &&
                                   !t.IsInterface &&
                                   !t.IsAbstract &&
                                   t.GetProperty("Name")?.GetValue(null)?.ToString() == providerName);

            if (providerType != null)
            {
                var instanceProperty = providerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty != null)
                {
                    var oldProvider = instanceProperty.GetValue(null) as IAIProvider;
                    if (oldProvider != null)
                    {
                        return oldProvider.GetSettingDescriptors();
                    }
                }
            }

            return Enumerable.Empty<SettingDescriptor>();
        }

        public static SmartHopperSettings Load()
        {
            if (!File.Exists(SettingsPath))
            {
                Debug.WriteLine($"[SmartHopperSettings] Settings file not found at {SettingsPath}, using default settings.");
                return new SmartHopperSettings();
            }

            Debug.WriteLine($"[SmartHopperSettings] Loading settings from {SettingsPath}");
            try
            {
                var json = File.ReadAllText(SettingsPath);
                Debug.WriteLine($"[SmartHopperSettings] Read settings JSON: {json}");

                var settings = JsonConvert.DeserializeObject<SmartHopperSettings>(json) ?? new SmartHopperSettings();
                settings.ProviderSettings = settings.DecryptSensitiveSettings(settings.ProviderSettings);
                // Ensure TrustedProviders is initialized
                settings.TrustedProviders ??= new Dictionary<string, bool>();
                Debug.WriteLine($"[SmartHopperSettings] Settings loaded: DebounceTime={settings.DebounceTime}, DefaultAIProvider='{settings.DefaultAIProvider}', ProviderSettings count={settings.ProviderSettings.Count}, TrustedProviders count={settings.TrustedProviders.Count}");
                return settings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SmartHopperSettings] Error loading settings from {SettingsPath}: {ex.Message}");
                return new SmartHopperSettings();
            }
        }

        /// <summary>
        /// Updates the settings file by merging the provided JSON patch.
        /// </summary>
        /// <param name="jsonPatch">JSON string with key/value pairs to update.</param>
        public static void Update(string jsonPatch)
        {
            try
            {
                Debug.WriteLine($"[SmartHopperSettings] Updating settings with patch: {jsonPatch}");
                var patch = JObject.Parse(jsonPatch);
                // Encrypt any sensitive provider settings in patch
                if (patch.TryGetValue("ProviderSettings", out var psToken))
                {
                    var rawSettings = psToken.ToObject<Dictionary<string, Dictionary<string, object>>>();
                    var encrypted = EncryptSensitiveSettings(rawSettings);
                    patch["ProviderSettings"] = JObject.FromObject(encrypted);
                }
                var existing = File.Exists(SettingsPath)
                    ? JObject.Parse(File.ReadAllText(SettingsPath))
                    : new JObject();
                Debug.WriteLine($"[SmartHopperSettings] Existing settings JSON: {existing.ToString()}");
                existing.Merge(patch, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Union,
                    MergeNullValueHandling = MergeNullValueHandling.Ignore
                });
                File.WriteAllText(SettingsPath, existing.ToString(Formatting.Indented));
                Debug.WriteLine($"[SmartHopperSettings] Settings saved to {SettingsPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SmartHopperSettings] Error updating settings file: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves current settings to disk, merging only updated global settings and preserving existing ones.
        /// </summary>
        public void Save()
        {
            Debug.WriteLine($"[SmartHopperSettings] Saving settings: DebounceTime={DebounceTime}, DefaultAIProvider='{DefaultAIProvider}', TrustedProviders count={TrustedProviders.Count}");
            // Build JSON patch for saving global settings only
            var patch = new JObject
            {
                ["DebounceTime"]        = DebounceTime,
                ["DefaultAIProvider"]   = DefaultAIProvider,
                ["TrustedProviders"]    = JObject.FromObject(TrustedProviders)
            };
            Update(patch.ToString());
        }

        /// <summary>
        /// Updates the provider settings for a specific provider.
        /// </summary>
        /// <param name="providerName">Name of the provider.</param>
        /// <param name="settings">Plain settings dictionary.</param>
        internal static void UpdateProviderSettings(string providerName, Dictionary<string, object> settings)
        {
            Debug.WriteLine($"[SmartHopperSettings] Updating provider settings for '{providerName}' with {settings?.Count ?? 0} values");
            // Use Update to merge and encrypt this provider's settings
            var raw = new Dictionary<string, Dictionary<string, object>> { [providerName] = settings };
            var patch = new JObject { ["ProviderSettings"] = JObject.FromObject(raw) };
            Update(patch.ToString());
        }

        /// <summary>
        /// Loads settings for a specific provider.
        /// </summary>
        /// <param name="providerName">Name of the provider.</param>
        /// <returns>Plain settings dictionary.</returns>
        internal static Dictionary<string, object> LoadProviderSettings(string providerName)
        {
            Debug.WriteLine($"[SmartHopperSettings] Loading settings for provider '{providerName}' only");
            if (!File.Exists(SettingsPath))
            {
                Debug.WriteLine($"[SmartHopperSettings] Settings file not found at {SettingsPath}, returning defaults.");
                return new Dictionary<string, object>();
            }

            try
            {
                var json = File.ReadAllText(SettingsPath);
                var jObj = JObject.Parse(json);
                var providerToken = jObj["ProviderSettings"]?[providerName];
                if (providerToken == null)
                {
                    Debug.WriteLine($"[SmartHopperSettings] No settings found for provider '{providerName}'.");
                    return new Dictionary<string, object>();
                }

                var rawDict = providerToken.ToObject<Dictionary<string, object>>();
                var descriptors = ProviderManager.Instance.GetProvider(providerName)?.GetSettingDescriptors() ?? Enumerable.Empty<SettingDescriptor>();
                var decrypted = new Dictionary<string, object>();

                foreach (var kv in rawDict)
                {
                    var desc = descriptors.FirstOrDefault(d => d.Name == kv.Key);
                    if (desc?.IsSecret == true && kv.Value != null)
                        decrypted[kv.Key] = UnprotectString(kv.Value.ToString());
                    else
                        decrypted[kv.Key] = kv.Value;
                }

                Debug.WriteLine($"[SmartHopperSettings] Loaded settings for '{providerName}': {string.Join(", ", decrypted.Select(kv => kv.Key + "=" + kv.Value))}");
                return decrypted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SmartHopperSettings] Error loading settings for '{providerName}': {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Discovers available AI providers.
        /// </summary>
        /// <returns>Collection of discovered providers</returns>
        public static IEnumerable<IAIProvider> DiscoverProviders()
        {
            // Use the ProviderManager to discover providers
            return ProviderManager.Instance.GetProviders();
        }

        /// <summary>
        /// Gets the default AI provider from settings, or the first available provider if not set.
        /// </summary>
        /// <returns>The default AI provider name</returns>
        public string GetDefaultAIProvider()
        {
            return ProviderManager.Instance.GetDefaultAIProvider();
        }

        /// <summary>
        /// Gets the icon for the specified AI provider
        /// </summary>
        /// <param name="providerName">Name of the provider</param>
        /// <returns>The provider's icon or null if not found</returns>
        public static Image GetProviderIcon(string providerName)
        {
            return ProviderManager.Instance.GetProviderIcon(providerName);
        }
    }
}
