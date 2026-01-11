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

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Interface for AI provider factories.
    /// This interface is used by the provider discovery mechanism to create instances of providers and their settings.
    /// </summary>
    public interface IAIProviderFactory
    {
        /// <summary>
        /// Creates an instance of the AI provider.
        /// </summary>
        /// <returns>An instance of the AI provider.</returns>
        IAIProvider CreateProvider();

        /// <summary>
        /// Creates an instance of the AI provider settings.
        /// </summary>
        /// <returns>An instance of the AI provider settings.</returns>
        IAIProviderSettings CreateProviderSettings();
    }
}
