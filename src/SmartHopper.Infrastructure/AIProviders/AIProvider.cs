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
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Base class for AI providers, encapsulating common logic.
    /// </summary>
    /// <typeparam name="T">The type of the derived provider class.</typeparam>
    public abstract class AIProvider<T> : AIProvider where T : AIProvider<T>
    {
        private static readonly Lazy<T> InstanceValue = new(() => Activator.CreateInstance(typeof(T), true) as T);

        /// <summary>
        /// Gets the singleton instance of the provider.
        /// </summary>
        public static T Instance => InstanceValue.Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIProvider{T}"/> class.
        /// Protected constructor to enforce the singleton pattern.
        /// </summary>
        protected AIProvider()
        {
        }
    }

    /// <summary>
    /// Base class for AI providers, encapsulating common logic.
    /// </summary>
    public abstract class AIProvider : IAIProvider
    {
        private Dictionary<string, object> _injectedSettings;
        private Dictionary<string, object> _defaultSettings;

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public abstract Image Icon { get; }

        /// <inheritdoc/>
        public abstract bool IsEnabled { get; }

        /// <inheritdoc/>
        public abstract string DefaultServerUrl { get; }

        /// <inheritdoc/>
        public IAIProviderModels Models { get; set; }

        /// <inheritdoc/>
        public virtual async Task InitializeProviderAsync()
        {
            try
            {
                // STEP 1: Register models FIRST to make them available for default value resolution
                // Prevent reloading capabilities if already initialized
                if (!ModelManager.Instance.HasProviderCapabilities(this.Name))
                {
                    Debug.WriteLine($"[{this.Name}] Registering model capabilities");

                    // Initialize the models manager asynchronously
                    var capabilitiesDict = await this.Models.RetrieveCapabilities().ConfigureAwait(false);
                    var defaultModelsDict = this.Models.RetrieveDefault();

                    // 1) Register every model the API knows about:
                    foreach (var capability in capabilitiesDict)
                    {
                        var defaultFor = FindDefaultCapabilityForModel(capability.Key, defaultModelsDict);

                        ModelManager.Instance.RegisterCapabilities(
                            this.Name,
                            capability.Key,
                            capability.Value,
                            defaultFor);

                        Debug.WriteLine($"[{this.Name}] Registered model {capability.Key} with capabilities {capability.Value.ToDetailedString()} and default {defaultFor.ToDetailedString()}");
                    }

                    // 2) Ensure concrete defaults are in the registry:
                    foreach (var (modelName, defaultCaps) in defaultModelsDict)
                    {
                        if (!capabilitiesDict.ContainsKey(modelName))
                        {
                            // Use RetrieveCapabilities which now handles wildcard resolution automatically
                            var capabilities = this.Models.RetrieveCapabilities(modelName);

                            ModelManager.Instance.RegisterCapabilities(
                                this.Name,
                                modelName,
                                capabilities,
                                defaultCaps
                            );

                            Debug.WriteLine($"[{this.Name}] Registered concrete default model {modelName} with capabilities {capabilities.ToDetailedString()} and default {defaultCaps.ToDetailedString()}");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"[{this.Name}] Capabilities already initialized, skipping reload");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] Error during model registration: {ex.Message}");
                // Continue initialization even if model registration fails
            }

            // STEP 2: Now load settings with models available for lazy default resolution
            // Initialize the provider with its settings from SmartHopperSettings
            var settingsDict = SmartHopperSettings.Instance.GetProviderSettings(this.Name);
            if (settingsDict == null)
            {
                settingsDict = new Dictionary<string, object>();
            }

            // Load default values to a separate dictionary to prevent circular dependencies during retrieval
            this._defaultSettings = new Dictionary<string, object>();
            try
            {
                var descriptors = this.GetSettingDescriptors();
                foreach (var descriptor in descriptors)
                {
                    if (descriptor.DefaultValue != null)
                    {
                        this._defaultSettings[descriptor.Name] = descriptor.DefaultValue;

                        // Also add to settingsDict if not already present
                        if (!settingsDict.ContainsKey(descriptor.Name))
                        {
                            settingsDict[descriptor.Name] = descriptor.DefaultValue;
                            Debug.WriteLine($"[{this.Name}] Applied default value for setting '{descriptor.Name}': {descriptor.DefaultValue}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] Warning: Could not load default values during initialization: {ex.Message}");
                // Continue initialization even if default value loading fails
            }

            // Apply all settings (stored + defaults) to the provider
            this.RefreshCachedSettings(settingsDict);
        }

        /// <inheritdoc/>
        public abstract string Encode(IAIRequest request);

        /// <inheritdoc/>
        public abstract string Encode(IAIInteraction interaction);

        /// <inheritdoc/>
        public abstract List<IAIInteraction> DecodeResponse(string response);
        
        /// <inheritdoc/>
        public abstract AIMetrics DecodeMetrics(string response);

        /// <inheritdoc/>
        public virtual IAIRequest PreCall(IAIRequest request)
        {
            return request;
        }

        /// <inheritdoc/>
        public async Task<IAIReturn> Call(IAIRequest request)
        {
            // Start stopwatch
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Execute PreCall
            request = this.PreCall(request);

            // Validate request before calling the API
            (bool isValid, List<string> errors) = request.IsValid();
            if (!isValid)
            {
                stopwatch.Stop();

                var error = "The request is not valid: " + string.Join(", ", errors);

                var result = new AIReturn();
                var metrics = new AIMetrics
                {
                    FinishReason = "error",
                    CompletionTime = stopwatch.Elapsed.TotalSeconds,
                };

                result = AIReturn.CreateError(error, request, metrics);

                return result;
            }

            // Execute CallApi
            var response = await this.CallApi(request);

            // Add provider specific metrics
            stopwatch.Stop();
            response.Metrics.CompletionTime = stopwatch.Elapsed.TotalSeconds;
            response.Metrics.Provider = this.Name;
            response.Metrics.Model = request.Model;

            // Execute PostCall
            response = this.PostCall(response);

            return response;
        }

        /// <inheritdoc/>
        public virtual IAIReturn PostCall(IAIReturn response)
        {
            try
            {
                // Determine status based on decoded interactions' tool calls
                var interactions = response.Result; // triggers provider Decode
                if (interactions != null && interactions.Any(i => i.ToolCalls != null && i.ToolCalls.Count > 0))
                {
                    response.Status = AICallStatus.CallingTools;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] PostCall metrics parsing error: {ex.Message}");
                // Keep original response on failure
            }

            return response;
        }

        /// <inheritdoc/>
        public string GetDefaultModel(AICapability requiredCapability = AICapability.BasicChat, bool useSettings = true)
        {
            // Use settings model if matches required capabilities
            if (useSettings)
            {
                string modelFromSettings = this.GetSetting<string>("Model");

                if (!string.IsNullOrWhiteSpace(modelFromSettings))
                {
                    if (ModelManager.Instance.ValidateCapabilities(this.Name, modelFromSettings, requiredCapability))
                    {
                        return modelFromSettings;
                    }
                }
            }

            // Else, try to get default model from ModelManager that matches the required capabilities
            string modelFromProviderDefault = ModelManager.Instance.GetDefaultModel(this.Name, requiredCapability);

            if (!string.IsNullOrWhiteSpace(modelFromProviderDefault))
            {
                return modelFromProviderDefault;
            }

            return null;
        }

        /// <summary>
        /// Refreshes the provider's cached settings by merging the input settings with existing cached settings.
        /// </summary>
        /// <param name="settings">The new settings to merge with existing cached settings.</param>
        /// <remarks>
        /// This method preserves any settings that were added by the provider itself (e.g., via SetSetting)
        /// while updating settings from external sources like the UI or configuration files.
        /// Input settings take precedence over existing cached settings for matching keys.
        /// </remarks>
        public void RefreshCachedSettings(Dictionary<string, object> settings)
        {
            if (this._injectedSettings == null)
            {
                this.ResetCachedSettings(settings);
                return;
            }

            if (settings != null)
            {
                foreach (var kvp in settings)
                {
                    this._injectedSettings[kvp.Key] = kvp.Value;
                }
            }
        }

        /// <inheritdoc/>
        public virtual IEnumerable<SettingDescriptor> GetSettingDescriptors()
        {
            var ui = ProviderManager.Instance.GetProviderSettings(this.Name);
            return ui?.GetSettingDescriptors()
                ?? Enumerable.Empty<SettingDescriptor>();
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
                // Check _defaultSettings field for default value (loaded during initialization)
                if (this._defaultSettings != null && this._defaultSettings.TryGetValue(key, out var defaultValue) && defaultValue != null)
                {
                    value = defaultValue;
                    Debug.WriteLine($"[{this.Name}] Using default value for setting '{key}': {defaultValue}");
                }
                else
                {
                    // No default value available, return type default
                    Debug.WriteLine($"[{this.Name}] No value or default found for setting '{key}'");
                    return default;
                }
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
        /// Sets a setting value for this provider with automatic provider scoping and persistence.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The setting value to store.</param>
        /// <remarks>
        /// This method automatically scopes the setting to the current provider (using this.Name)
        /// and integrates with the existing encryption system for secret settings.
        /// The setting is both updated in the local cache and persisted to disk.
        /// </remarks>
        protected void SetSetting(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.WriteLine($"Warning: Cannot set setting with empty key for provider {this.Name}");
                return;
            }

            try
            {
                // Update the local injected settings cache
                if (this._injectedSettings == null)
                {
                    this._injectedSettings = new Dictionary<string, object>();
                }
                this._injectedSettings[key] = value;

                // Persist to the global settings with provider scoping
                var settings = SmartHopper.Infrastructure.Settings.SmartHopperSettings.Instance;
                settings.SetSetting(this.Name, key, value);
                settings.Save();

                Debug.WriteLine($"[{this.Name}] Setting '{key}' updated and persisted");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting '{key}' for provider {this.Name}: {ex.Message}");
            }
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
        /// Makes an HTTP request to the specified endpoint with authentication.
        /// </summary>
        /// <param name="request">The request to make.</param>
        /// <returns>The HTTP response content as a type T.</returns>
        protected virtual async Task<IAIReturn> CallApi(IAIRequest request)
        {
            string endpoint = request.Endpoint;
            string httpMethod = request.HttpMethod;
            string requestBody = request.EncodedRequestBody;
            string contentType = request.ContentType;
            string authentication = request.Authentication;

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
                var path = endpoint.StartsWith("/", StringComparison.Ordinal) ? endpoint : "/" + endpoint;
                fullUrl = baseUrl + path;
            }

            Debug.WriteLine($"[{this.Name}] Call - Method: {httpMethod.ToUpper(CultureInfo.InvariantCulture)}, URL: {fullUrl}");

            using (var httpClient = new HttpClient())
            {
                // Set up authentication
                if (authentication.ToLower(CultureInfo.InvariantCulture) == "bearer")
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
                    switch (httpMethod.ToUpper(CultureInfo.InvariantCulture))
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

                    // Prepare the AIReturn
                    var aiReturn = AIReturn.CreateSuccess(
                        raw: content,
                        request: request,
                        metrics: new AIMetrics()
                        {
                            Provider = this.Name,
                            Model = request.Model,
                        });

                    return aiReturn;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{this.Name}] Call - Exception: {ex.Message}");
                    throw new Exception($"Error calling {this.Name} API: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Resets the provider's cached settings, completely replacing them with the specified settings.
        /// </summary>
        /// <param name="settings">The decrypted settings to use.</param>
        /// <remarks>
        /// This method completely replaces the cached settings. Use RefreshCachedSettings if you want to merge settings instead.
        /// </remarks>
        private void ResetCachedSettings(Dictionary<string, object> settings)
        {
            this._injectedSettings = settings ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Finds the default capability for a model by checking exact matches first, then wildcard patterns.
        /// Supports both directions: wildcard in capabilities matching specific in defaults, and vice versa.
        /// </summary>
        /// <param name="modelName">The model name from capabilities (may contain wildcards).</param>
        /// <param name="defaultModelsDict">Dictionary of default models with their capabilities.</param>
        /// <returns>The default capability for the model, or AICapability.None if no match found.</returns>
        private static AICapability FindDefaultCapabilityForModel(string modelName, Dictionary<string, AICapability> defaultModelsDict)
        {
            // First, try exact match (existing behavior)
            if (defaultModelsDict.ContainsKey(modelName))
            {
                Debug.WriteLine($"[ModelManager.FindDefaultCapabilityForModel] Found exact match for {modelName}: {defaultModelsDict[modelName]}");
                return defaultModelsDict[modelName];
            }

            // If modelName contains wildcard, match against specific names in defaults
            if (modelName.Contains("*"))
            {
                var pattern = modelName.Replace("*", "");
                var matchingDefault = defaultModelsDict.FirstOrDefault(kvp => kvp.Key.StartsWith(pattern));
                if (!matchingDefault.Equals(default(KeyValuePair<string, AICapability>)))
                {
                    Debug.WriteLine($"[ModelManager.FindDefaultCapabilityForModel] Found wildcard match for {modelName}: {matchingDefault.Value}");
                    return matchingDefault.Value;
                }
            }
            
            // If no wildcard in modelName, check if any defaults contain wildcards that match this specific name
            var matchingWildcard = defaultModelsDict.FirstOrDefault(kvp =>
                kvp.Key.Contains("*") && modelName.StartsWith(kvp.Key.Replace("*", "")));
            if (!matchingWildcard.Equals(default(KeyValuePair<string, AICapability>)))
            {
                Debug.WriteLine($"[ModelManager.FindDefaultCapabilityForModel] Found wildcard match for {modelName}: {matchingWildcard.Value}");
                return matchingWildcard.Value;
            }

            // No match found
            Debug.WriteLine($"[ModelManager.FindDefaultCapabilityForModel] No match found for {modelName}");
            return AICapability.None;
        }
    }
}
