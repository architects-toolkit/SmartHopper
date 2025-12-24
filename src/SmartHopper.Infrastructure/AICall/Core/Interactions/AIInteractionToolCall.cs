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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Metrics;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Represents an AI-generated tool call with associated metadata.
    /// </summary>
    public class AIInteractionToolCall : AIInteractionBase, IAIKeyedInteraction, IAIRenderInteraction
    {
        /// <inheritdoc/>
        public override AIAgent Agent { get; set; } = AIAgent.ToolCall;

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
        /// Gets or sets the reasoning content associated with this tool call.
        /// Used by providers like DeepSeek that include reasoning_content with tool calls.
        /// </summary>
        public string Reasoning { get; set; }

        /// <summary>
        /// Returns a string representation of the AIInteractionToolCall.
        /// </summary>
        /// <returns>A formatted string containing tool call metadata.</returns>
        public override string ToString()
        {
            var result = "Calling tool";

            if (!string.IsNullOrEmpty(this.Id))
            {
                result += $" ({this.Id})";
            }

            if (!string.IsNullOrEmpty(this.Name))
            {
                result += $" {this.Name}";
            }

            if (this.Arguments != null && this.Arguments.HasValues)
            {
                result += $" with the following arguments:\n{JsonConvert.SerializeObject(this.Arguments, Formatting.Indented)}";
            }

            return result;
        }

        /// <summary>
        /// Returns a stable stream grouping key for this interaction. Defaults to tool.call:{IdOrName}.
        /// </summary>
        /// <returns>Stream group key.</returns>
        public virtual string GetStreamKey()
        {
            var id = !string.IsNullOrEmpty(this.Id) ? this.Id : (this.Name ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(this.TurnId))
            {
                return $"turn:{this.TurnId}:tool.call:{id}";
            }

            return $"tool.call:{id}";
        }

        /// <summary>
        /// Returns a stable de-duplication key for this interaction. Includes arguments hash to disambiguate.
        /// </summary>
        /// <returns>De-duplication key.</returns>
        public virtual string GetDedupKey()
        {
            var id = !string.IsNullOrEmpty(this.Id) ? this.Id : (this.Name ?? string.Empty);
            var argsStr = this.Arguments != null ? this.Arguments.ToString(Newtonsoft.Json.Formatting.None) : string.Empty;
            var argsHash = !string.IsNullOrEmpty(argsStr) ? SmartHopper.Infrastructure.AICall.Utilities.HashUtility.ComputeShortHash(argsStr) : "none";

            if (!string.IsNullOrWhiteSpace(this.TurnId))
            {
                return $"turn:{this.TurnId}:tool.call:{id}:{argsHash}";
            }

            return $"tool.call:{id}:{argsHash}";
        }

        /// <summary>
        /// Gets the CSS role class to use when rendering this interaction.
        /// </summary>
        public virtual string GetRoleClassForRender()
        {
            return "tool";
        }

        /// <summary>
        /// Gets the display name for rendering (header label).
        /// </summary>
        public virtual string GetDisplayNameForRender()
        {
            return string.IsNullOrWhiteSpace(this.Name) ? "Tool Call" : $"Tool Call: {this.Name}";
        }

        /// <summary>
        /// Gets the raw markdown content to render for this interaction (pretty-printed JSON args).
        /// </summary>
        public virtual string GetRawContentForRender()
        {
            return this.Arguments != null && this.Arguments.HasValues ? JsonConvert.SerializeObject(this.Arguments, Formatting.Indented) : string.Empty;
        }

        /// <summary>
        /// Gets the reasoning content associated with this tool call, if any.
        /// </summary>
        public virtual string GetRawReasoningForRender()
        {
            return this.Reasoning ?? string.Empty;
        }
    }
}
