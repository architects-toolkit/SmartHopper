/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Config.Interfaces;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// Factory class for creating instances of the OpenAI provider and its settings.
    /// This class is discovered by the ProviderManager through reflection.
    /// </summary>
    public class OpenAIProviderFactory : IAIProviderFactory
    {
        /// <summary>
        /// Creates an instance of the OpenAI provider.
        /// </summary>
        /// <returns>An instance of the OpenAI provider.</returns>
        public IAIProvider CreateProvider()
        {
            return OpenAI.Instance;
        }

        /// <summary>
        /// Creates an instance of the OpenAI provider settings.
        /// </summary>
        /// <param name="provider">The provider associated with these settings.</param>
        /// <returns>An instance of the OpenAI provider settings.</returns>
        public IAIProviderSettings CreateProviderSettings(IAIProvider provider)
        {
            return new OpenAISettings(provider);
        }
    }
}
