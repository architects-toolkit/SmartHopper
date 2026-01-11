/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 * 
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using SmartHopper.Infrastructure.AIProviders;

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
