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
using SmartHopper.Infrastructure.AICodec;

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Represents an AI-generated image result with associated metadata.
    /// Used as the Result type for AIInteractionImage in image generation operations.
    /// </summary>
    public class AIInteractionImage : IAIInteraction
    {
        /// <summary>
        /// Gets or sets the agent of the interaction.
        /// </summary>
        required public AIAgent Agent { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the interaction.
        /// </summary>
        public DateTime Time { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the list of tool calls associated with this interaction.
        /// </summary>
        public List<AIToolCall> ToolCalls { get; set; } = new List<AIToolCall>();

        /// <summary>
        /// Gets or sets the URL of the generated image.
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the raw image data (base64 encoded, if available).
        /// </summary>
        public string ImageData { get; set; }

        /// <summary>
        /// Gets or sets the revised prompt used by the AI model.
        /// May differ from the original prompt due to AI model optimizations.
        /// </summary>
        public string RevisedPrompt { get; set; }

        /// <summary>
        /// Gets or sets the original prompt provided by the user.
        /// </summary>
        public string OriginalPrompt { get; set; }

        /// <summary>
        /// Gets or sets the size of the generated image (e.g., "1024x1024").
        /// </summary>
        public string ImageSize { get; set; } = "1024x1024";

        /// <summary>
        /// Gets or sets the quality setting used for image generation (e.g., "standard", "hd").
        /// </summary>
        public string ImageQuality { get; set; } = "standard";

        /// <summary>
        /// Gets or sets the style setting used for image generation (e.g., "vivid", "natural").
        /// </summary>
        public string ImageStyle { get; set; } = "vivid";

        /// <summary>
        /// Returns a string representation of the AIInteractionImage.
        /// </summary>
        /// <returns>A formatted string containing image metadata.</returns>
        public override string ToString()
        {
            return $"AIInteractionImage ({ImageSize}) generated from '{OriginalPrompt.Substring(0, Math.Min(50, OriginalPrompt.Length))}...'";
        }

        /// <summary>
        /// Creates a request for image generation.
        /// </summary>
        /// <param name="prompt">The prompt to generate the image from.</param>
        /// <param name="size">The size of the image to generate (e.g., "1024x1024").</param>
        /// <param name="quality">The quality setting used for image generation (e.g., "standard", "hd").</param>
        /// <param name="style">The style setting used for image generation (e.g., "vivid", "natural").</param>
        public void CreateRequest(string prompt, string size = null, string quality = null, string style = null)
        {
            this.OriginalPrompt = prompt;
            this.ImageSize = size ?? this.ImageSize;
            this.ImageQuality = quality ?? this.ImageQuality;
            this.ImageStyle = style ?? this.ImageStyle;
        }

        /// <summary>
        /// Sets the result of the image generation. imageUrl or imageData must be provided.
        /// </summary>
        /// <param name="imageUrl">The URL of the generated image.</param>
        /// <param name="imageData">The raw image data (base64 encoded, if available).</param>
        /// <param name="revisedPrompt">The revised prompt used by the AI model.</param>
        public void SetResult(string imageUrl = null, string imageData = null, string revisedPrompt = null)
        {
            if (imageUrl == null && imageData == null)
            {
                throw new ArgumentNullException("imageUrl or imageData must be provided");
            }

            if (imageUrl != null)
            {
                this.ImageUrl = imageUrl;
            }

            if (imageData != null)
            {
                this.ImageData = imageData;
            }

            if (revisedPrompt != null)
            {
                this.RevisedPrompt = revisedPrompt;
            }
        }
    }
}
