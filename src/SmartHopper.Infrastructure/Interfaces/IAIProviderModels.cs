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
using SmartHopper.Infrastructure.Managers.ModelManager;

namespace SmartHopper.Infrastructure.Interfaces
{
    /// <summary>
    /// Interface for AI provider model management operations.
    /// </summary>
    public interface IAIProviderModels
    {
        /// <summary>
        /// Gets the model to use for AI processing.
        /// </summary>
        /// <param name="requestedModel">The requested model, or empty for default.</param>
        /// <returns>The model to use.</returns>
        string GetModel(string requestedModel = "");

        /// <summary>
        /// Retrieves the list of available model names for this provider.
        /// </summary>
        /// <returns>A list of available model names.</returns>
        Task<List<string>> RetrieveAvailable();

        /// <summary>
        /// Gets all models and their capabilities supported by this provider.
        /// </summary>
        /// <returns>Dictionary of model names and their capabilities.</returns>
        Task<Dictionary<string, AIModelCapability>> RetrieveCapabilities();

        /// <summary>
        /// Gets the capability information for a specific model.
        /// </summary>
        /// <param name="model">The model name.</param>
        /// <returns>Model capabilities or null if not found.</returns>
        AIModelCapability RetrieveCapabilities(string model);
    }
}
