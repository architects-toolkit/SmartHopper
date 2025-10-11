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
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Defines the contract for AI provider integrations (e.g., MistralAI, OpenAI, Anthropic, DeepSeek).
    /// Implementations handle request encoding/decoding, calling, and model/settings management.
    /// </summary>
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
        /// Initializes the provider.
        /// </summary>
        /// <returns>A task representing the asynchronous initialization operation.</returns>
        Task InitializeProviderAsync();

        /// <summary>
        /// Gets the encoded request for this provider, given an <see cref="AIRequestCall"/>.
        /// </summary>
        /// <param name="request">The request to encode for transport.</param>
        /// <returns>A provider-specific encoded representation of the request.</returns>
        string Encode(AIRequestCall request);

        /// <summary>
        /// Gets the encoded interaction for this provider, given an <see cref="IAIInteraction"/>.
        /// </summary>
        /// <param name="interaction">The interaction to encode.</param>
        /// <returns>A provider-specific encoded representation of the interaction.</returns>
        string Encode(IAIInteraction interaction);

        /// <summary>
        /// Gets the encoded list of interactions for this provider, given an <see cref="List{IAIInteraction}"/>.
        /// </summary>
        /// <param name="interactions">The interactions to encode.</param>
        /// <returns>A provider-specific encoded representation of the interactions.</returns>
        string Encode(List<IAIInteraction> interactions);

        /// <summary>
        /// Gets the decoded list of interactions given the encoded response. Interactions include the response, tool calls and metrics.
        /// </summary>
        /// <param name="response">The provider response payload to decode.</param>
        /// <returns>A list of decoded interactions in SmartHopper's internal format.</returns>
        List<IAIInteraction> Decode(JObject response);

        /// <summary>
        /// Gets the pre-call request for the provider.
        /// </summary>
        /// <param name="request">The request to prepare before the provider call.</param>
        /// <returns>The possibly modified request to use for the provider call.</returns>
        AIRequestCall PreCall(AIRequestCall request);

        /// <summary>
        /// Gets the task processing the Call with the provider.
        /// </summary>
        /// <param name="request">The request to send to the AI provider.</param>
        /// <returns>The response from the AI provider.</returns>
        Task<IAIReturn> Call(AIRequestCall request);

        /// <summary>
        /// Gets the post-call response for the provider.
        /// </summary>
        /// <param name="response">The response from the AI provider.</param>
        /// <returns>The response from the AI provider.</returns>
        IAIReturn PostCall(IAIReturn response);

        /// <summary>
        /// Gets the default model name for the provider.
        /// </summary>
        /// <param name="requiredCapability">Optional capability to constrain the default model.</param>
        /// <param name="useSettings">True to honor user settings overrides; otherwise use provider defaults.</param>
        /// <returns>The default model name for this provider.</returns>
        string GetDefaultModel(AICapability requiredCapability = AICapability.Text2Text, bool useSettings = true);

        /// <summary>
        /// Selects the most appropriate model given a requested model (optional) and a required capability.
        /// Implementations should respect provider settings and capability compatibility.
        /// </summary>
        /// <param name="requiredCapability">The capability required by the request/tool.</param>
        /// <param name="requestedModel">An optional user/request-specified model name.</param>
        /// <returns>The selected model name (concrete, API-ready), or empty string when none.</returns>
        string SelectModel(AICapability requiredCapability, string requestedModel);

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
    }
}
