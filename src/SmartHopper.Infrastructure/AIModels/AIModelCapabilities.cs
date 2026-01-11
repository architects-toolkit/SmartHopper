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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartHopper.Infrastructure.AIModels
{
    /// <summary>
    /// Represents the capabilities and metadata of a specific AI model.
    /// </summary>
    public class AIModelCapabilities
    {
        /// <summary>
        /// Gets or sets the AI provider name (e.g., "openai", "anthropic").
        /// </summary>
        public string Provider { get; set; } = "";

        /// <summary>
        /// Gets or sets the model name (e.g., "gpt-4", "claude-3-opus").
        /// </summary>
        public string Model { get; set; } = "";

        /// <summary>
        /// Gets or sets the capabilities supported by this model.
        /// </summary>
        public AICapability Capabilities { get; set; } = AICapability.None;

        /// <summary>
        /// Gets or sets the capabilities for which this model is the default.
        /// If a model is marked as default for Text2Text, it will be returned as the default
        /// when requesting a model with Text2Text capabilities for this provider.
        /// </summary>
        public AICapability Default { get; set; } = AICapability.None;

        /// <summary>
        /// Indicates the model has been verified to work end-to-end in SmartHopper.
        /// </summary>
        public bool Verified { get; set; }

        /// <summary>
        /// Whether the provider supports streaming with this model.
        /// </summary>
        public bool SupportsStreaming { get; set; }

        /// <summary>
        /// Whether the provider supports prompt caching with this model.
        /// </summary>
        public bool SupportsPromptCaching { get; set; }

        /// <summary>
        /// Optional ranking to break ties when multiple models match; higher is preferred.
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// Indicates whether this model is deprecated.
        /// </summary>
        public bool Deprecated { get; set; }

        /// <summary>
        /// Alternative names that should resolve to this model.
        /// </summary>
        public List<string> Aliases { get; set; } = new List<string>();

        /// <summary>
        /// Provider-defined cache key strategy name or hint (optional).
        /// </summary>
        public string CacheKeyStrategy { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the maximum context window size in tokens for this model.
        /// When null, context limit is unknown and automatic summarization at threshold is skipped.
        /// </summary>
        public int? ContextLimit { get; set; }

        /// <summary>
        /// List of AI tool names for which this model is discouraged.
        /// When a component uses any of these tools, a "not recommended" badge will be displayed.
        /// </summary>
        public List<string> DiscouragedForTools { get; set; } = new List<string>();

        /// <summary>
        /// Checks if this model supports a specific capability.
        /// </summary>
        /// <param name="capability">The capability to check for.</param>
        /// <returns>True if the capability is supported.</returns>
        public bool HasCapability(AICapability capability)
        {
            if (capability == AICapability.None)
            {
                return true;
            }

            return (this.Capabilities & capability) == capability;
        }

        /// <summary>
        /// Gets a unique key for this model.
        /// </summary>
        /// <returns>A string key in the format "provider.model".</returns>
        public string GetKey()
        {
            return $"{this.Provider.ToLowerInvariant()}.{this.Model.ToLowerInvariant()}";
        }

        /// <summary>
        /// Checks if this model is discouraged for any of the specified tools.
        /// </summary>
        /// <param name="toolNames">List of tool names to check against.</param>
        /// <returns>True if any of the specified tools are in the discouraged list.</returns>
        public bool IsDiscouragedForAnyTool(IEnumerable<string> toolNames)
        {
            if (toolNames == null || this.DiscouragedForTools == null || this.DiscouragedForTools.Count == 0)
            {
                return false;
            }

            return toolNames.Any(t => this.DiscouragedForTools.Any(d =>
                string.Equals(d, t, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
