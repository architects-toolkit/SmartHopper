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
using System.Drawing;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Infrastructure.Interfaces
{
    public interface IAIProvider
    {
        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the default model name for the provider.
        /// </summary>
        string DefaultModel { get; }

        /// <summary>
        /// Gets the provider's icon. Should return a 16x16 image suitable for display in the UI.
        /// </summary>
        Image Icon { get; }

        /// <summary>
        /// Gets a value indicating whether this provider is enabled and should be available for use.
        /// This can be used to disable template or experimental providers.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Gets a response from the AI provider.
        /// </summary>
        /// <param name="messages">The messages to send to the AI provider.</param>
        /// <param name="model">The model to use for AI processing.</param>
        /// <param name="jsonSchema">The JSON schema to use for AI processing.</param>
        /// <param name="endpoint">The endpoint to use for AI processing.</param>
        /// <param name="toolFilter">The tool filter to use for AI processing.</param>
        /// <returns>The response from the AI provider.</returns>
        Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", string? toolFilter = null);

        /// <summary>
        /// Gets the model to use for AI processing.
        /// </summary>
        /// <param name="requestedModel">The requested model, or empty for default.</param>
        /// <returns>The model to use.</returns>
        string GetModel(string requestedModel = "");

        /// <summary>
        /// Injects decrypted settings for this provider (called by ProviderManager).
        /// </summary>
        /// <param name="settings">The provider settings.</param>
        void InitializeSettings(Dictionary<string, object> settings);

        /// <summary>
        /// Returns the SettingDescriptors for this provider by
        /// fetching its IAIProviderSettings instance from ProviderManager.
        /// </summary>
        /// <returns>An enumerable of SettingDescriptor instances for the provider.</returns>
        IEnumerable<SettingDescriptor> GetSettingDescriptors();
    }
}
