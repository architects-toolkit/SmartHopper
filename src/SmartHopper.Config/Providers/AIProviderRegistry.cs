using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SmartHopper.Config.Configuration;
using SmartHopper.Config.Interfaces;

namespace SmartHopper.Config.Providers
{
    /// <summary>
    /// Registry for AI provider icons and information. Uses SmartHopperSettings.DiscoverProviders
    /// for provider discovery.
    /// </summary>
    public static class AIProviderRegistry
    {
        /// <summary>
        /// Gets the icon for the specified AI provider
        /// </summary>
        /// <param name="providerName">Name of the provider</param>
        /// <returns>The provider's icon or null if not found</returns>
        public static Image GetProviderIcon(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
                return null;

            var provider = SmartHopperSettings.DiscoverProviders()
                .FirstOrDefault(p => p.Name == providerName);

            return provider?.Icon;
        }

        /// <summary>
        /// Gets a registered provider by name
        /// </summary>
        /// <param name="providerName">Name of the provider</param>
        /// <returns>The provider or null if not found</returns>
        public static IAIProvider GetProvider(string providerName)
        {
            if (string.IsNullOrEmpty(providerName))
                return null;

            return SmartHopperSettings.DiscoverProviders()
                .FirstOrDefault(p => p.Name == providerName);
        }

        /// <summary>
        /// Gets all registered providers
        /// </summary>
        public static IEnumerable<IAIProvider> GetAllProviders()
        {
            return SmartHopperSettings.DiscoverProviders();
        }
    }
}
