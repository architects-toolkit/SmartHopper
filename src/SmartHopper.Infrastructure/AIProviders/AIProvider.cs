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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.Infrastructure.Streaming;
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
        /// Initializes a new instance of the <see cref="AIProvider{T}"/> class.
        /// Protected constructor to enforce the singleton pattern.
        /// </summary>
        protected AIProvider()
        {
        }

        /// <summary>
        /// Builds an absolute Uri for a provider endpoint using <see cref="DefaultServerUrl"/>.
        /// Ensures consistent normalization across call and streaming paths.
        /// </summary>
        /// <param name="endpoint">Absolute URL or provider-relative endpoint (with or without leading '/').</param>
        /// <returns>Absolute <see cref="Uri"/> for the request.</returns>
        public override Uri BuildFullUrl(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));
            }

            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var abs))
            {
                return abs;
            }

            var baseUri = this.DefaultServerUrl ?? throw new InvalidOperationException("DefaultServerUrl is not configured.");

            // Normalization of baseUri to ensure it ends with a trailing slash
            if (!baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
            {
                baseUri = new Uri(baseUri.AbsoluteUri + "/");
            }

            var relative = endpoint.StartsWith("/", StringComparison.Ordinal) ? endpoint.Substring(1) : endpoint;

            return new Uri(baseUri, relative);
        }

        /// <summary>
        /// Gets the singleton instance of the provider.
        /// </summary>
        public static T Instance => InstanceValue.Value;

    }

    /// <summary>
    /// Base class for AI providers, encapsulating common logic.
    /// </summary>
    public abstract class AIProvider : IAIProvider
    {
        private Dictionary<string, object> _injectedSettings;
        private Dictionary<string, object> _defaultSettings;

        // Recursion guard to prevent infinite loops during settings access
        [ThreadStatic]
        private static HashSet<string> _currentlyGettingSettings;

        // Streaming adapter cache to avoid reflection on every streaming request
        private IStreamingAdapter _cachedStreamingAdapter;
        private bool _streamingAdapterProbed;

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public abstract Image Icon { get; }

        /// <inheritdoc/>
        public abstract bool IsEnabled { get; }

        /// <summary>
        /// Gets the default server URL for the provider.
        /// </summary>
        public abstract Uri DefaultServerUrl { get; }

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

                    // Retrieve full model metadata and register each model
                    var models = await this.Models.RetrieveModels().ConfigureAwait(false);
                    if (models != null)
                    {
                        foreach (var m in models)
                        {
                            if (m == null) continue;

                            // Ensure provider name is set and normalized
                            if (string.IsNullOrWhiteSpace(m.Provider))
                            {
                                m.Provider = this.Name.ToLowerInvariant();
                            }

                            ModelManager.Instance.SetCapabilities(m);
                            Debug.WriteLine($"[{this.Name}] Registered model {m.Model} with capabilities {m.Capabilities.ToDetailedString()} and default {m.Default.ToDetailedString()}");
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
        public abstract string Encode(AIRequestCall request);

        /// <inheritdoc/>
        public abstract string Encode(IAIInteraction interaction);

        /// <summary>
        /// Encode multiple interactions.
        /// </summary>
        /// <param name="interactions">The interactions to encode.</param>
        /// <returns>The encoded string.</returns>
        public virtual string Encode(List<IAIInteraction> interactions)
        {
            var result = string.Empty;
            foreach (var interaction in interactions)
            {
                result += this.Encode(interaction);
            }

            return result;
        }

        /// <summary>
        /// Merges System and Summary interactions into a single System interaction.
        /// Summary interactions are appended to the System prompt with a separator.
        /// </summary>
        /// <param name="interactions">The interactions to process.</param>
        /// <returns>A new list with System and Summary merged.</returns>
        protected List<IAIInteraction> MergeSystemAndSummary(IReadOnlyList<IAIInteraction> interactions)
        {
            if (interactions == null || interactions.Count == 0)
            {
                return new List<IAIInteraction>();
            }

            var result = new List<IAIInteraction>();
            AIInteractionText systemInteraction = null;
            var summaries = new List<AIInteractionText>();

            // First pass: collect System and Summary interactions
            foreach (var interaction in interactions)
            {
                if (interaction.Agent == AIAgent.System && interaction is AIInteractionText systemText)
                {
                    systemInteraction = systemText;
                }
                else if (interaction.Agent == AIAgent.Summary && interaction is AIInteractionText summaryText)
                {
                    summaries.Add(summaryText);
                }
            }

            // Build merged system message if we have summaries
            if (summaries.Count > 0)
            {
                var mergedContent = new StringBuilder();

                // Add original system prompt
                if (systemInteraction != null && !string.IsNullOrWhiteSpace(systemInteraction.Content))
                {
                    mergedContent.AppendLine(systemInteraction.Content);
                    mergedContent.AppendLine();
                }

                // Add separator and summaries
                mergedContent.AppendLine("---");
                mergedContent.AppendLine("This is a summary of the previous messages in the conversation:");
                mergedContent.AppendLine();

                foreach (var summary in summaries)
                {
                    if (!string.IsNullOrWhiteSpace(summary.Content))
                    {
                        mergedContent.AppendLine(summary.Content);
                        mergedContent.AppendLine();
                    }
                }

                // Create merged system interaction
                var merged = new AIInteractionText
                {
                    Agent = AIAgent.System,
                    Content = mergedContent.ToString().TrimEnd(),
                    Time = systemInteraction?.Time ?? DateTime.UtcNow,
                };

                result.Add(merged);
            }
            else if (systemInteraction != null)
            {
                // No summaries, just add the original system interaction
                result.Add(systemInteraction);
            }

            // Second pass: add all non-System, non-Summary interactions
            foreach (var interaction in interactions)
            {
                if (interaction.Agent != AIAgent.System && interaction.Agent != AIAgent.Summary)
                {
                    result.Add(interaction);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public abstract List<IAIInteraction> Decode(JObject response);

        /// <inheritdoc/>
        public virtual AIRequestCall PreCall(AIRequestCall request)
        {
            // Ensure provider is attached if missing so callers can simply do provider.Call(request)
            if (request != null && string.IsNullOrWhiteSpace(request.Provider))
            {
                request.Provider = this.Name;
            }

            return request;
        }

        /// <inheritdoc/>
        public async Task<IAIReturn> Call(AIRequestCall request)
        {
            // Start stopwatch
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Execute PreCall
            request = this.PreCall(request);

            // Validate request before calling the API (structured messages)
            (bool isValid, List<AIRuntimeMessage> messages) = request.IsValid();
            if (!isValid)
            {
                stopwatch.Stop();
                var result = new AIReturn();
                var metrics = new AIMetrics
                {
                    FinishReason = "error",
                    CompletionTime = stopwatch.Elapsed.TotalSeconds,
                };

                // Create error; request validation messages (errors) will appear via AIReturn.Messages (Request.Messages)
                result.CreateError("The request is not valid", request, metrics);

                return result;
            }

            // Execute CallApi
            var response = await this.CallApi(request);

            // For backoffice/admin-style calls, return raw without chat decoding or timestamping
            if (request?.RequestKind == AIRequestKind.Backoffice)
            {
                // Stop local timing but avoid setting completion time in response as requested
                stopwatch.Stop();
                return response;
            }

            // Add provider specific metrics
            stopwatch.Stop();
            response.SetCompletionTime(stopwatch.Elapsed.TotalSeconds);

            // Execute PostCall
            response = this.PostCall(response);

            return response;
        }

        /// <inheritdoc/>
        public virtual IAIReturn PostCall(IAIReturn response)
        {
            return response;
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
            return ui != null ? ui.GetSettingDescriptors() : Enumerable.Empty<SettingDescriptor>();
        }

        /// <summary>
        /// Gets a setting value, with type conversion and fallback to default.
        /// </summary>
        /// <typeparam name="T">The expected type of the setting.</typeparam>
        /// <param name="key">The setting key.</param>
        /// <returns>The setting value, or default if not found.</returns>
        protected T GetSetting<T>(string key)
        {
            // Initialize recursion guard if needed
            if (_currentlyGettingSettings == null)
            {
                _currentlyGettingSettings = new HashSet<string>();
            }

            var settingKey = $"{this.Name}.{key}";

            // Check for recursion
            if (_currentlyGettingSettings.Contains(settingKey))
            {
                Debug.WriteLine($"[AIProvider] Recursion detected for {settingKey}, returning default to break cycle");
                return default;
            }

            try
            {
                _currentlyGettingSettings.Add(settingKey);

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
            finally
            {
                _currentlyGettingSettings.Remove(settingKey);
            }
        }

        /// <inheritdoc/>
        public string GetDefaultModel(AICapability requiredCapability = AICapability.Text2Text, bool useSettings = true)
        {
            // Use settings model if matches requiredCapabilites
            if (useSettings && this.GetSetting<string>("Model") != null)
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

        /// <inheritdoc/>
        public virtual string SelectModel(AICapability requiredCapability, string requestedModel)
        {
            // Prefer provider-configured default from settings when compatible
            var preferredDefault = this.GetDefaultModel(requiredCapability, useSettings: true);

            // Delegate to centralized selection policy to avoid duplication
            var selected = ModelManager.Instance.SelectBestModel(this.Name, requestedModel, requiredCapability, preferredDefault);
            return selected ?? string.Empty;
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
        private async Task<IAIReturn> CallApi(AIRequestCall request)
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

            // Determine the full URL using centralized normalization
            Uri fullUri = this.BuildFullUrl(endpoint);

            Debug.WriteLine($"[{this.Name}] Call - Method: {httpMethod.ToUpper(CultureInfo.InvariantCulture)}, URL: {fullUri}");

            using (var httpClient = new HttpClient())
            {
                // Apply per-request timeout (policy should normalize, but clamp defensively)
                try
                {
                    var seconds = request?.TimeoutSeconds ?? 120;
                    httpClient.Timeout = TimeSpan.FromSeconds(seconds);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{this.Name}] Warning: could not set HttpClient timeout: {ex.Message}");
                }

                // Set up authentication from request
                var auth = authentication?.Trim().ToLowerInvariant();

                // Centralized API key handling: fetch from provider settings
                var apiKey = this.GetSetting<string>("ApiKey");

                if (string.IsNullOrWhiteSpace(auth) || auth == "none")
                {
                    // no auth
                }
                else if (auth == "bearer")
                {
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        throw new InvalidOperationException($"{this.Name} API key is not configured or is invalid.");
                    }

                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                }
                else if (auth == "x-api-key")
                {
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        throw new InvalidOperationException($"{this.Name} API key is not configured or is invalid.");
                    }

                    httpClient.DefaultRequestHeaders.Remove("x-api-key");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);
                }
                else
                {
                    throw new NotSupportedException($"Authentication method '{authentication}' is not supported. Supported: 'none', 'bearer', 'x-api-key'.");
                }

                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Apply additional request-scoped headers via shared helper.
                // Reserved headers are applied internally via authentication helpers: 'Authorization', 'x-api-key'.
                HttpHeadersHelper.ApplyExtraHeaders(httpClient, request.Headers);

                try
                {
                    HttpResponseMessage response;
                    switch (httpMethod.ToUpper(CultureInfo.InvariantCulture))
                    {
                        case "GET":
                            response = await httpClient.GetAsync(fullUri).ConfigureAwait(false);
                            break;
                        case "POST":
                            var postContent = !string.IsNullOrEmpty(requestBody)
                                ? new StringContent(requestBody, Encoding.UTF8, contentType)
                                : null;
                            response = await httpClient.PostAsync(fullUri, postContent).ConfigureAwait(false);
                            break;
                        case "DELETE":
                            response = await httpClient.DeleteAsync(fullUri).ConfigureAwait(false);
                            break;
                        case "PATCH":
                            var patchContent = !string.IsNullOrEmpty(requestBody)
                                ? new StringContent(requestBody, Encoding.UTF8, contentType)
                                : null;
                            response = await httpClient.PatchAsync(fullUri, patchContent).ConfigureAwait(false);
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
                    JObject rawJObject;
                    try
                    {
                        rawJObject = JObject.Parse(content);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[{this.Name}] Call - Failed to parse JSON response: {ex.Message}");
                        throw;
                    }

                    var aiReturn = new AIReturn();
                    aiReturn.CreateSuccess(
                        raw: rawJObject,
                        request: request);

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
        /// Gets the cached streaming adapter for this provider.
        /// Override <see cref="CreateStreamingAdapter"/> in derived classes to provide a streaming adapter.
        /// </summary>
        /// <returns>The cached streaming adapter, or null if the provider doesn't support streaming.</returns>
        public IStreamingAdapter GetStreamingAdapter()
        {
            if (!this._streamingAdapterProbed)
            {
                this._cachedStreamingAdapter = this.CreateStreamingAdapter();
                this._streamingAdapterProbed = true;
                Debug.WriteLine(this._cachedStreamingAdapter != null
                    ? $"[{this.Name}] Streaming adapter cached"
                    : $"[{this.Name}] No streaming adapter available");
            }

            return this._cachedStreamingAdapter;
        }

        /// <summary>
        /// Creates a streaming adapter for this provider. Override in derived classes to enable streaming.
        /// </summary>
        /// <returns>An <see cref="IStreamingAdapter"/> instance, or null if streaming is not supported.</returns>
        protected virtual IStreamingAdapter CreateStreamingAdapter()
        {
            return null;
        }

        /// <summary>
        /// Builds a full URL by combining the default server URL with the specified endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint path to append to the base URL.</param>
        /// <returns>A complete URI combining the default server URL with the endpoint.</returns>
        public virtual Uri BuildFullUrl(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));
            }

            if (this.DefaultServerUrl == null)
            {
                throw new InvalidOperationException("DefaultServerUrl is not set");
            }

            // Combine base URL with endpoint
            return new Uri(this.DefaultServerUrl, endpoint.TrimStart('/'));
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
    }
}
