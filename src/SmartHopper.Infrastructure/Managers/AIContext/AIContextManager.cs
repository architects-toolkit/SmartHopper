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
using System.Linq;
using SmartHopper.Infrastructure.Interfaces;

namespace SmartHopper.Infrastructure.Managers.AIContext
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
        }

        /// <summary>
        /// Unregisters a context provider
        /// </summary>
        /// <param name="providerId">The ID of the provider to unregister</param>
        public static void UnregisterProvider(string providerId)
        {
            if (string.IsNullOrEmpty(providerId)) return;

            _contextProviders.RemoveAll(p => p.ProviderId == providerId);
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
        /// <param name="contextFilter">Optional context key filter. If specified, only context with matching keys will be included. Multiple context keys can be specified as a comma-separated or space-separated list.
        /// Prefix a context key with '-' to exclude it (e.g., "-time_current-datetime" excludes that specific context key).
        /// Use '*' to include all context keys (default behavior).
        /// Use '-*' to exclude all context keys.</param>
        /// <returns>A dictionary of context key-value pairs</returns>
        public static Dictionary<string, string> GetCurrentContext(string providerFilter = null, string contextFilter = null)
        {
            var result = new Dictionary<string, string>();

            // if "-*" is specified in either filter, exclude all and return an empty dictionary
            if ((providerFilter != null && providerFilter.Contains("-*")) ||
                (contextFilter != null && contextFilter.Contains("-*")))
            {
                return result;
            }

            // Parse provider filters
            HashSet<string> includeProviderFilters = null;
            HashSet<string> excludeProviderFilters = null;

            if (!string.IsNullOrEmpty(providerFilter))
            {
                var filterParts = providerFilter.Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .ToList();

                // Separate include and exclude filters
                var includeFilters = filterParts.Where(p => !p.StartsWith("-")).ToList();
                var excludeFilters = filterParts.Where(p => p.StartsWith("-"))
                    .Select(p => p.Substring(1)) // Remove the '-' prefix
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();

                // Handle '*' wildcard: treat '*' as include-all (default behavior)
                if (!includeFilters.Contains("*"))
                {
                    if (includeFilters.Count > 0)
                    {
                        includeProviderFilters = new HashSet<string>(includeFilters);
                    }
                }

                if (excludeFilters.Count > 0)
                {
                    excludeProviderFilters = new HashSet<string>(excludeFilters);
                }
            }

            // Parse context filters
            HashSet<string> includeContextFilters = null;
            HashSet<string> excludeContextFilters = null;

            if (!string.IsNullOrEmpty(contextFilter))
            {
                var filterParts = contextFilter.Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .ToList();

                // Separate include and exclude filters
                var includeFilters = filterParts.Where(c => !c.StartsWith("-")).ToList();
                var excludeFilters = filterParts.Where(c => c.StartsWith("-"))
                    .Select(c => c.Substring(1)) // Remove the '-' prefix
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();

                // Handle '*' wildcard: treat '*' as include-all (default behavior)
                if (!includeFilters.Contains("*"))
                {
                    if (includeFilters.Count > 0)
                    {
                        includeContextFilters = new HashSet<string>(includeFilters);
                    }
                }

                if (excludeFilters.Count > 0)
                {
                    excludeContextFilters = new HashSet<string>(excludeFilters);
                }
            }

            // Get providers based on filter
            var providers = _contextProviders
                .Where(p => ShouldIncludeProvider(p.ProviderId, includeProviderFilters, excludeProviderFilters))
                .ToList();

            // Collect context from all matching providers
            foreach (var provider in providers)
            {
                try
                {
                    var context = provider.GetContext();
                    if (context != null)
                    {
                        // Format context keys with provider prefix if not already formatted
                        foreach (var item in context)
                        {
                            string key = item.Key;
                            if (!key.Contains("_"))
                            {
                                key = $"{provider.ProviderId}_{key}";
                            }

                            // Apply context key filter if specified
                            if (ShouldIncludeContext(key, includeContextFilters, excludeContextFilters))
                            {
                                result[key] = item.Value;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    // Log the error but continue with other providers
                    System.Diagnostics.Debug.WriteLine($"Error getting context from provider {provider.ProviderId}: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Determines if a provider should be included based on the filter criteria
        /// </summary>
        /// <param name="providerId">The provider ID to check</param>
        /// <param name="includeFilters">The set of include filters</param>
        /// <param name="excludeFilters">The set of exclude filters</param>
        /// <returns>True if the provider should be included, false otherwise</returns>
        private static bool ShouldIncludeProvider(string providerId, HashSet<string> includeFilters, HashSet<string> excludeFilters)
        {
            // If provider ID is in exclude filters, exclude it
            if (excludeFilters != null && excludeFilters.Contains(providerId))
            {
                return false;
            }

            // If include filters are specified and provider ID is not in them, exclude it
            if (includeFilters != null && includeFilters.Count > 0 && !includeFilters.Contains(providerId))
            {
                return false;
            }

            // In all other cases, include the provider
            return true;
        }

        /// <summary>
        /// Determines if a context key should be included based on the filter criteria
        /// </summary>
        /// <param name="key">The context key to check</param>
        /// <param name="includeFilters">The set of include filters</param>
        /// <param name="excludeFilters">The set of exclude filters</param>
        /// <returns>True if the key should be included, false otherwise</returns>
        private static bool ShouldIncludeContext(string key, HashSet<string> includeFilters, HashSet<string> excludeFilters)
        {
            // If key is in exclude filters, exclude it
            if (excludeFilters != null)
            {
                // Check exact match
                if (excludeFilters.Contains(key))
                {
                    return false;
                }

                // For filters without underscores, check if they match the suffix after the underscore
                foreach (var filter in excludeFilters)
                {
                    if (!filter.Contains("_") && key.EndsWith($"_{filter}"))
                    {
                        return false;
                    }
                }
            }

            // If no include filters, include all (that weren't excluded)
            if (includeFilters == null || includeFilters.Count == 0)
            {
                return true;
            }

            // Check if the key exactly matches any include filter
            if (includeFilters.Contains(key))
            {
                return true;
            }

            // For filters without underscores, check if they match the suffix after the underscore
            foreach (var filter in includeFilters)
            {
                if (!filter.Contains("_") && key.EndsWith($"_{filter}"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
