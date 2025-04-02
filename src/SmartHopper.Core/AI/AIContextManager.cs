using System.Collections.Generic;
using System.Linq;
using System;

namespace SmartHopper.Core.AI
{
    /// <summary>
    /// Interface for providing context to AI queries
    /// </summary>
    public interface IAIContextProvider
    {
        /// <summary>
        /// Gets the provider identifier
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// Gets the current context for AI queries
        /// </summary>
        /// <returns>A dictionary of context key-value pairs</returns>
        Dictionary<string, string> GetContext();
    }
    
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
        /// <param name="providerFilter">Optional provider ID filter. If specified, only context from providers with matching IDs will be included. Multiple providers can be specified as a comma-separated list.</param>
        /// <param name="contextFilter">Optional context key filter. If specified, only context with matching keys will be included. Multiple context keys can be specified as a comma-separated list.</param>
        /// <returns>A dictionary of context key-value pairs</returns>
        public static Dictionary<string, string> GetCurrentContext(string providerFilter = null, string contextFilter = null)
        {
            var result = new Dictionary<string, string>();
            
            // Parse provider filters
            HashSet<string> providerFilters = null;
            if (!string.IsNullOrEmpty(providerFilter))
            {
                providerFilters = new HashSet<string>(
                    providerFilter.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                );
            }
            
            // Parse context filters
            HashSet<string> contextFilters = null;
            if (!string.IsNullOrEmpty(contextFilter))
            {
                contextFilters = new HashSet<string>(
                    contextFilter.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                );
            }
            
            // Get providers based on filter
            var providers = providerFilters == null 
                ? _contextProviders 
                : _contextProviders.Where(p => providerFilters.Contains(p.ProviderId)).ToList();
            
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
                            if (ShouldIncludeContext(key, contextFilters))
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
        /// Determines if a context key should be included based on the filter criteria
        /// </summary>
        /// <param name="key">The context key to check</param>
        /// <param name="contextFilters">The set of context filters</param>
        /// <returns>True if the key should be included, false otherwise</returns>
        private static bool ShouldIncludeContext(string key, HashSet<string> contextFilters)
        {
            // If no filters, include all
            if (contextFilters == null || contextFilters.Count == 0)
            {
                return true;
            }
            
            // Check if the key exactly matches any filter
            if (contextFilters.Contains(key))
            {
                return true;
            }
            
            // For filters without underscores, check if they match the suffix after the underscore
            foreach (var filter in contextFilters)
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
