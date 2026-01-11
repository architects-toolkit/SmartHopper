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

using Newtonsoft.Json;

namespace SmartHopper.Infrastructure.Settings
{
    /// <summary>
    /// Settings for SmartHopper Assistant features including CanvasButton behavior, greeting generation, and provider/model selection.
    /// </summary>
    public class SmartHopperAssistantSettings
    {
        /// <summary>
        /// Gets or sets the AI provider to use for SmartHopper Assistant features.
        /// </summary>
        [JsonProperty]
        public string AssistantProvider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the AI model to use for SmartHopper Assistant features.
        /// </summary>
        [JsonProperty]
        public string AssistantModel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the SmartHopper Assistant canvas button is enabled.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        [JsonProperty]
        public bool EnableCanvasButton { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether AI-generated greetings are enabled in chat.
        /// </summary>
        [JsonProperty]
        public bool EnableAIGreeting { get; set; } = true;
    }
}
