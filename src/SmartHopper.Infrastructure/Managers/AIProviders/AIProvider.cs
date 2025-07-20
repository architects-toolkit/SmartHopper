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
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.Interfaces;
using SmartHopper.Infrastructure.Managers.AITools;
using SmartHopper.Infrastructure.Models;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Infrastructure.Managers.AIProviders
{
    /// <summary>
    /// Base class for AI providers, encapsulating common logic.
    /// </summary>
    public abstract class AIProvider : IAIProvider
    {
        private Dictionary<string, object> _injectedSettings;

        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the default model name for the provider.
        /// </summary>
        public abstract string DefaultModel { get; }

        /// <summary>
        /// Gets the default server URL for the provider.
        /// </summary>
        public abstract string DefaultServerUrl { get; }

        /// <summary>
        /// Gets a value indicating whether the provider is enabled.
        /// </summary>
        public abstract bool IsEnabled { get; }

        /// <summary>
        /// Gets the icon representing the provider.
        /// </summary>
        public abstract Image Icon { get; }

        /// <summary>
        /// Retrieves a response from the AI model based on provided messages and parameters.
        /// </summary>
        /// <param name="messages">The conversation messages to send.</param>
        /// <param name="model">The model to use.</param>
        /// <param name="jsonSchema">Optional JSON schema to validate the response.</param>
        /// <param name="endpoint">Optional endpoint to send the request to.</param>
        /// <param name="toolFilter">Optional filter to specify which tools are available.</param>
        /// <returns>An AIResponse containing the result.</returns>
        public abstract Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", string? toolFilter = null);

        /// <summary>
        /// Initializes the provider with the specified settings.
        /// </summary>
        /// <param name="settings">The decrypted settings to use.</param>
        public void InitializeSettings(Dictionary<string, object> settings)
        {
            this._injectedSettings = settings ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets a setting value, with type conversion and fallback to default.
        /// </summary>
        /// <typeparam name="T">The expected type of the setting.</typeparam>
        /// <param name="key">The setting key.</param>
        /// <returns>The setting value, or default if not found.</returns>
        protected T GetSetting<T>(string key)
        {
            if (this._injectedSettings == null)
            {
                Debug.WriteLine($"Warning: Settings not initialized for {this.Name}");
                return default;
            }

            if (!this._injectedSettings.TryGetValue(key, out var value) || value == null)
            {
                // Try to get the default value from the descriptor
                var descriptor = this.GetSettingDescriptors().FirstOrDefault(d => d.Name == key);
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
                    Debug.WriteLine($"Warning: Failed to convert {key} to {typeof(T).Name} for provider {this.Name}");
                    return default;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting setting {key} for provider {this.Name}: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Gets the model to use for AI processing.
        /// </summary>
        /// <param name="requestedModel">The requested model, or empty for default.</param>
        /// <returns>The model to use.</returns>
        public virtual string GetModel(string requestedModel = "")
        {
            // Use the requested model if provided
            if (!string.IsNullOrWhiteSpace(requestedModel))
            {
                return requestedModel;
            }

            // Use the model from settings if available
            string modelFromSettings = this.GetSetting<string>("Model");
            if (!string.IsNullOrWhiteSpace(modelFromSettings))
            {
                return modelFromSettings;
            }

            // Fall back to the default model
            return this.DefaultModel;
        }

        /// <summary>
        /// Gets all formatted tools with the default filter.
        /// </summary>
        /// <returns>A JArray of formatted tool function definitions.</returns>
        protected JArray GetFormattedTools()
        {
            // When no tool filter is specified, return all tools
            return this.GetFormattedTools("*");
        }

        /// <summary>
        /// Common tool formatting for function definitions.
        /// </summary>
        /// <param name="toolFilter">The filter to apply to tool categories.</param>
        /// <returns>A JArray of formatted tool function definitions matching the filter, or null if an error occurs.</returns>
        protected JArray GetFormattedTools(string toolFilter)
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
                var toolFilterObj = Filtering.Parse(toolFilter);
                foreach (var tool in tools)
                {
                    if (!toolFilterObj.ShouldInclude(tool.Value.Category))
                    {
                        // Skip tools that don't match the filter
                        continue;
                    }

                    var toolObject = new JObject
                    {
                        ["type"] = "function",
                        ["function"] = new JObject
                        {
                            ["name"] = tool.Value.Name,
                            ["description"] = tool.Value.Description,
                            ["parameters"] = JObject.Parse(tool.Value.ParametersSchema),
                        },
                    };

                    toolsArray.Add(toolObject);
                }

                Debug.WriteLine($"[GetFormattedTools] {toolsArray.Count} tools formatted");

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
        /// <returns>An enumerable of SettingDescriptor instances for the provider.</returns>
        public virtual IEnumerable<SettingDescriptor> GetSettingDescriptors()
        {
            var ui = ProviderManager.Instance.GetProviderSettings(this.Name);
            return ui?.GetSettingDescriptors()
                ?? Enumerable.Empty<SettingDescriptor>();
        }

        /// <summary>
        /// Retrieves the list of available model names for this provider.
        /// </summary>
        /// <returns>A list of available model names.</returns>
        public virtual Task<List<string>> RetrieveAvailableModels()
        {
            return Task.FromResult(new List<string>());
        }

        /// <summary>
        /// Makes an HTTP request to the specified endpoint with authentication.
        /// </summary>
        /// <param name="endpoint">The endpoint to call. Can be a full URL or a relative path.</param>
        /// <param name="httpMethod">The HTTP method to use (GET, POST, DELETE, PATCH). Defaults to GET.</param>
        /// <param name="requestBody">The request body content for POST and PATCH requests.</param>
        /// <param name="contentType">The content type for the request body. Defaults to "application/json".</param>
        /// <param name="authentication">The authentication method to use. Currently only "bearer" is supported.</param>
        /// <returns>The HTTP response content as a string.</returns>
        protected virtual async Task<string> CallApi(string endpoint, string httpMethod = "GET", string requestBody = null, string contentType = "application/json", string authentication = "bearer")
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));
            }

            // Determine the full URL
            string fullUrl;
            if (Uri.IsWellFormedUriString(endpoint, UriKind.Absolute))
            {
                // Endpoint is a full URL
                fullUrl = endpoint;
            }
            else
            {
                // Endpoint is a relative path, append to DefaultServerUrl
                var baseUrl = this.DefaultServerUrl.TrimEnd('/');
                var path = endpoint.StartsWith("/") ? endpoint : "/" + endpoint;
                fullUrl = baseUrl + path;
            }

            Debug.WriteLine($"[{this.Name}] Call - Method: {httpMethod.ToUpper()}, URL: {fullUrl}");

            using (var httpClient = new HttpClient())
            {
                // Set up authentication
                if (authentication.ToLower() == "bearer")
                {
                    string apiKey = this.GetSetting<string>("ApiKey");
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        throw new Exception($"{this.Name} API key is not configured or is invalid.");
                    }

                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                }
                else
                {
                    throw new NotSupportedException($"Authentication method '{authentication}' is not supported. Only 'bearer' is currently supported.");
                }

                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                try
                {
                    HttpResponseMessage response;
                    switch (httpMethod.ToUpper())
                    {
                        case "GET":
                            response = await httpClient.GetAsync(fullUrl).ConfigureAwait(false);
                            break;
                        case "POST":
                            var postContent = !string.IsNullOrEmpty(requestBody)
                                ? new StringContent(requestBody, Encoding.UTF8, contentType)
                                : null;
                            response = await httpClient.PostAsync(fullUrl, postContent).ConfigureAwait(false);
                            break;
                        case "DELETE":
                            response = await httpClient.DeleteAsync(fullUrl).ConfigureAwait(false);
                            break;
                        case "PATCH":
                            var patchContent = !string.IsNullOrEmpty(requestBody)
                                ? new StringContent(requestBody, Encoding.UTF8, contentType)
                                : null;
                            response = await httpClient.PatchAsync(fullUrl, patchContent).ConfigureAwait(false);
                            break;
                        default:
                            throw new NotSupportedException($"HTTP method '{httpMethod}' is not supported. Supported methods: GET, POST, DELETE, PATCH");
                    }

                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Debug.WriteLine($"[{this.Name}] Call - Response status: {response.StatusCode}");

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[{this.Name}] Call - Error response: {content}");
                        throw new Exception($"Error from {this.Name} API: {response.StatusCode} - {content}");
                    }

                    return content;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{this.Name}] Call - Exception: {ex.Message}");
                    throw new Exception($"Error calling {this.Name} API: {ex.Message}", ex);
                }
            }
        }
    }
}
