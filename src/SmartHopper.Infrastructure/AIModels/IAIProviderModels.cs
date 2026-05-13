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

namespace SmartHopper.Infrastructure.AIModels
{
    /// <summary>
    /// Interface for AI provider model metadata retrieval.
    /// Providers must return concrete models (no wildcards) with full metadata.
    /// </summary>
    public interface IAIProviderModels
    {
        /// <summary>
        /// Retrieves locally defined models with full metadata for this provider.
        /// </summary>
        /// <returns>List of model capability records.</returns>
        Task<List<AIModelCapabilities>> RetrieveModels();

        /// <summary>
        /// Retrieves api list of models
        /// </summary>
        /// <returns>List of available model names.</returns>
        Task<List<string>> RetrieveApiModels();
    }
}
