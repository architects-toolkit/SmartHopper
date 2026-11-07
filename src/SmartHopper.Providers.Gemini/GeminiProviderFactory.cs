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
using System.Diagnostics;
using SmartHopper.ProviderSdk.AIProviders;

using SmartHopper.ProviderSdk.Logging;
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
            try
            {
                Logger.For<GeminiProviderFactory>().Trace("[GeminiProviderFactory] Attempting to get AIProvider<GeminiProvider>.Instance...");
                var instance = AIProvider<GeminiProvider>.Instance;
                if (instance == null)
                {
                    Logger.For<GeminiProviderFactory>().Warning("[GeminiProviderFactory] ERROR: AIProvider<GeminiProvider>.Instance returned null!");
                }
                else
                {
                    Logger.For<GeminiProviderFactory>().Trace($"[GeminiProviderFactory] Successfully got instance: {instance.GetType().FullName}");
                }

                return instance;
            }
            catch (Exception ex)
            {
                Logger.For<GeminiProviderFactory>().Exception(ex, $"[GeminiProviderFactory] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                Logger.For<GeminiProviderFactory>().Exception(ex, $"[GeminiProviderFactory] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Creates a new instance of the Gemini provider settings.
        /// </summary>
        /// <returns>A new GeminiProviderSettings instance.</returns>
        public IAIProviderSettings CreateProviderSettings()
        {
            return new GeminiProviderSettings(AIProvider<GeminiProvider>.Instance);
        }
    }
}
