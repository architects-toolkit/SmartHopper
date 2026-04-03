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
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Providers.Gemini
{
    /// <summary>
    /// Factory for creating Google Gemini provider instances.
    /// </summary>
    public class GeminiProviderFactory : IAIProviderFactory
    {
        /// <summary>
        /// Creates a new instance of the Gemini provider.
        /// </summary>
        /// <returns>The Gemini provider singleton instance.</returns>
        public IAIProvider CreateProvider()
        {
            return AIProvider<GeminiProvider>.Instance;
        }

        /// <summary>
        /// Creates a new instance of the Gemini provider settings.
        /// </summary>
        /// <returns>A new GeminiProviderSettings instance.</returns>
        public IAIProviderSettings CreateProviderSettings()
        {
            return new GeminiProviderSettings();
        }
    }
}
