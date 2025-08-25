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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;


namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Represents an AI-generated text result with associated metadata.
    /// Used as the Result type for AIInteractionTool in tool operations.
    /// </summary>
    public class AIInteractionToolResult : AIInteractionToolCall, IAIInteraction
    {
        /// <inheritdoc/>
        public override AIAgent Agent { get; set; } = AIAgent.ToolResult;

        /// <summary>
        /// Gets or sets the result of the tool call.
        /// </summary>
        public JObject Result { get; set; }

        /// <summary>
        /// Gets or sets the structured runtime messages produced while generating this tool result.
        /// These are propagated from inner AI calls to improve diagnostics and visibility.
        /// </summary>
        public List<AIRuntimeMessage> Messages { get; set; } = new List<AIRuntimeMessage>();

        /// <summary>
        /// Returns a string representation of the AIInteractionToolResult.
        /// </summary>
        /// <returns>A formatted string containing tool result metadata.</returns>
        public override string ToString()
        {
            var result = "Tool result";

            if (!string.IsNullOrEmpty(Name))
            {
                result += $" from {Name}";
            }

            if (Result != null && Result.HasValues)
            {
                result += $":\n{JsonConvert.SerializeObject(Result, Formatting.Indented)}";
            }

            return result;
        }
    }
}
