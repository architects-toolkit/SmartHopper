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
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AICall;

namespace SmartHopper.Providers.DeepSeek
{
    /// <summary>
    /// DeepSeek provider-specific model management implementation.
    /// </summary>
    public class DeepSeekProviderModels : AIProviderModels
    {
        private readonly DeepSeekProvider deepSeekProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeepSeekProviderModels"/> class.
        /// </summary>
        /// <param name="provider">The DeepSeek provider instance.</param>
        public DeepSeekProviderModels(DeepSeekProvider provider)
            : base(provider)
        {
            this.deepSeekProvider = provider;
        }

        /// <summary>
        /// Retrieves the list of available model names for DeepSeek.
        /// </summary>
        /// <returns>A list of available model names.</returns>
        public override async Task<List<string>> RetrieveAvailable()
        {
            Debug.WriteLine("[DeepSeek] Retrieving available models");
            try
            {
                // Use AIRequestCall to perform the request
                var request = new AIRequestCall();
                request.Initialize(this.deepSeekProvider.Name, string.Empty, string.Empty, "/models", AICapability.TextInput);
                request.HttpMethod = "GET";
                request.ContentType = "application/json";
                request.Authentication = "bearer";

                var aiReturn = await request.Exec().ConfigureAwait(false);
                if (!aiReturn.Success)
                {
                    throw new Exception($"API request failed: {aiReturn.ErrorMessage}");
                }

                var response = aiReturn.Body.GetLastInteraction() as AIInteractionText;
                var content = response?.Content ?? string.Empty;
                var json = JObject.Parse(content);
                var data = json["data"] as JArray;
                var modelIds = new List<string>();
                if (data != null)
                {
                    foreach (var item in data.OfType<JObject>())
                    {
                        var id = item["id"]?.ToString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            modelIds.Add(id);
                        }
                    }
                }

                return modelIds;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DeepSeek] Exception retrieving models: {ex.Message}");
                throw new Exception($"Error retrieving models from DeepSeek API: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets all models and their capabilities supported by DeepSeek.
        /// </summary>
        /// <returns>Dictionary of model names and their capabilities.</returns>
        public override async Task<Dictionary<string, AICapability>> RetrieveCapabilities()
        {
            var result = new Dictionary<string, AICapability>();

            // Add deepseek-reasoner model
            result["deepseek-reasoner"] = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput;

            // Add deepseek-chat model
            result["deepseek-chat"] = AICapability.TextInput | AICapability.TextOutput | AICapability.FunctionCalling | AICapability.JsonOutput;

            return result;
        }

        /// <summary>
        /// Gets all default models supported by DeepSeek.
        /// </summary>
        /// <returns>Dictionary of model names and their capabilities.</returns>
        public override Dictionary<string, AICapability> RetrieveDefault()
        {
            var result = new Dictionary<string, AICapability>();

            // Add deepseek-reasoner model
            result["deepseek-reasoner"] = AICapability.ReasoningChat;

            // Add deepseek-chat model as default for both BasicChat and AdvancedChat
            result["deepseek-chat"] = AICapability.BasicChat | AICapability.AdvancedChat;

            return result;
        }
    }
}
