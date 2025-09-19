/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Providers.OpenRouter
{
    /// <summary>
    /// Factory class for creating instances of the OpenRouter provider and its settings.
    /// This class is discovered by the ProviderManager through reflection.
    /// </summary>
    public class OpenRouterProviderFactory : IAIProviderFactory
    {
        /// <summary>
        /// Creates an instance of the OpenRouter provider.
        /// </summary>
        /// <returns>An instance of the OpenRouter provider.</returns>
        public IAIProvider CreateProvider()
        {
            return OpenRouterProvider.Instance;
        }

        /// <summary>
        /// Creates an instance of the OpenRouter provider settings.
        /// </summary>
        /// <returns>An instance of the OpenRouter provider settings.</returns>
        public IAIProviderSettings CreateProviderSettings()
        {
            return new OpenRouterProviderSettings(OpenRouterProvider.Instance);
        }
    }
}
