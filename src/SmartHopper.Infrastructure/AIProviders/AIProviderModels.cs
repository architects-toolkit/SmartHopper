/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Base class for AI provider model metadata retrieval.
    /// </summary>
    public abstract class AIProviderModels : IAIProviderModels
    {
        protected readonly IAIProvider _provider;

        /// <summary>
        /// Initializes a new instance of the AIProviderModels.
        /// </summary>
        /// <param name="provider">The AI provider this model manager belongs to.</param>
        protected AIProviderModels(IAIProvider provider)
        {
            this._provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Retrieves all models with full metadata (concrete names only) for this provider.
        /// </summary>
        /// <returns>List of AIModelCapabilities.</returns>
        public abstract Task<List<AIModelCapabilities>> RetrieveModels();
    }
}
