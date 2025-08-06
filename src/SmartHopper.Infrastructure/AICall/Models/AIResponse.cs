/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;

namespace SmartHopper.Infrastructure.Models
{
    public class AIResponse
    {
        public string Response { get; set; }
        public string FinishReason { get; set; }
        public int InTokens { get; set; }
        public int OutTokens { get; set; }
        public double CompletionTime { get; set; }
        /// <summary>
        /// Gets or sets the list of tool calls made by the AI model.
        /// </summary>
        public List<AIToolCall> ToolCalls { get; set; } = new List<AIToolCall>();
        public string Provider { get; set; }
        public string Model { get; set; }

        /// <summary>
        /// Tracks how many times this response is reused across different data tree branches.
        /// Default is 1 (used once).
        /// </summary>
        public int ReuseCount { get; set; } = 1;

        // Image generation fields (optional, used only for image generation responses)
        /// <summary>
        /// Gets or sets the URL of the generated image (if hosted remotely).
        /// </summary>
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the base64-encoded image data (if returned directly).
        /// </summary>
        public string ImageData { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the revised prompt used by the AI for image generation (may be cleaned or enhanced).
        /// </summary>
        public string RevisedPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the original prompt provided by the user for image generation.
        /// </summary>
        public string OriginalPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the size of the generated image (e.g., "1024x1024").
        /// </summary>
        public string ImageSize { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quality setting used for image generation (e.g., "standard", "hd").
        /// </summary>
        public string ImageQuality { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the style setting used for image generation (e.g., "vivid", "natural").
        /// </summary>
        public string ImageStyle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets any error message if the generation failed.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
