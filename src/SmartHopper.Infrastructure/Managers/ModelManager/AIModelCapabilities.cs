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
using SmartHopper.Infrastructure.Managers.AIProviders;
using SmartHopper.Infrastructure.Models;

namespace SmartHopper.Infrastructure.Managers.ModelManager
{
    /// <summary>
    /// Represents the capabilities and metadata of a specific AI model.
    /// </summary>
    public class AIModelCapabilities
    {
        /// <summary>
        /// The AI provider name (e.g., "openai", "anthropic").
        /// </summary>
        public string Provider { get; set; } = "";

        /// <summary>
        /// The model name (e.g., "gpt-4", "claude-3-opus").
        /// </summary>
        public string Model { get; set; } = "";

        /// <summary>
        /// The capabilities supported by this model.
        /// </summary>
        public AIModelCapability Capabilities { get; set; } = AIModelCapability.None;

        /// <summary>
        /// Checks if this model supports all the specified capabilities.
        /// </summary>
        /// <param name="requiredCapabilities">The capabilities to check for.</param>
        /// <returns>True if all capabilities are supported.</returns>
        public bool HasAllCapabilities(params AIModelCapability[] requiredCapabilities)
        {
            if (requiredCapabilities == null || requiredCapabilities.Length == 0)
                return true;

            foreach (var required in requiredCapabilities)
            {
                if (!HasCapability(required))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if this model supports a specific capability.
        /// </summary>
        /// <param name="capability">The capability to check for.</param>
        /// <returns>True if the capability is supported.</returns>
        public bool HasCapability(AIModelCapability capability)
        {
            return (Capabilities & capability) == capability;
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