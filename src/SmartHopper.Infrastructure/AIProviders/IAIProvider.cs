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
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Infrastructure.AIProviders
{
    public interface IAIProvider
    {
        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        string Name { get; }

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
        /// Gets the models manager for this provider.
        /// Provides access to model-related operations including capability management.
        /// </summary>
        IAIProviderModels Models { get; }

        /// <summary>
        /// Gets the default model name for the provider.
        /// </summary>
        string GetDefaultModel(AICapability requiredCapability = AICapability.BasicChat, bool useSettings = true);

        /// <summary>
        /// Gets the pre-call request for the provider.
        /// </summary>
        AIRequest PreCall<T>(AIRequest request);

        /// <summary>
        /// Gets the task processing the Call<T> with the provider.
        /// </summary>
        /// <param name="request">The request to send to the AI provider.</param>
        /// <returns>The response from the AI provider.</returns>
        Task<AIReturn<T>> Call<T>(AIRequest request);

        /// <summary>
        /// Gets the post-call response for the provider.
        /// </summary>
        /// <param name="response">The response from the AI provider.</param>
        /// <returns>The response from the AI provider.</returns>
        AIReturn<T> PostCall<T>(AIReturn<T> response);

        /// <summary>
        /// Refreshes the provider's cached settings by merging the input settings with existing cached settings.
        /// </summary>
        /// <param name="settings">The new settings to merge with existing cached settings.</param>
        void RefreshCachedSettings(Dictionary<string, object> settings);

        /// <summary>
        /// Returns the SettingDescriptors for this provider by
        /// fetching its IAIProviderSettings instance from ProviderManager.
        /// </summary>
        /// <returns>An enumerable of SettingDescriptor instances for the provider.</returns>
        IEnumerable<SettingDescriptor> GetSettingDescriptors();

        /// <summary>
        /// Initializes the provider.
        /// </summary>
        Task InitializeProviderAsync();
    }
}
