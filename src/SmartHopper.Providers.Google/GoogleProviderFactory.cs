using System;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Providers.Google
{
    /// <summary>
    /// Factory for creating Google Gemini provider instances.
    /// </summary>
    public class GoogleProviderFactory : IAIProviderFactory
    {
        /// <summary>
        /// Creates a new instance of the Google Gemini provider.
        /// </summary>
        /// <returns>The Google provider singleton instance.</returns>
        public IAIProvider CreateProvider()
        {
            return GoogleProvider.Instance;
        }

        /// <summary>
        /// Creates a new instance of the Google provider settings.
        /// </summary>
        /// <returns>A new GoogleProviderSettings instance.</returns>
        public IAIProviderSettings CreateSettings()
        {
            return new GoogleProviderSettings();
        }
    }
}
