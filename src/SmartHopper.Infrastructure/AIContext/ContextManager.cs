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
using System.Linq;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Infrastructure.AIContext
{
    /// <summary>
    /// Static manager for handling AI context providers
    /// </summary>
    public static class AIContextManager
    {
        private static readonly List<IAIContextProvider> _contextProviders = new List<IAIContextProvider>();

        /// <summary>
        /// Registers a context provider for AI queries
        /// </summary>
        /// <param name="provider">The context provider implementation</param>
        public static void RegisterProvider(IAIContextProvider provider)
        {
            if (provider == null) return;

            // Remove any existing provider with the same ID
            _contextProviders.RemoveAll(p => p.ProviderId == provider.ProviderId);

            // Add the new provider
            _contextProviders.Add(provider);

            Debug.WriteLine($"[RegisterContextProvider] Registered provider: {provider.ProviderId}");
        }

        /// <summary>
        /// Unregisters a context provider
        /// </summary>
        /// <param name="providerId">The ID of the provider to unregister</param>
        public static void UnregisterProvider(string providerId)
        {
            if (string.IsNullOrEmpty(providerId)) return;

            _contextProviders.RemoveAll(p => p.ProviderId == providerId);

            Debug.WriteLine($"[UnregisterProvider] Unregistered provider: {providerId}");
        }

        /// <summary>
        /// Unregisters a specific context provider instance
        /// </summary>
        /// <param name="provider">The provider instance to unregister</param>
        public static void UnregisterProvider(IAIContextProvider provider)
        {
            if (provider == null) return;

            _contextProviders.Remove(provider);
        }

        /// <summary>
        /// Gets all registered context providers
        /// </summary>
        /// <returns>List of registered context providers</returns>
        public static List<IAIContextProvider> GetProviders()
        {
            return _contextProviders.ToList();
        }

        /// <summary>
        /// Gets a specific context provider by ID
        /// </summary>
        /// <param name="providerId">The ID of the provider to get</param>
        /// <returns>The context provider with the specified ID, or null if not found</returns>
        public static IAIContextProvider GetProvider(string providerId)
        {
            return _contextProviders.FirstOrDefault(p => p.ProviderId == providerId);
        }

        /// <summary>
        /// Gets the combined context from all registered providers, with optional filtering
        /// </summary>
        /// <param name="providerFilter">Optional provider ID filter. If specified, only context from providers with matching IDs will be included. Multiple providers can be specified as a comma-separated or space-separated list.
        /// Prefix a provider ID with '-' to exclude it (e.g., "-time" excludes the time provider).
        /// Use '*' to include all providers (default behavior).
        /// Use '-*' to exclude all providers.</param>
        /// <returns>A dictionary of context key-value pairs</returns>
        public static Dictionary<string, string> GetCurrentContext(string providerFilter = null)
        {
            var result = new Dictionary<string, string>();

            var provFilter = Filtering.Parse(providerFilter);

            var providers = _contextProviders
                .Where(p => provFilter.ShouldInclude(p.ProviderId))
                .ToList();

            foreach (var provider in providers)
            {
                foreach (var kv in provider.GetContext())
                {
                    string key = kv.Key;
                    if (!key.Contains("_", StringComparison.Ordinal))
                    {
                        key = $"{provider.ProviderId}_{key}";
                    }

                    Debug.WriteLine($"[GetCurrentContext] Adding context key: {key}");
                    result[key] = kv.Value;
                }
            }

            Debug.WriteLine($"[GetCurrentContext] {result.Count} context keys added");

            return result;
        }
    }
}
