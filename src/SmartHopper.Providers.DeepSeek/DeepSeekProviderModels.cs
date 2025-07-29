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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Managers.AIProviders;
using SmartHopper.Infrastructure.Managers.ModelManager;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Providers.DeepSeek
{
    /// <summary>
    /// DeepSeek provider-specific model management implementation.
    /// </summary>
    public class DeepSeekProviderModels : AIProviderModels
    {
        private readonly DeepSeekProvider _DeepSeekProvider;

        public DeepSeekProviderModels(DeepSeekProvider provider, Func<string, string, string, string, string, Task<string>> apiCaller) : base(provider, apiCaller)
        {
            this._DeepSeekProvider = provider;
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
                var content = await this._apiCaller("/models", "GET", string.Empty, "application/json", "bearer").ConfigureAwait(false);
                var json = JObject.Parse(content);
                var data = json["data"] as JArray;
                var modelIds = new List<string>();
                if (data != null)
                {
                    foreach (var item in data.OfType<JObject>())
                    {
                        var id = item["id"]?.ToString();
                        if (!string.IsNullOrEmpty(id)) modelIds.Add(id);
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
        /// Gets all models and their capabilities supported by DeepSeek
        /// </summary>
        /// <returns>Dictionary of model names and their capabilities.</returns>
        public override async Task<Dictionary<string, AIModelCapability>> RetrieveCapabilities()
        {
            var result = new Dictionary<string, AIModelCapability>();

            // Add deepseek-reasoner model
            result["deepseek-reasoner"] = AIModelCapability.TextInput | AIModelCapability.TextOutput | AIModelCapability.FunctionCalling | AIModelCapability.StructuredOutput;

            // Add deepseek-chat model
            result["deepseek-chat"] = AIModelCapability.TextInput | AIModelCapability.TextOutput | AIModelCapability.FunctionCalling | AIModelCapability.StructuredOutput;

            return result;
        }

        /// <summary>
        /// Gets all default models supported by DeepSeek
        /// </summary>
        /// <returns>Dictionary of model names and their capabilities.</returns>
        public override Dictionary<string, AIModelCapability> RetrieveDefault()
        {
            var result = new Dictionary<string, AIModelCapability>();

            // Add deepseek-reasoner model
            result["deepseek-reasoner"] = AIModelCapability.ReasoningChat;

            // Add deepseek-chat model as default for both BasicChat and AdvancedChat
            result["deepseek-chat"] = AIModelCapability.BasicChat | AIModelCapability.AdvancedChat;

            return result;
        }
    }
}
