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
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Represents an AI-generated text result with associated metadata.
    /// Used as the Result type for AIInteractionText in text generation operations.
    /// </summary>
    public class AIInteractionToolCall : IAIInteraction
    {
        /// <summary>
        /// Gets the agent of the interaction.
        /// </summary>
        public virtual AIAgent Agent { get; } = AIAgent.ToolCall;

        /// <summary>
        /// Gets or sets the timestamp of the interaction.
        /// </summary>
        public DateTime Time { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the id of the tool call.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the tool to call.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the arguments of the tool call.
        /// </summary>
        public JObject Arguments { get; set; }

        /// <summary>
        /// Returns a string representation of the AIInteractionToolCall.
        /// </summary>
        /// <returns>A formatted string containing tool call metadata.</returns>
        public override string ToString()
        {
            var result = "Calling tool";

            if(!string.IsNullOrEmpty(Id))
            {
                result += $" ({Id})";
            }

            if(!string.IsNullOrEmpty(Name))
            {
                result += $" {Name}";
            }

            if(!string.IsNullOrEmpty(Arguments))
            {
                result += $" with the following arguments:\n{JsonConvert.SerializeObject(Arguments, Formatting.Indented)}";
            }

            return result;
        }
    }
}
