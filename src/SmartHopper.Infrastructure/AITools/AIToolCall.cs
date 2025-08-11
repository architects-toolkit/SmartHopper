/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Infrastructure.AITools
{
    /// <summary>
    /// Represents a tool call made by an AI model.
    /// </summary>
    public class AIToolCall
    {
        private JObject _arguments = null;

        /// <summary>
        /// Gets or sets the ID of the tool call.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the tool being called.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the provider to use for the tool call.
        /// </summary>
        public IAIProvider Provider { get; set; }

        /// <summary>
        /// Gets or sets the model to use for the tool call.
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// Gets or sets the arguments passed to the tool as a JSON object.
        /// </summary>
        public JObject Arguments { get => _arguments; set 
        {
            this.UpdateArguments(value);
        } }

        /// <summary>
        /// Gets or sets the JSON object containing the result of the tool call.
        /// </summary>
        public JObject Result { get; set; }

        /// <summary>
        /// Gets or sets the metrics about this call.
        /// </summary>
        public AIMetrics Metrics { get; set; } = new AIMetrics();

        /// <summary>
        /// Gets a value indicating whether the tool call was executed.
        /// </summary>
        public bool Executed { get => this.Result != null; }

        /// <summary>
        /// Gets a value indicating whether the tool call was successful.
        /// </summary>
        public bool Success { get => string.IsNullOrEmpty(this.ErrorMessage); }

        /// <summary>
        /// Gets or sets the error message if any occurred during the tool call.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets a value indicating whether the tool call is valid.
        /// </summary>
        public (bool IsValid, List<string> Errors) IsValid()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(this.Name))
            {
                errors.Add("Tool name is required");
            }

            if (this.Arguments == null)
            {
                errors.Add("Arguments are required");
            }

            // Validate model capabilities
            if (string.IsNullOrEmpty(this.Model))
            {
                errors.Add("Model is required");
            }
            else
            {
                if (this.Provider == null)
                {
                    errors.Add($"Unknown provider '{this.Provider}'");
                }
                else
                {
                    var modelCapabilities = this.Provider.GetCapabilities(this.Model);
                    var toolRequiredCapabilities = ToolManager.Instance.GetTool(this.Name).RequiredCapabilities;
                    if (!modelCapabilities.HasFlag(toolRequiredCapabilities))
                    {
                        errors.Add($"Model '{this.Model}' does not support tool '{this.Name}'");
                    }
                }
            }

            return (errors.Count == 0, errors);
        }

        private void UpdateArguments(JObject arguments)
        {
            if (this.Model != null)
            {
                arguments["model"] = this.Model;
            }
            if (this.Provider != null)
            {
                arguments["provider"] = this.Provider.Name;
            }
            this._arguments = arguments;
        }
    }
}
