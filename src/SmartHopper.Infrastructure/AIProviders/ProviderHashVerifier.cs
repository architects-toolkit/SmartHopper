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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Hash verification status codes.
    /// </summary>
    public enum ProviderVerificationStatus
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
    /// Verification result containing hash comparison status and details.
    /// </summary>
    public sealed class ProviderVerificationResult
    {
        public bool Success { get; set; }
        public ProviderVerificationStatus Status { get; set; }
        public string LocalHash { get; set; }
        public string PublicHash { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Verifies provider DLL integrity using SHA-256 hashes from public repository.
    /// </summary>
    internal class ProviderHashVerifier
    {
        private const string HashBaseUrl = "https://architects-toolkit.github.io/SmartHopper/hashes";
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        /// <summary>
        /// Calculates SHA-256 hash of a file.
        /// </summary>
        public static string CalculateFileHash(string filePath)
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
                        var response = await HttpClient.GetAsync(url).ConfigureAwait(false);

                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            Debug.WriteLine($"[ProviderHashVerifier] Hash manifest not found at {url} (404)");
                            continue;
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            Debug.WriteLine($"[ProviderHashVerifier] Failed to fetch from {url}: {(int)response.StatusCode} {response.ReasonPhrase}");
                            continue;
                        }

                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var manifest = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);

                        if (manifest != null && manifest.ContainsKey("providers"))
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
        /// <param name="dllPath">Path to the provider DLL</param>
        /// <param name="version">SmartHopper version (e.g., "v1.2.3")</param>
        /// <param name="platform">Target platform (e.g., "net7.0-windows")</param>
        /// <returns>Verification result with status and details</returns>
        public static async Task<ProviderVerificationResult> VerifyProviderAsync(string dllPath, string version, string platform)
        {
            var result = new ProviderVerificationResult();

            try
            {
                // Calculate local hash
                result.LocalHash = CalculateFileHash(dllPath);
                Debug.WriteLine($"[ProviderHashVerifier] Local hash for {Path.GetFileName(dllPath)}: {result.LocalHash}");

                // Fetch public hashes
                var publicHashes = await FetchPublicHashesAsync(version).ConfigureAwait(false);

                if (publicHashes == null)
                {
                    result.Status = ProviderVerificationStatus.Unavailable;
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
                        result.Status = ProviderVerificationStatus.NotFound;
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
                    result.Status = ProviderVerificationStatus.Match;
                    Debug.WriteLine($"[ProviderHashVerifier] Hash match for {dllName}");
                }
                else
                {
                    result.Status = ProviderVerificationStatus.Mismatch;
                    result.ErrorMessage = $"SHA-256 hash mismatch detected for {dllName}. Expected: {publicHash}, Actual: {result.LocalHash}";
                    Debug.WriteLine($"[ProviderHashVerifier] Hash mismatch for {dllName}");
                }
            }
            catch (Exception ex)
            {
                result.Status = ProviderVerificationStatus.Unavailable;
                result.ErrorMessage = $"Error during hash verification: {ex.Message}";
                Debug.WriteLine($"[ProviderHashVerifier] Verification error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Verifies all provider DLLs in a directory.
        /// </summary>
        /// <param name="directory">Directory containing provider DLLs</param>
        /// <param name="version">SmartHopper version</param>
        /// <param name="platform">Target platform</param>
        /// <returns>Dictionary of DLL names to verification results</returns>
        public static async Task<Dictionary<string, ProviderVerificationResult>> VerifyAllProvidersAsync(string directory, string version, string platform)
        {
            var results = new Dictionary<string, ProviderVerificationResult>();

            try
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    Debug.WriteLine($"[ProviderHashVerifier] Provider directory is invalid: '{directory ?? "<null>"}'");
                    return results;
                }

                string[] providerFiles = Directory.GetFiles(directory, "SmartHopper.Providers.*.dll");
                
                foreach (string providerFile in providerFiles)
                {
                    string dllName = Path.GetFileName(providerFile);
                    var result = await VerifyProviderAsync(providerFile, version, platform).ConfigureAwait(false);
                    results[dllName] = result;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProviderHashVerifier] Error verifying all providers: {ex.Message}");
            }

            return results;
        }
    }
}
