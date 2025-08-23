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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.AIProviders;

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
        public bool Verified { get; set; } = false;

        /// <summary>
        /// Whether the provider supports streaming with this model.
        /// </summary>
        public bool SupportsStreaming { get; set; } = false;

        /// <summary>
        /// Whether the provider supports prompt caching with this model.
        /// </summary>
        public bool SupportsPromptCaching { get; set; } = false;

        /// <summary>
        /// Optional ranking to break ties when multiple models match; higher is preferred.
        /// </summary>
        public int Rank { get; set; } = 0;

        /// <summary>
        /// Indicates whether this model is deprecated.
        /// </summary>
        public bool Deprecated { get; set; } = false;

        /// <summary>
        /// Alternative names that should resolve to this model.
        /// </summary>
        public List<string> Aliases { get; set; } = new List<string>();

        /// <summary>
        /// Provider-defined cache key strategy name or hint (optional).
        /// </summary>
        public string CacheKeyStrategy { get; set; } = string.Empty;

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
            return $"{Provider.ToLower()}.{Model.ToLower()}";
        }
    }
}
