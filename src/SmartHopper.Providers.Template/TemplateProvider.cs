/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Newtonsoft.Json.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace SmartHopper.Providers.Template
{
    /// <summary>
    /// Template AI provider implementation. This class serves as a guide for implementing new AI providers.
    /// 
    /// To create a new provider:
    /// 1. Create a new project named SmartHopper.Providers.YourProviderName
    /// 2. Copy this template and rename all "Template" references to your provider name
    /// 3. Implement the required methods with your provider-specific logic
    /// 4. Create a factory class that implements IAIProviderFactory
    /// 5. Set IsEnabled to true when your provider is ready for use
    /// </summary>
    public class TemplateProvider : AIProvider
    {
        // Static instance for singleton pattern
        private static readonly Lazy<TemplateProvider> _instance = new Lazy<TemplateProvider>(() => new TemplateProvider());
        
        /// <summary>
        /// Gets the singleton instance of the provider.
        /// </summary>
        public static TemplateProvider Instance => _instance.Value;

        /// <summary>
        /// The name of the provider. This will be displayed in the UI and used for provider selection.
        /// </summary>
        public static readonly string _name = "Template";

        /// <summary>
        /// The default model to use if none is specified.
        /// </summary>
        private const string _defaultModel = "template-model";

        /// <summary>
        /// Private constructor to enforce singleton pattern.
        /// </summary>
        private TemplateProvider()
        {
            // Initialization code if needed
        }

        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        public override string Name => _name;

        /// <summary>
        /// Gets the default model for this provider.
        /// </summary>
        public override string DefaultModel => _defaultModel;

        /// <summary>
        /// Gets whether this provider is enabled and should be available for use.
        /// Set this to false for template or experimental providers that shouldn't be used in production.
        /// </summary>
        public override bool IsEnabled => false; // Set to true when your provider is ready for use

        /// <summary>
        /// Gets the provider's icon.
        /// </summary>
        public override Image Icon
        {
            get
            {
                var iconBytes = Properties.Resources.template_icon;
                using (var ms = new MemoryStream(iconBytes))
                {
                    return new Bitmap(ms);
                }
            }
        }

        /// <summary>
        /// Gets the setting descriptors for this provider.
        /// These describe the settings that can be configured in the UI.
        /// </summary>
        /// <returns>A collection of setting descriptors.</returns>
        public override IEnumerable<SettingDescriptor> GetSettingDescriptors()
        {
            // Define the settings that your provider requires
            return new[]
            {
                new SettingDescriptor
                {
                    Name = "ApiKey",
                    DisplayName = "API Key",
                    Description = "Your API key for the Template service",
                    IsSecret = true, // Set to true for sensitive data like API keys
                    Type = typeof(string)
                },
                new SettingDescriptor
                {
                    Name = "Model",
                    DisplayName = "Model",
                    Description = "The model to use for generating responses",
                    Type = typeof(string),
                    DefaultValue = _defaultModel
                },
                new SettingDescriptor
                {
                    Name = "MaxTokens",
                    DisplayName = "Max Tokens",
                    Description = "Maximum number of tokens to generate",
                    Type = typeof(int),
                    DefaultValue = 150
                }
            };
        }

        /// <summary>
        /// Validates the provided settings.
        /// </summary>
        /// <param name="settings">The settings to validate.</param>
        /// <returns>True if the settings are valid, otherwise false.</returns>
        public override bool ValidateSettings(Dictionary<string, object> settings)
        {
            // Only validate settings that are actually provided
            if (settings == null)
                return false;

            // Check API key format if present
            if (settings.TryGetValue("ApiKey", out var apiKeyObj) && apiKeyObj != null)
            {
                string apiKey = apiKeyObj.ToString();
                // Simple format validation - don't require presence, just valid format if provided
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    // Invalid format: empty key
                    return false;
                }
                // API key format is valid
            }

            // Check model format if present
            if (settings.TryGetValue("Model", out var modelObj) && modelObj != null)
            {
                string model = modelObj.ToString();
                if (string.IsNullOrWhiteSpace(model))
                {
                    // Invalid format: empty model
                    return false;
                }
            }
            
            // Check max tokens if present - must be a positive number
            if (settings.TryGetValue("MaxTokens", out var maxTokensObj) && maxTokensObj != null)
            {
                // Try to parse as integer
                if (int.TryParse(maxTokensObj.ToString(), out int maxTokens))
                {
                    if (maxTokens <= 0)
                    {
                        // Invalid format: negative or zero
                        return false;
                    }
                    // MaxTokens format is valid
                }
                else
                {
                    // Invalid format: not an integer
                    return false;
                }
            }
            
            // All provided settings have valid format
            return true;
        }

        /// <summary>
        /// Gets a response from the AI provider.
        /// </summary>
        /// <param name="messages">The messages to send to the AI provider.</param>
        /// <param name="model">The model to use, or empty for default.</param>
        /// <param name="jsonSchema">Optional JSON schema for response formatting.</param>
        /// <param name="endpoint">Optional custom endpoint URL.</param>
        /// <param name="includeToolDefinitions">Optional flag to include tool definitions in the response.</param>
        /// <returns>The AI response.</returns>
        public override async Task<AIResponse> GetResponse(JArray messages, string model, string jsonSchema = "", string endpoint = "", bool includeToolDefinitions = false)
        {
            try
            {
                // Access settings using the secure GetSetting<T> method
                string apiKey = GetSetting<string>("ApiKey");
                int maxTokens = GetSetting<int>("MaxTokens");
                
                // Verify we have an API key
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new Exception("API Key is not configured for Template provider.");
                }
                
                // This is a template implementation
                // In a real provider, you would:
                // 1. Format the messages according to the provider's API requirements
                // 2. Send an HTTP request to the provider's API
                // 3. Parse the response and return it in the standard AIResponse format

                // For the template, we'll just return a placeholder response
                await Task.Delay(500); // Simulate API call delay

                return new AIResponse
                {
                    Content = "This is a template response. Replace this with actual API integration.",
                    Model = model ?? _defaultModel,
                    FinishReason = "stop",
                    InputTokens = messages.ToString().Length / 4, // Rough estimate
                    OutputTokens = 20, // Rough estimate
                    TotalTokens = (messages.ToString().Length / 4) + 20
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TemplateProvider.GetResponse: {ex.Message}");
                throw new Exception($"Error getting response from Template provider: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the model to use for AI processing.
        /// </summary>
        /// <param name="settings">The provider settings.</param>
        /// <param name="requestedModel">The requested model, or empty for default.</param>
        /// <returns>The model to use.</returns>
        public override string GetModel(Dictionary<string, object> settings, string requestedModel = "")
        {
            // Use the requested model if provided
            if (!string.IsNullOrWhiteSpace(requestedModel))
                return requestedModel;

            // Use the model from settings if available
            string modelFromSettings = GetSetting<string>("Model");
            if (!string.IsNullOrWhiteSpace(modelFromSettings))
                return modelFromSettings;

            // Fall back to the default model
            return DefaultModel;
        }
    }
}
