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
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Represents an AI-generated text result with associated metadata.
    /// Used as the Result type for AIInteractionText in text generation operations.
    /// </summary>
    public class AIInteractionToolResult : AIInteractionToolCall, IAIInteraction
    {
        /// <summary>
        /// Gets the agent of the interaction.
        /// </summary>
        public override AIAgent Agent { get; } = AIAgent.ToolResult;

        /// <summary>
        /// Gets or sets the result of the tool call.
        /// </summary>
        public JObject Result { get; set; }

        /// <summary>
        /// Returns a string representation of the AIInteractionToolResult.
        /// </summary>
        /// <returns>A formatted string containing tool result metadata.</returns>
        public override string ToString()
        {
            var result = "Tool result";

            if(!string.IsNullOrEmpty(Name))
            {
                result += $" from {Name}";
            }

            if(!string.IsNullOrEmpty(Result))
            {
                result += $":\n{JsonConvert.SerializeObject(Result, Formatting.Indented)}";
            }

            return result;
        }
    }
}
