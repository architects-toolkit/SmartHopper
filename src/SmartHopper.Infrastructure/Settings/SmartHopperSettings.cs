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
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.Managers.AIProviders;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Infrastructure.Settings
{
    public class SmartHopperSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Grasshopper",
            "SmartHopper.json");

        // Use a constant key and IV for encryption (TODO: in a production environment, these should be secured properly)
        private static readonly byte[] key = new byte[] { 132, 42, 53, 84, 75, 46, 97, 88, 109, 110, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
        private static readonly byte[] iv = new byte[] { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116 };

        [JsonProperty]
        internal Dictionary<string, Dictionary<string, object>> ProviderSettings { get; set; }

        [JsonProperty]
        public int DebounceTime { get; set; }

        [JsonProperty]
        public string DefaultAIProvider { get; set; }

        [JsonProperty]
        public Dictionary<string, bool> TrustedProviders { get; set; }

        [JsonProperty(nameof(SmartHopperAssistant))]
        public SmartHopperAssistantSettings SmartHopperAssistant { get; set; }

        private static SmartHopperSettings? instance;

        public static SmartHopperSettings Instance => instance ??= Load();

        public SmartHopperSettings()
        {
            this.ProviderSettings = new Dictionary<string, Dictionary<string, object>>();
            this.DebounceTime = 1000;
            this.DefaultAIProvider = string.Empty;
            this.TrustedProviders = new Dictionary<string, bool>();
            this.SmartHopperAssistant = new SmartHopperAssistantSettings();
        }

        /// <summary>
        /// Gets a setting for the specified provider.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <param name="settingName">The name of the setting.</param>
        /// <returns>The setting value, or null if not found.</returns>
        internal object GetSetting(string providerName, string settingName)
        {
            if (this.ProviderSettings.TryGetValue(providerName, out var settings) &&
                settings.TryGetValue(settingName, out var value))
            {
                var descriptors = GetProviderDescriptors(providerName);
                var descriptor = descriptors.FirstOrDefault(d => d.Name == settingName);

                if (descriptor?.IsSecret == true && value != null)
                {
                    Debug.WriteLine($"[Settings] Found {providerName}.{settingName} in storage (secret)");
                    return Decrypt(value.ToString());
                }

                Debug.WriteLine($"[Settings] Found {providerName}.{settingName} in storage = {value}");
                return value;
            }

            // If setting doesn't exist, try to get default value from descriptor
            var allDescriptors = GetProviderDescriptors(providerName);
            var missingDescriptor = allDescriptors.FirstOrDefault(d => d.Name == settingName);

            if (missingDescriptor?.DefaultValue != null)
            {
                Debug.WriteLine($"[Settings] Using default for {providerName}.{settingName} = {missingDescriptor.DefaultValue}");
                return missingDescriptor.DefaultValue;
            }

            Debug.WriteLine($"[Settings] No value or default found for {providerName}.{settingName}");
            return null;
        }

        /// <summary>
        /// Sets a setting for the specified provider.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <param name="settingName">The name of the setting.</param>
        /// <param name="value">The setting value.</param>
        internal void SetSetting(string providerName, string settingName, object value)
        {
            if (!this.ProviderSettings.TryGetValue(providerName, out Dictionary<string, object>? settingsValue))
            {
                settingsValue = new Dictionary<string, object>();
                this.ProviderSettings[providerName] = settingsValue;
            }

            var descriptors = GetProviderDescriptors(providerName);
            var descriptor = descriptors.FirstOrDefault(d => d.Name == settingName);

            if (descriptor?.IsSecret == true && value != null)
            {
                Debug.WriteLine($"[Settings] Storing encrypted secret for {providerName}.{settingName}");
                settingsValue[settingName] = Encrypt(settingsValue.ToString());
            }
            else
            {
                Debug.WriteLine($"[Settings] Storing value for {providerName}.{settingName} = {value}");
                settingsValue[settingName] = value;
            }
        }

        /// <summary>
        /// Removes a setting for the specified provider.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <param name="settingName">The name of the setting.</param>
        internal void RemoveSetting(string providerName, string settingName)
        {
            if (this.ProviderSettings.TryGetValue(providerName, out var settings))
            {
                settings.Remove(settingName);
            }
        }

        /// <summary>
        /// Retrieves all decrypted settings for the specified provider.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>Dictionary of setting names and their values.</returns>
        public Dictionary<string, object> GetProviderSettings(string providerName)
        {
            var settingsDict = new Dictionary<string, object>();
            var descriptors = GetProviderDescriptors(providerName);
            foreach (var descriptor in descriptors)
            {
                var value = this.GetSetting(providerName, descriptor.Name);
                if (value != null)
                {
                    settingsDict[descriptor.Name] = value;
                }
            }

            return settingsDict;
        }

        /// <summary>
        /// Checks the integrity of the settings.
        /// </summary>
        /// <returns>True if all settings are valid, false otherwise.</returns>
        internal bool IntegrityCheck()
        {
            // Skip integrity check if no providers loaded yet
            var providers = ProviderManager.Instance.GetProviders(includeUntrusted: true);
            if (providers == null || !providers.Any())
            {
                Debug.WriteLine("[Settings] Skipping integrity check: no providers loaded yet.");
                return true;
            }

            bool isValid = true;
            var invalidSettings = new List<(string Provider, string Setting)>();

            foreach (var provider in this.ProviderSettings.Keys.ToList())
            {
                var descriptors = GetProviderDescriptors(provider);
                if (descriptors == null || !descriptors.Any())
                {
                    continue;
                }

                // Check for unknown settings
                var knownSettingNames = descriptors.Select(d => d.Name).ToList();
                var unknownSettings = this.ProviderSettings[provider].Keys
                    .Where(key => !knownSettingNames.Contains(key))
                    .ToList();

                foreach (var unknown in unknownSettings)
                {
                    Debug.WriteLine($"Unknown setting found for provider {provider}: {unknown}");
                    invalidSettings.Add((provider, unknown));
                    isValid = false;
                }
            }

            // Log all invalid settings
            if (invalidSettings.Count > 0)
            {
                Debug.WriteLine($"Found {invalidSettings.Count} invalid settings:");
                foreach (var (provider, setting) in invalidSettings)
                {
                    Debug.WriteLine($"  - {provider}.{setting}");
                }
            }

            return isValid;
        }

        /// <summary>
        /// Refreshes all providers with their current settings.
        /// </summary>
        internal void RefreshProvidersLocalStorage()
        {
            try
            {
                Debug.WriteLine("Refreshing providers with settings from local storage");

                // Check that the ProviderManager instance exists first
                if (ProviderManager.Instance == null)
                {
                    Debug.WriteLine("Cannot refresh providers: ProviderManager.Instance is null");
                    return;
                }

                // Get providers safely, avoiding potential circular initialization
                var providers = ProviderManager.Instance.GetProviders(includeUntrusted: true);
                if (providers == null)
                {
                    Debug.WriteLine("No providers available to refresh");
                    return;
                }

                // Refresh provider settings
                foreach (var provider in providers)
                {
                    try
                    {
                        var providerSettings = new Dictionary<string, object>();

                        // Get descriptors for this provider
                        var descriptors = provider.GetSettingDescriptors();

                        // For each descriptor, get the setting value
                        foreach (var descriptor in descriptors)
                        {
                            var value = this.GetSetting(provider.Name, descriptor.Name);
                            if (value != null)
                            {
                                providerSettings[descriptor.Name] = value;
                                bool hasStorage = this.ProviderSettings
                                    .TryGetValue(provider.Name, out var storedSettings)
                                    && storedSettings != null
                                    && storedSettings.ContainsKey(descriptor.Name);

                                string sourceInfo = hasStorage
                                    ? "(from storage)"
                                    : "(from default)";

                                Debug.WriteLine(
                                    $"Setting {provider.Name}.{descriptor.Name} = " +
                                    $"{(descriptor.IsSecret ? "<secret>" : value)} {sourceInfo}");
                            }
                        }

                        // Initialize the provider with the settings
                        Debug.WriteLine($"Initializing provider {provider.Name} with {providerSettings.Count} settings");
                        provider.RefreshCachedSettings(providerSettings);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error refreshing provider {provider.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing provider settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Encrypts a string using AES encryption.
        /// </summary>
        /// <param name="plainText">The plain text to encrypt.</param>
        /// <returns>The encrypted string.</returns>
        private static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return plainText;
            }

            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

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
            catch (Exception ex)
            {
                Debug.WriteLine($"Error encrypting string: {ex.Message}");

                // If encryption fails, return the original string
                return plainText;
            }
        }

        /// <summary>
        /// Decrypts a string using AES encryption.
        /// </summary>
        /// <param name="encryptedText">The encrypted text.</param>
        /// <returns>The decrypted string.</returns>
        private static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
            {
                return encryptedText;
            }

            try
            {
                byte[] cipherText = Convert.FromBase64String(encryptedText);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    using (var decryptor = aes.CreateDecryptor())
                    using (var msDecrypt = new MemoryStream(cipherText))
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error decrypting string: {ex.Message}");

                // If decryption fails, return the original string
                return encryptedText;
            }
        }

        private static IEnumerable<SettingDescriptor> GetProviderDescriptors(string providerName)
        {
            var ui = ProviderManager.Instance.GetProviderSettings(providerName);
            if (ui == null)
            {
                return Enumerable.Empty<SettingDescriptor>();
            }

            return ui.GetSettingDescriptors();
        }

        /// <summary>
        /// Loads the settings from disk.
        /// </summary>
        /// <returns>The loaded settings.</returns>
        public static SmartHopperSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonConvert.DeserializeObject<SmartHopperSettings>(json);

                    if (settings != null)
                    {
                        // Don't run IntegrityCheck during Load to avoid circular dependency with ProviderManager.
                        // IntegrityCheck should be called explicitly after both SmartHopperSettings and ProviderManager
                        // are fully initialized.
                        // Don't automatically refresh providers here to avoid circular dependency.
                        // This should happen explicitly after both SmartHopperSettings and ProviderManager
                        // are fully initialized.
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
            }

            return new SmartHopperSettings();
        }

        /// <summary>
        /// Saves the settings to disk.
        /// </summary>
        public void Save()
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Log what we're about to save
                Debug.WriteLine($"[Settings] Saving settings to {SettingsPath}");
                Debug.WriteLine($"[Settings] DefaultAIProvider: {this.DefaultAIProvider}");
                Debug.WriteLine($"[Settings] DebounceTime: {this.DebounceTime}");

                if (this.ProviderSettings != null)
                {
                    foreach (var providerKvp in this.ProviderSettings)
                    {
                        Debug.WriteLine($"[Settings] Provider '{providerKvp.Key}' has {providerKvp.Value?.Count ?? 0} settings");
                        if (providerKvp.Value != null)
                        {
                            foreach (var settingKvp in providerKvp.Value)
                            {
                                // Don't log the actual values of secrets
                                var isSecret = GetProviderDescriptors(providerKvp.Key)
                                    .FirstOrDefault(d => d.Name == settingKvp.Key)?.IsSecret ?? false;

                                Debug.WriteLine($"[Settings]   - {settingKvp.Key} = {(isSecret ? "<secret>" : settingKvp.Value)}");
                            }
                        }
                    }
                }

                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
                Debug.WriteLine($"[Settings] Settings saved successfully ({json.Length} bytes)");

                // After saving, refresh all providers
                this.RefreshProvidersLocalStorage();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}
