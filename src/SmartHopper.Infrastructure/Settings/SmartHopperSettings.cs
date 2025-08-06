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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Infrastructure.Settings
{
    public class SmartHopperSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Grasshopper",
            "SmartHopper.json");

        // Legacy encryption keys for migration purposes only
        private static readonly byte[] LegacyKey = new byte[] { 132, 42, 53, 84, 75, 46, 97, 88, 109, 110, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 };
        private static readonly byte[] LegacyIv = new byte[] { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116 };

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

        /// <summary>
        /// Gets or sets version of the encryption method used. 1 = legacy AES, 2 = OS secure store.
        /// </summary>
        [JsonProperty]
        public int EncryptionVersion { get; set; } = 1;

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
            // Ensure migration is triggered before storing new values
            if (this.EncryptionVersion < 2)
            {
                try
                {
                    this.MigrateEncryption();
                }
                catch (Exception migrationEx)
                {
                    Debug.WriteLine($"[SetSetting] Migration failed: {migrationEx.Message}");
                }
            }

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
                settingsValue[settingName] = Encrypt(value.ToString());
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
        /// Encrypts a string using OS secure store (DPAPI/Keychain) protected key.
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
                var key = GetOrCreateEncryptionKey();
                if (key == null)
                {
                    Debug.WriteLine("[Encryption] Could not get encryption key, using legacy encryption");
                    return EncryptLegacy(plainText);
                }

                return EncryptWithSecureKey(plainText, key);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Encryption] Error encrypting with secure key: {ex.Message}");

                // Fallback to legacy encryption
                return EncryptLegacy(plainText);
            }
        }

        /// <summary>
        /// Decrypts a string using OS secure store or legacy decryption with automatic migration.
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
                // Try OS secure store decryption first (new format starts with "SH02:")
                if (encryptedText.StartsWith("SH02:"))
                {
                    var key = GetOrCreateEncryptionKey();
                    if (key != null)
                    {
                        return DecryptWithSecureKey(encryptedText.Substring(5), key);
                    }
                    else
                    {
                        Debug.WriteLine("[Decryption] Encryption key not found for decryption");
                        return encryptedText;
                    }
                }
                else
                {
                    // Try legacy decryption for backwards compatibility
                    return DecryptLegacy(encryptedText);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Decryption] Error decrypting: {ex.Message}");

                // If decryption fails, return the original string
                return encryptedText;
            }
        }

        /// <summary>
        /// Gets or creates the encryption key from OS secure store (DPAPI/Keychain).
        /// </summary>
        /// <returns>The encryption key, or null if not available.</returns>
        private static byte[] GetOrCreateEncryptionKey()
        {
            const string keyName = "SmartHopper.EncryptionKey";

            try
            {
                // Try to retrieve existing key first
                var existingKey = GetSecureData(keyName);
                if (existingKey != null && existingKey.Length == 32) // 256-bit key
                {
                    Debug.WriteLine("[SecureStore] Retrieved existing encryption key from OS secure store");
                    return existingKey;
                }

                // Generate new 256-bit encryption key
                using (var rng = RandomNumberGenerator.Create())
                {
                    var newKey = new byte[32]; // 256 bits
                    rng.GetBytes(newKey);

                    // Store the key securely
                    if (StoreSecureData(keyName, newKey))
                    {
                        Debug.WriteLine("[SecureStore] Generated and stored new encryption key in OS secure store");
                        return newKey;
                    }
                    else
                    {
                        Debug.WriteLine("[SecureStore] Failed to store encryption key in OS secure store");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecureStore] Error getting/creating encryption key: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Encrypts data using AES with the OS secure store protected key.
        /// </summary>
        /// <param name="plainText">The plain text to encrypt.</param>
        /// <param name="key">The encryption key from OS secure store.</param>
        /// <returns>The encrypted string with SH02: prefix.</returns>
        private static string EncryptWithSecureKey(string plainText, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();

                // Encrypt data with AES
                byte[] encryptedData;
                using (var encryptor = aes.CreateEncryptor())
                using (var msEncrypt = new MemoryStream())
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    csEncrypt.Write(plainBytes, 0, plainBytes.Length);
                    csEncrypt.FlushFinalBlock();
                    encryptedData = msEncrypt.ToArray();
                }

                // Combine: [iv][encrypted_data]
                var result = new List<byte>();
                result.AddRange(aes.IV);
                result.AddRange(encryptedData);

                return "SH02:" + Convert.ToBase64String(result.ToArray());
            }
        }

        /// <summary>
        /// Decrypts data using AES with the OS secure store protected key.
        /// </summary>
        /// <param name="encryptedData">The encrypted data (without SH02: prefix).</param>
        /// <param name="key">The encryption key from OS secure store.</param>
        /// <returns>The decrypted string.</returns>
        private static string DecryptWithSecureKey(string encryptedData, byte[] key)
        {
            byte[] envelope = Convert.FromBase64String(encryptedData);

            // Extract components: [iv][encrypted_data]
            byte[] iv = new byte[16]; // AES IV is always 16 bytes
            Array.Copy(envelope, 0, iv, 0, 16);

            byte[] cipherData = new byte[envelope.Length - 16];
            Array.Copy(envelope, 16, cipherData, 0, cipherData.Length);

            // Decrypt data with AES
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                using (var msDecrypt = new MemoryStream(cipherData))
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (var srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Stores data securely using OS-specific protection (DPAPI on Windows, file-based on other platforms).
        /// </summary>
        /// <param name="keyName">The name/identifier for the data.</param>
        /// <param name="data">The data to store securely.</param>
        /// <returns>True if successfully stored, false otherwise.</returns>
        private static bool StoreSecureData(string keyName, byte[] data)
        {
            try
            {
#if WINDOWS
                // Use DPAPI on Windows
                var protectedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                var registryPath = $@"HKEY_CURRENT_USER\SOFTWARE\SmartHopper\SecureKeys";
                Microsoft.Win32.Registry.SetValue(registryPath, keyName, Convert.ToBase64String(protectedData));
                Debug.WriteLine($"[SecureStore] Stored data '{keyName}' using DPAPI in Windows Registry");
                return true;
#else
                // Use file-based storage on other platforms with basic protection
                var secureDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".smarthopper", "secure");
                Directory.CreateDirectory(secureDir);

                var filePath = Path.Combine(secureDir, $"{keyName}.dat");

                // Add basic obfuscation (not cryptographically secure, but better than plaintext)
                var obfuscated = new byte[data.Length];
                var seed = Environment.UserName.GetHashCode() ^ Environment.MachineName.GetHashCode();
                var rng = new Random(seed);

                for (int i = 0; i < data.Length; i++)
                {
                    obfuscated[i] = (byte)(data[i] ^ (rng.Next() & 0xFF));
                }

                File.WriteAllBytes(filePath, obfuscated);

                // Set restrictive file permissions (Unix-like systems)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // chmod 600 equivalent
                    File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                Debug.WriteLine($"[SecureStore] Stored data '{keyName}' using file-based storage with obfuscation");
                return true;
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecureStore] Error storing secure data '{keyName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves data securely using OS-specific protection (DPAPI on Windows, file-based on other platforms).
        /// </summary>
        /// <param name="keyName">The name/identifier for the data.</param>
        /// <returns>The retrieved data, or null if not found or error.</returns>
        private static byte[] GetSecureData(string keyName)
        {
            try
            {
#if WINDOWS
                // Use DPAPI on Windows
                var registryPath = $@"HKEY_CURRENT_USER\SOFTWARE\SmartHopper\SecureKeys";
                var protectedDataBase64 = Microsoft.Win32.Registry.GetValue(registryPath, keyName, null) as string;

                if (string.IsNullOrEmpty(protectedDataBase64))
                {
                    return null;
                }

                var protectedData = Convert.FromBase64String(protectedDataBase64);
                var data = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
                Debug.WriteLine($"[SecureStore] Retrieved data '{keyName}' using DPAPI from Windows Registry");
                return data;
#else
                // Use file-based storage on other platforms
                var secureDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".smarthopper", "secure");
                var filePath = Path.Combine(secureDir, $"{keyName}.dat");

                if (!File.Exists(filePath))
                {
                    return null;
                }

                var obfuscated = File.ReadAllBytes(filePath);

                // Reverse the obfuscation
                var data = new byte[obfuscated.Length];
                var seed = Environment.UserName.GetHashCode() ^ Environment.MachineName.GetHashCode();
                var rng = new Random(seed);

                for (int i = 0; i < obfuscated.Length; i++)
                {
                    data[i] = (byte)(obfuscated[i] ^ (rng.Next() & 0xFF));
                }

                Debug.WriteLine($"[SecureStore] Retrieved data '{keyName}' using file-based storage with deobfuscation");
                return data;
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecureStore] Error retrieving secure data '{keyName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Legacy AES encryption for backwards compatibility.
        /// </summary>
        private static string EncryptLegacy(string plainText)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = LegacyKey;
                    aes.IV = LegacyIv;

                    using (var encryptor = aes.CreateEncryptor())
                    using (var msEncrypt = new MemoryStream())
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (var swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                        swEncrypt.Flush();
                        csEncrypt.FlushFinalBlock();
                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Legacy Encryption] Error: {ex.Message}");
                return plainText;
            }
        }

        /// <summary>
        /// Legacy AES decryption for migration support.
        /// </summary>
        private static string DecryptLegacy(string encryptedText)
        {
            try
            {
                byte[] cipherText = Convert.FromBase64String(encryptedText);

                using (var aes = Aes.Create())
                {
                    aes.Key = LegacyKey;
                    aes.IV = LegacyIv;

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
                Debug.WriteLine($"[Legacy Decryption] Error: {ex.Message}");
                return encryptedText;
            }
        }

        /// <summary>
        /// Manually triggers encryption migration from legacy to certificate-based encryption.
        /// </summary>
        /// <returns>True if migration was performed or already completed, false if migration failed.</returns>
        public bool TriggerEncryptionMigration()
        {
            try
            {
                this.MigrateEncryption();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Migration] Manual migration trigger failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Migrates settings from legacy encryption to OS secure store encryption.
        /// </summary>
        private void MigrateEncryption()
        {
            if (this.EncryptionVersion >= 2)
            {
                return; // Already migrated
            }

            Debug.WriteLine("[Migration] Starting encryption migration from legacy to OS secure store");

            var key = GetOrCreateEncryptionKey();
            if (key == null)
            {
                Debug.WriteLine("[Migration] Cannot migrate - no encryption key available");
                return;
            }

            bool migrated = false;

            // Migrate all encrypted settings
            foreach (var providerKvp in this.ProviderSettings.ToList())
            {
                var providerName = providerKvp.Key;
                var settings = providerKvp.Value;
                var descriptors = GetProviderDescriptors(providerName);

                foreach (var settingKvp in settings.ToList())
                {
                    var settingName = settingKvp.Key;
                    var descriptor = descriptors.FirstOrDefault(d => d.Name == settingName);

                    if (descriptor?.IsSecret == true && settingKvp.Value != null)
                    {
                        var encryptedValue = settingKvp.Value.ToString();

                        // Skip if already OS secure store encrypted
                        if (encryptedValue.StartsWith("SH02:"))
                        {
                            continue;
                        }

                        try
                        {
                            // Decrypt with legacy method
                            var plainText = DecryptLegacy(encryptedValue);

                            // Re-encrypt with OS secure store method
                            var newEncryptedValue = EncryptWithSecureKey(plainText, key);

                            // Update the setting
                            settings[settingName] = newEncryptedValue;

                            Debug.WriteLine($"[Migration] Migrated {providerName}.{settingName}");
                            migrated = true;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Migration] Failed to migrate {providerName}.{settingName}: {ex.Message}");
                        }
                    }
                }
            }

            if (migrated)
            {
                this.EncryptionVersion = 2;
                Debug.WriteLine("[Migration] Encryption migration completed successfully");
                this.Save(); // Save the migrated settings
            }
            else
            {
                Debug.WriteLine("[Migration] No settings required migration");
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

                        // Trigger encryption migration if needed (safe to do here as it doesn't depend on ProviderManager)
                        // Note: This will be a no-op if already migrated or no certificate available
                        try
                        {
                            settings.MigrateEncryption();
                        }
                        catch (Exception migrationEx)
                        {
                            Debug.WriteLine($"[Load] Migration failed: {migrationEx.Message}");
                        }

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
