/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Newtonsoft.Json;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;
using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace SmartHopper.Config.Configuration
{
    public class SmartHopperSettings
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Grasshopper",
            "SmartHopper.json"
        );

        public Dictionary<string, Dictionary<string, object>> ProviderSettings { get; set; }
        public int DebounceTime { get; set; }
        
        /// <summary>
        /// The default AI provider to use when not explicitly specified in components.
        /// If not set or the provider doesn't exist, the first available provider will be used.
        /// </summary>
        public string DefaultAIProvider { get; set; }

        public SmartHopperSettings()
        {
            ProviderSettings = new Dictionary<string, Dictionary<string, object>>();
            DebounceTime = 1000;
            DefaultAIProvider = string.Empty;
        }

        // Use a constant key and IV for encryption (these could be moved to secure configuration)
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

        private Dictionary<string, Dictionary<string, object>> EncryptSensitiveSettings(Dictionary<string, Dictionary<string, object>> settings)
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

                foreach (var setting in provider.Value)
                {
                    var descriptor = descriptors.FirstOrDefault(d => d.Name == setting.Key);
                    if (descriptor?.IsSecret == true && setting.Value != null)
                    {
                        decryptedSettings[provider.Key][setting.Key] = UnprotectString(setting.Value.ToString());
                    }
                    else
                    {
                        decryptedSettings[provider.Key][setting.Key] = setting.Value;
                    }
                }
            }

            return decryptedSettings;
        }

        private IEnumerable<SettingDescriptor> GetProviderDescriptors(string providerName)
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
                return new SmartHopperSettings();

            try
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonConvert.DeserializeObject<SmartHopperSettings>(json) ?? new SmartHopperSettings();
                settings.ProviderSettings = settings.DecryptSensitiveSettings(settings.ProviderSettings);
                return settings;
            }
            catch (Exception)
            {
                return new SmartHopperSettings();
            }
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var settingsToSave = new SmartHopperSettings
                {
                    ProviderSettings = EncryptSensitiveSettings(ProviderSettings),
                    DebounceTime = DebounceTime,
                    DefaultAIProvider = DefaultAIProvider
                };

                var json = JsonConvert.SerializeObject(settingsToSave, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception)
            {
                // Handle or log error as needed
            }
        }

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
