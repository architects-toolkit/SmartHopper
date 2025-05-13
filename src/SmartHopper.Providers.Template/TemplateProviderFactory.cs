/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Providers.Template
{
    /// <summary>
    /// Factory class for creating instances of the Template provider and its settings.
    /// This class is discovered by the ProviderManager through reflection.
    /// 
    /// IMPORTANT: Each provider must have a factory class that implements IAIProviderFactory.
    /// The factory is responsible for creating instances of the provider and its settings.
    /// </summary>
    public class TemplateProviderFactory : IAIProviderFactory
    {
        /// <summary>
        /// Creates an instance of the Template provider.
        /// </summary>
        /// <returns>An instance of the Template provider.</returns>
        public IAIProvider CreateProvider()
        {
            return TemplateProvider.Instance;
        }

        /// <summary>
        /// Creates an instance of the Template provider settings.
        /// </summary>
        /// <param name="provider">The provider associated with these settings.</param>
        /// <returns>An instance of the Template provider settings.</returns>
        public IAIProviderSettings CreateProviderSettings(IAIProvider provider)
        {
            return new TemplateProviderSettings(provider);
        }
    }
}
