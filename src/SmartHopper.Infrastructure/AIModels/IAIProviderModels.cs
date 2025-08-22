/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
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
        /// Retrieves all models with full metadata for this provider.
        /// </summary>
        /// <returns>List of model capability records (concrete names only).</returns>
        Task<List<AIModelCapabilities>> RetrieveModels();
    }
}
