/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Newtonsoft.Json;

namespace SmartHopper.Infrastructure.Settings
{
    /// <summary>
    /// Settings for SmartHopper Assistant features including CanvasButton behavior, greeting generation, and provider/model selection
    /// </summary>
    public class SmartHopperAssistantSettings
    {
        /// <summary>
        /// Gets or sets whether AI-generated greetings are enabled in chat
        /// </summary>
        [JsonProperty]
        public bool EnableAIGreeting { get; set; } = true;

        /// <summary>
        /// Gets or sets the AI provider to use for SmartHopper Assistant features
        /// </summary>
        [JsonProperty]
        public string AssistantProvider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the AI model to use for SmartHopper Assistant features
        /// </summary>
        [JsonProperty]
        public string AssistantModel { get; set; } = string.Empty;
    }
}
