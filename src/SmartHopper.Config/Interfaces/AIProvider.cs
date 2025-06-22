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
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Config.Managers;
using SmartHopper.Config.Models;

namespace SmartHopper.Config.Interfaces
{
    public interface IAIProvider
    {
        string Name { get; }

        string DefaultModel { get; }

        /// <summary>
        /// Gets the provider's icon. Should return a 16x16 image suitable for display in the UI.
        /// </summary>
        Image Icon { get; }

        /// <summary>
        /// Gets or sets whether this provider is enabled and should be available for use.
        /// This can be used to disable template or experimental providers.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Indicates whether this provider supports streaming responses.
        /// </summary>
        bool SupportsStreaming { get; }

        Task<AIResponse> GetResponse(
            JArray messages,
            string model,
            string jsonSchema = "",
            string endpoint = "",
            bool includeToolDefinitions = false,
            IProgress<ChatChunk>? progress = null
        );

        string GetModel(string requestedModel = "");

        /// <summary>
        /// Injects decrypted settings for this provider (called by ProviderManager).
        /// </summary>
        void InitializeSettings(Dictionary<string, object> settings);

        IEnumerable<SettingDescriptor> GetSettingDescriptors();
    }

    /// <summary>
    /// Base class for AI providers, encapsulating common logic.
    /// </summary>
    public abstract class AIProvider : IAIProvider
    {
        private Dictionary<string, object> _injectedSettings;

        public abstract string Name { get; }

        public abstract string DefaultModel { get; }

        protected abstract string ApiURL { get; }

        public abstract bool IsEnabled { get; }

        /// <summary>
        /// Indicates whether this provider supports streaming responses.
        /// </summary>
        public abstract bool SupportsStreaming { get; }

        public abstract Image Icon { get; }

        public async Task<AIResponse> GetResponse(
            JArray messages,
            string model,
            string jsonSchema = "",
            string endpoint = "",
            bool includeToolDefinitions = false,
            IProgress<ChatChunk>? progress = null
        )
        {
            var context = new RequestContext
            {
                Messages = messages,
                Model = this.GetModel(model),
                JsonSchema = jsonSchema,
                Endpoint = endpoint,
                IncludeToolDefinitions = includeToolDefinitions,
                DoStreaming = this.SupportsStreaming && this.GetSetting<bool>("EnableStreaming") && progress != null,
                Progress = progress,
            };

            this.PreCall(context);
            if (context.DoStreaming)
            {
                Debug.WriteLine($"[SmartHopper] STREAMING RESPONSE for {this.Name}");
                await this.CallStreamingAsync(context);
            }
            else
            {
                Debug.WriteLine($"[SmartHopper] STREAMING disabled for {this.Name}");
                await this.CallSyncAsync(context);
            }
            this.PostCall(context);
            return context.Response!;
        }

        /// <summary>
        /// Prepare the request context (e.g. populate Body, extract settings).
        /// </summary>
        protected virtual void PreCall(RequestContext context)
        {
        }

        /// <summary>
        /// Execute a non-streaming API call.
        /// </summary>
        protected virtual async Task CallSyncAsync(RequestContext context)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {this.GetSetting<string>("ApiKey")}");
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var resp = await client.PostAsync(this.ApiURL, new StringContent(context.Body.ToString(), Encoding.UTF8, "application/json")).ConfigureAwait(false);
            var jsonText = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            Debug.WriteLine($"[{this.Name}] Sync response: {jsonText}");
            context.RawJson = jsonText;
        }

        /// <summary>
        /// Execute a streaming API call, reporting chunks via context.Progress.
        /// </summary>
        protected virtual Task CallStreamingAsync(RequestContext context)
        {
            throw new NotImplementedException("Streaming not yet supported for this provider");
        }

        /// <summary>
        /// Finalize the response (e.g. parse accumulated text into AIResponse).
        /// </summary>
        protected virtual void PostCall(RequestContext context)
        {
        }

        /// <summary>
        /// Initializes the provider with the specified settings.
        /// </summary>
        /// <param name="settings">The decrypted settings to use.</param>
        public void InitializeSettings(Dictionary<string, object> settings)
        {
            _injectedSettings = settings ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets a setting value, with type conversion and fallback to default.
        /// </summary>
        /// <typeparam name="T">The expected type of the setting.</typeparam>
        /// <param name="key">The setting key.</param>
        /// <returns>The setting value, or default if not found.</returns>
        protected T GetSetting<T>(string key)
        {
            if (_injectedSettings == null)
            {
                Debug.WriteLine($"Warning: Settings not initialized for {Name}");
                return default;
            }

            if (!_injectedSettings.TryGetValue(key, out var value) || value == null)
            {
                // Try to get the default value from the descriptor
                var descriptor = GetSettingDescriptors().FirstOrDefault(d => d.Name == key);
                if (descriptor?.DefaultValue != null && descriptor.DefaultValue is T defaultValue)
                {
                    return defaultValue;
                }
                return default;
            }

            // Handle type conversion
            try
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
                else if (typeof(T) == typeof(int) && int.TryParse(value.ToString(), out var intValue))
                {
                    return (T)(object)intValue;
                }
                else if (typeof(T) == typeof(bool) && bool.TryParse(value.ToString(), out var boolValue))
                {
                    return (T)(object)boolValue;
                }
                else if (typeof(T) == typeof(string))
                {
                    return (T)(object)value.ToString();
                }
                else if (typeof(T) == typeof(double) && double.TryParse(value.ToString(), out var doubleValue))
                {
                    return (T)(object)doubleValue;
                }
                else
                {
                    Debug.WriteLine($"Warning: Failed to convert {key} to {typeof(T).Name} for provider {Name}");
                    return default;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting setting {key} for provider {Name}: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Default model resolution logic.
        /// </summary>
        public virtual string GetModel(string requestedModel = "")
        {
            string model = this.GetSetting<string>("Model");

            if (!string.IsNullOrWhiteSpace(requestedModel))
                return requestedModel;
            if (!string.IsNullOrWhiteSpace(model))
                return model;
            return DefaultModel;
        }

        /// <summary>
        /// Common tool formatting for function definitions.
        /// </summary>
        protected JArray GetFormattedTools()
        {
            try
            {
                AIToolManager.DiscoverTools();
                var tools = AIToolManager.GetTools();
                if (tools.Count == 0)
                {
                    Debug.WriteLine("No tools available.");
                    return null;
                }

                var toolsArray = new JArray();
                foreach (var tool in tools)
                {
                    var toolObject = new JObject
                    {
                        ["type"] = "function",
                        ["function"] = new JObject
                        {
                            ["name"] = tool.Value.Name,
                            ["description"] = tool.Value.Description,
                            ["parameters"] = JObject.Parse(tool.Value.ParametersSchema)
                        }
                    };
                    toolsArray.Add(toolObject);
                }
                return toolsArray;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error formatting tools: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns the SettingDescriptors for this provider by
        /// fetching its IAIProviderSettings instance from ProviderManager.
        /// </summary>
        public virtual IEnumerable<SettingDescriptor> GetSettingDescriptors()
        {
            var ui = ProviderManager.Instance.GetProviderSettings(Name);
            return ui?.GetSettingDescriptors()
                ?? Enumerable.Empty<SettingDescriptor>();
        }
    }
}
