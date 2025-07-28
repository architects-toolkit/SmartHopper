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

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// OpenAI provider-specific model management implementation.
    /// </summary>
    public class OpenAIProviderModels : AIProviderModels
    {
        private readonly OpenAIProvider _openAIProvider;

        public OpenAIProviderModels(OpenAIProvider provider, Func<string, string, string, string, string, Task<string>> apiCaller) : base(provider, apiCaller)
        {
            this._openAIProvider = provider;
        }

        /// <summary>
        /// Retrieves the list of available model names for OpenAI.
        /// </summary>
        /// <returns>A list of available model names.</returns>
        public override async Task<List<string>> RetrieveAvailable()
        {
            Debug.WriteLine("[OpenAI] Retrieving available models");
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
                Debug.WriteLine($"[OpenAI] Exception retrieving models: {ex.Message}");
                throw new Exception($"Error retrieving models from OpenAI API: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets all models and their capabilities supported by OpenAI, fetching fresh data from API.
        /// </summary>
        /// <returns>Dictionary of model names and their capabilities.</returns>
        public override async Task<Dictionary<string, AIModelCapability>> RetrieveCapabilities()
        {
            var result = new Dictionary<string, AIModelCapability>();

            // GPT-4.1 models - text input/output, image input, structured output, function calling
            result["gpt-4.1*"] = AIModelCapability.TextInput | AIModelCapability.TextOutput | AIModelCapability.ImageInput | AIModelCapability.StructuredOutput | AIModelCapability.FunctionCalling;

            // O4-mini models - text input/output, image input, structured output, function calling
            result["o4-mini*"] = AIModelCapability.TextInput | AIModelCapability.TextOutput | AIModelCapability.ImageInput | AIModelCapability.StructuredOutput | AIModelCapability.FunctionCalling;

            // O3-mini models - text input/output, structured output, function calling
            result["o3-mini*"] = AIModelCapability.TextInput | AIModelCapability.TextOutput | AIModelCapability.StructuredOutput | AIModelCapability.FunctionCalling;

            // GPT-4o models - text input/output, image input, structured output, function calling
            result["gpt-4o*"] = AIModelCapability.TextInput | AIModelCapability.TextOutput | AIModelCapability.ImageInput | AIModelCapability.StructuredOutput | AIModelCapability.FunctionCalling;

            // GPT-image-1 - text input, image input and output
            result["gpt-image-1"] = AIModelCapability.TextInput | AIModelCapability.ImageInput | AIModelCapability.ImageOutput;

            // DALL-E 3 - text input, image output
            result["dall-e-3"] = AIModelCapability.TextInput | AIModelCapability.ImageOutput;

            // DALL-E 2 - text input, image output
            result["dall-e-2"] = AIModelCapability.TextInput | AIModelCapability.ImageOutput;

            // GPT-3.5 models - text input and output
            result["gpt-3.5*"] = AIModelCapability.TextInput | AIModelCapability.TextOutput;

            // GPT-4 models - text input and output
            result["gpt-4"] = AIModelCapability.TextInput | AIModelCapability.TextOutput;
            result["gpt-4-0613"] = AIModelCapability.TextInput | AIModelCapability.TextOutput;
            result["gpt-4-0314"] = AIModelCapability.TextInput | AIModelCapability.TextOutput;

            return result;
        }
    }
}
