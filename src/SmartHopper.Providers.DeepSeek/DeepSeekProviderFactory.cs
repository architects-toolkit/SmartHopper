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

namespace SmartHopper.Providers.DeepSeek
{
    /// <summary>
    /// Factory class for creating instances of the Template provider and its settings.
    /// This class is discovered by the ProviderManager through reflection.
    ///
    /// IMPORTANT: Each provider must have a factory class that implements IAIProviderFactory.
    /// The factory is responsible for creating instances of the provider and its settings.
    /// </summary>
    public class DeepSeekProviderFactory : IAIProviderFactory
    {
        /// <summary>
        /// Creates an instance of the Template provider.
        /// </summary>
        /// <returns>An instance of the Template provider.</returns>
        public IAIProvider CreateProvider()
        {
            return DeepSeekProvider.Instance;
        }

        /// <summary>
        /// Creates an instance of the DeepSeek provider settings.
        /// </summary>
        /// <returns>An instance of the DeepSeek provider settings.</returns>
        public IAIProviderSettings CreateProviderSettings()
        {
            return new DeepSeekProviderSettings(DeepSeekProvider.Instance);
        }
    }
}
