/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Diagnostics;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.Infrastructure.Streaming;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Base class for AI providers, encapsulating common logic.
    /// </summary>
    /// <typeparam name="T">The type of the derived provider class.</typeparam>
    public abstract class AIProvider<T> : AIProvider
        where T : AIProvider<T>
    {
        private static readonly Lazy<T> InstanceValue = new (() =>
        {
            try
            {
                var instance = Activator.CreateInstance(typeof(T), nonPublic: true) as T;
                if (instance == null)
                {
                    throw new InvalidOperationException($"Failed to create instance of {typeof(T).FullName}. Activator.CreateInstance returned null.");
                }

                return instance;
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                Debug.WriteLine($"[AIProvider<{typeof(T).Name}>] Constructor threw exception: {tie.InnerException.GetType().Name}: {tie.InnerException.Message}");
                Debug.WriteLine($"[AIProvider<{typeof(T).Name}>] Stack trace: {tie.InnerException.StackTrace}");
                throw tie.InnerException;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIProvider<{typeof(T).Name}>] Failed to create instance: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[AIProvider<{typeof(T).Name}>] Stack trace: {ex.StackTrace}");
                throw;
            }
        });

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

            // On macOS, Uri.TryCreate with UriKind.Absolute treats paths like
            // "/chat/completions" as file:///chat/completions.
            // For security reasons, only accept HTTP(S) schemes for API endpoints.
            // This prevents file:// URIs and other potentially unsafe schemes.
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out Uri abs)
                && (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
            {
                return abs;
            }

            var baseUri = this.DefaultServerUrl ?? throw new InvalidOperationException($"DefaultServerUrl is not configured for provider {this.Name}.");

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

        /// <inheritdoc/>
        public abstract bool IsConfigured { get; }

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
        public async Task<IAIReturn> Call(AIRequestCall request, CancellationToken cancellationToken = default)
        {
            // Start stopwatch
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Execute PreCall
            request = this.PreCall(request);

            // Ensure the provider is configured before attempting any API call.
            // This is computed from the persisted settings, so it stays in sync with the current environment.
            if (!this.IsConfigured)
            {
                stopwatch.Stop();
                var configurationError = new AIReturn();
                var configurationMetrics = new AIMetrics
                {
                    FinishReason = "error",
                    CompletionTime = stopwatch.Elapsed.TotalSeconds,
                };

                configurationError.CreateError($"{this.Name} provider is not configured. Please set the required provider settings in SmartHopper settings.", request, configurationMetrics);

                return configurationError;
            }

            // Validate request before calling the API (structured messages)
            (bool isValid, List<SHRuntimeMessage> messages) = request.IsValid();
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
            var response = await this.CallApi(request, cancellationToken).ConfigureAwait(false);

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
        /// Checks whether the specified provider setting is present and non-empty in the persisted settings.
        /// This reads directly from storage, not the in-memory cache, so it reflects the current configuration.
        /// </summary>
        /// <param name="settingName">The setting key to check.</param>
        /// <returns>True if the setting has a non-empty value; otherwise, false.</returns>
        protected bool IsSettingConfigured(string settingName)
        {
            var settings = SmartHopperSettings.Instance?.GetProviderSettings(this.Name);
            if (settings == null || !settings.TryGetValue(settingName, out var value) || value == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(value.ToString());
        }

        /// <summary>
        /// Checks whether the specified provider setting is a valid absolute HTTP(S) URL in the persisted settings.
        /// This reads directly from storage, not the in-memory cache, so it reflects the current configuration.
        /// </summary>
        /// <param name="settingName">The setting key to check.</param>
        /// <returns>True if the setting is a valid HTTP(S) URL; otherwise, false.</returns>
        protected bool IsUrlSettingConfigured(string settingName)
        {
            var settings = SmartHopperSettings.Instance?.GetProviderSettings(this.Name);
            if (settings == null || !settings.TryGetValue(settingName, out var value) || value == null)
            {
                return false;
            }

            var url = value.ToString();
            return !string.IsNullOrWhiteSpace(url)
                && Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
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
        /// Creates an HttpClient configured for batch operations with the specified timeout.
        /// The timeout should be resolved by RequestTimeoutPolicy before calling this method.
        /// If no timeout is provided, falls back to <see cref="TimeoutDefaults.DefaultTimeoutSeconds"/>.
        /// </summary>
        /// <param name="requestTimeoutSeconds">Optional per-request timeout in seconds. If not provided, defaults to <see cref="TimeoutDefaults.DefaultTimeoutSeconds"/>.</param>
        /// <returns>A new HttpClient with appropriate timeout configured.</returns>
        protected HttpClient CreateBatchHttpClient(int? requestTimeoutSeconds = null)
        {
            var client = new HttpClient();

            // Use provided timeout or shared default.
            // Actual timeout resolution from settings is handled by RequestTimeoutPolicy upstream.
            int timeoutSeconds = requestTimeoutSeconds ?? TimeoutDefaults.DefaultTimeoutSeconds;

            // Clamp to shared bounds
            timeoutSeconds = Math.Max(TimeoutDefaults.MinTimeoutSeconds, Math.Min(timeoutSeconds, TimeoutDefaults.MaxTimeoutSeconds));

            try
            {
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                Debug.WriteLine($"[{this.Name}] Batch HttpClient timeout set to {timeoutSeconds}s");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{this.Name}] Warning: could not set batch HttpClient timeout: {ex.Message}");
            }

            return client;
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
                    if (!tool.Value.Enabled)
                    {
                        // Skip tools that are disabled (e.g., experimental or unsupported)
                        continue;
                    }

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
                            ["description"] = tool.Value.RichDescription,
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
        /// Builds a descriptive error message for a non-success HTTP response and classifies whether
        /// it should be surfaced as a network-style error (transient/connectivity) or as a provider error
        /// (client-side misuse such as auth/payload).
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="reasonPhrase">HTTP reason phrase or status enum name.</param>
        /// <param name="content">Raw response body for context.</param>
        /// <param name="providerName">Provider name to embed in the message.</param>
        /// <returns>Tuple of (enriched message, isNetworkLike). Network-like covers 5xx, 408 and 429.</returns>
        public static (string Message, bool IsNetworkLike) ClassifyHttpError(int statusCode, string reasonPhrase, string content, string providerName)
        {
            string message;
            bool isNetworkLike;
            switch (statusCode)
            {
                case 401:
                case 403:
                    message = $"HTTP {statusCode}: Authentication failed for {providerName}. Check your API key. Response: {content}";
                    isNetworkLike = false;
                    break;
                case 408:
                    message = $"HTTP 408 Request Timeout: The request to {providerName} took too long. Try increasing the HTTP request timeout in SmartHopper settings. Response: {content}";
                    isNetworkLike = true;
                    break;
                case 413:
                    message = $"HTTP 413 Payload Too Large: The request to {providerName} exceeds size limits. Try reducing input length or batch size. Response: {content}";
                    isNetworkLike = false;
                    break;
                case 429:
                    message = $"HTTP 429 Too Many Requests: Rate limit exceeded for {providerName}. Please retry after a delay. Response: {content}";
                    isNetworkLike = true;
                    break;
                case 500:
                    message = $"HTTP 500 Internal Server Error: {providerName} encountered an internal error. Retry after a brief delay. Response: {content}";
                    isNetworkLike = true;
                    break;
                case 502:
                    message = $"HTTP 502 Bad Gateway: {providerName} gateway error. The upstream server returned an invalid response. Response: {content}";
                    isNetworkLike = true;
                    break;
                case 503:
                    message = $"HTTP 503 Service Unavailable: The {providerName} API is at capacity. If using Flex tier, try again later or switch to Standard tier. Response: {content}";
                    isNetworkLike = true;
                    break;
                case 504:
                    message = $"HTTP 504 Gateway Timeout: {providerName} upstream timeout. The server took too long to respond. Try increasing the HTTP request timeout in SmartHopper settings. Response: {content}";
                    isNetworkLike = true;
                    break;
                default:
                    message = $"HTTP {statusCode} {reasonPhrase}: {content}";
                    // Treat any unspecified 5xx as network-like; everything else as provider error.
                    isNetworkLike = statusCode >= 500 && statusCode < 600;
                    break;
            }

            return (message, isNetworkLike);
        }

        /// <summary>
        /// Makes an HTTP request to the specified endpoint with authentication.
        /// </summary>
        /// <param name="request">The request to make.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The HTTP response content as a type T.</returns>
        private async Task<IAIReturn> CallApi(AIRequestCall request, CancellationToken cancellationToken = default)
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
                // Apply timeout from request (should be resolved by RequestTimeoutPolicy).
                // If somehow still null, fall back to the shared default so all layers stay aligned.
                try
                {
                    int seconds = request?.TimeoutSeconds ?? TimeoutDefaults.DefaultTimeoutSeconds;
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
                else if (auth == "x-goog-api-key")
                {
                    if (string.IsNullOrWhiteSpace(apiKey))
                    {
                        throw new InvalidOperationException($"{this.Name} API key is not configured or is invalid.");
                    }

                    httpClient.DefaultRequestHeaders.Remove("x-goog-api-key");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-goog-api-key", apiKey);
                }
                else
                {
                    throw new NotSupportedException($"Authentication method '{authentication}' is not supported. Supported: 'none', 'bearer', 'x-api-key', 'x-goog-api-key'.");
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
                            response = await httpClient.GetAsync(fullUri, cancellationToken).ConfigureAwait(false);
                            break;
                        case "POST":
                            var postContent = !string.IsNullOrEmpty(requestBody)
                                ? new StringContent(requestBody, Encoding.UTF8, contentType)
                                : null;
                            response = await httpClient.PostAsync(fullUri, postContent, cancellationToken).ConfigureAwait(false);
                            break;
                        case "DELETE":
                            response = await httpClient.DeleteAsync(fullUri, cancellationToken).ConfigureAwait(false);
                            break;
                        case "PATCH":
                            var patchContent = !string.IsNullOrEmpty(requestBody)
                                ? new StringContent(requestBody, Encoding.UTF8, contentType)
                                : null;
                            response = await httpClient.PatchAsync(fullUri, patchContent, cancellationToken).ConfigureAwait(false);
                            break;
                        default:
                            throw new NotSupportedException($"HTTP method '{httpMethod}' is not supported. Supported methods: GET, POST, DELETE, PATCH");
                    }

                    var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    Debug.WriteLine($"[{this.Name}] Call - Response status: {response.StatusCode}");

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[{this.Name}] Call - Error response: {content}");

                        // Create AIReturn with structured error instead of throwing
                        var errorReturn = new AIReturn();
                        var (errorMessage, isNetworkLike) = ClassifyHttpError((int)response.StatusCode, response.StatusCode.ToString(), content, this.Name);

                        if (isNetworkLike)
                        {
                            errorReturn.CreateNetworkError(errorMessage, request);
                        }
                        else
                        {
                            errorReturn.CreateProviderError(errorMessage, request);
                        }

                        errorReturn.Status = AICallStatus.Finished;

                        return errorReturn;
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

                        // Provide a more useful error when the API returns non-JSON content (e.g., HTML error pages)
                        var preview = content?.Length > 200 ? content.Substring(0, 200) + "..." : content;
                        if (!string.IsNullOrEmpty(content) && content.TrimStart().StartsWith("<", StringComparison.Ordinal))
                        {
                            throw new Exception(
                                $"The {this.Name} API returned an HTML response instead of JSON. " +
                                $"This usually indicates a server error, proxy issue, or Cloudflare challenge. " +
                                $"Response preview: {preview}");
                        }

                        throw new Exception(
                            $"The {this.Name} API returned invalid JSON. Response preview: {preview}", ex);
                    }

                    var aiReturn = new AIReturn();
                    aiReturn.CreateSuccess(
                        raw: rawJObject,
                        request: request);

                    return aiReturn;
                }
                catch (TaskCanceledException ex)
                {
                    Debug.WriteLine($"[{this.Name}] Call - TaskCanceledException (Timeout): {ex.Message}");
                    var errorReturn = new AIReturn();
                    errorReturn.CreateProviderError($"HTTP Request Timeout: The request to {this.Name} took too long (exceeded {request?.TimeoutSeconds ?? 600} seconds). Try increasing the HTTP timeout in Settings.", request);
                    errorReturn.Status = AICallStatus.Finished;
                    return errorReturn;
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

        /// <inheritdoc/>
        public virtual IEnumerable<AIExtraDescriptor> GetExtraDescriptors()
        {
            return Array.Empty<AIExtraDescriptor>();
        }
    }
}
