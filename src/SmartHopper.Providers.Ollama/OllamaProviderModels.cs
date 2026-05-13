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

namespace SmartHopper.Providers.Ollama
{
    /// <summary>
    /// Ollama provider-specific model management implementation.
    /// </summary>
    /// <remarks>
    /// Ollama is run locally and the available models depend entirely on what the
    /// user has pulled with <c>ollama pull</c>. We therefore do not ship a static catalog
    /// of curated models. The user supplies the model name (e.g. <c>llama3.1</c>,
    /// <c>qwen2.5:14b</c>) through the provider's Model setting; we return an empty list
    /// and rely on the user-supplied name.
    /// </remarks>
    public class OllamaProviderModels : AIProviderModels
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OllamaProviderModels"/> class.
        /// </summary>
        /// <param name="provider">The Ollama provider instance.</param>
        public OllamaProviderModels(OllamaProvider provider)
            : base(provider)
        {
        }

        /// <inheritdoc/>
        public override Task<List<AIModelCapabilities>> RetrieveModels()
        {
            // Models are user-managed in Ollama (pulled with `ollama pull`); no static catalog.
            return Task.FromResult(new List<AIModelCapabilities>());
        }
    }
}
