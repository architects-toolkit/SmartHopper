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

using System.Collections.Generic;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Providers.LocalAI
{
    /// <summary>
    /// LocalAI provider-specific model management implementation.
    /// </summary>
    /// <remarks>
    /// LocalAI is self-hosted and the available models depend entirely on what the
    /// user has installed on their instance. We therefore do not ship a static catalog
    /// of curated models. The user supplies the model name through the provider's
    /// Model setting; we return an empty list and rely on the user-supplied name.
    /// </remarks>
    public class LocalAIProviderModels : AIProviderModels
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalAIProviderModels"/> class.
        /// </summary>
        /// <param name="provider">The LocalAI provider instance.</param>
        public LocalAIProviderModels(LocalAIProvider provider)
            : base(provider)
        {
        }

        /// <inheritdoc/>
        public override Task<List<AIModelCapabilities>> RetrieveModels()
        {
            // Models are user-managed in LocalAI; no static catalog.
            return Task.FromResult(new List<AIModelCapabilities>());
        }
    }
}
