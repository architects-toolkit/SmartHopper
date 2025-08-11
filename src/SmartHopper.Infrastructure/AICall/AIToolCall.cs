/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Represents a tool call made by an AI model.
    /// </summary>
    public class AIToolCall : AIRequestBase
    {
        /// <summary>
        /// Gets or sets the ID of the tool call.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the tool being called.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the arguments passed to the tool as a JSON object.
        /// </summary>
        public JObject Arguments { get; set; } = new JObject();

        /// <summary>
        /// Gets or sets the JSON object containing the result of the tool call.
        /// </summary>
        public JObject Result { get; set; } = new JObject();

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
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets a value indicating whether the tool call is valid.
        /// </summary>
        /// <returns>A tuple containing a boolean indicating whether the tool call is valid and a list of error messages.</returns>
        public override (bool IsValid, List<string> Errors) IsValid()
        {
            var messages = new List<string>();
            bool hasErrors = false;

            var (baseValid, baseErrors) = base.IsValid();
            if (!baseValid)
            {
                messages.AddRange(baseErrors);
                hasErrors = true;
            }

            if (string.IsNullOrEmpty(this.Name))
            {
                messages.Add("Tool name is required");
                hasErrors = true;
            }

            if (this.Arguments == null)
            {
                messages.Add("Arguments are required");
                hasErrors = true;
            }

            return (!hasErrors, messages);
        }

        /// <summary>
        /// Replaces the reuse count of the metrics.
        /// </summary>
        /// <param name="reuseCount">The new reuse count.</param>
        public void ReplaceReuseCount(int reuseCount)
        {
            this.Metrics.ReuseCount = reuseCount;
        }
    }
}
