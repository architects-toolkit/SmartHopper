/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Infrastructure.Interfaces;

namespace SmartHopper.Providers.MistralAI
{
    /// <summary>
    /// Factory class for creating MistralAIProvider provider instances.
    /// This class is used by the provider discovery mechanism to create instances of the provider.
    /// </summary>
    public class MistralAIProviderFactory : IAIProviderFactory
    {
        /// <summary>
        /// Creates an instance of the MistralAI provider.
        /// </summary>
        /// <returns>An instance of the MistralAIProvider provider.</returns>
        public IAIProvider CreateProvider()
        {
            return MistralAIProvider.Instance;
        }

        /// <summary>
        /// Creates an instance of the MistralAI provider settings.
        /// </summary>
        /// <returns>An instance of the MistralAI provider settings.</returns>
        public IAIProviderSettings CreateProviderSettings()
        {
            return new MistralAIProviderSettings(MistralAIProvider.Instance);
        }
    }
}
