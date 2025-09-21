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
using System.Security.Cryptography;
using System.Text;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Metrics;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Represents an AI-generated image result with associated metadata.
    /// Used as the Result type for AIInteractionImage in image generation operations.
    /// </summary>
    public class AIInteractionImage : AIInteractionBase, IAIKeyedInteraction, IAIRenderInteraction
    {
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
        /// Structured runtime messages associated with this image interaction.
        /// Used to propagate warnings, infos, or provider notes alongside the result.
        /// </summary>
        public List<AIRuntimeMessage> Messages { get; set; } = new List<AIRuntimeMessage>();

        /// <summary>
        /// Returns a string representation of the AIInteractionImage.
        /// </summary>
        /// <returns>A formatted string containing image metadata.</returns>
        public override string ToString()
        {
            return $"AIInteractionImage ({this.ImageSize}) generated from '{this.OriginalPrompt.Substring(0, Math.Min(50, OriginalPrompt.Length))}...'";
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

        /// <summary>
        /// Returns a stable stream grouping key for this image interaction. Uses URL when available;
        /// otherwise a short hash of ImageData; falls back to the original prompt.
        /// </summary>
        /// <returns>Stream group key.</returns>
        public string GetStreamKey()
        {
            if (!string.IsNullOrWhiteSpace(this.TurnId))
            {
                return $"turn:{this.TurnId}";
            }

            var id = !string.IsNullOrEmpty(this.ImageUrl)
                ? this.ImageUrl
                : (!string.IsNullOrEmpty(this.ImageData) ? ComputeShortHash(this.ImageData) : (this.OriginalPrompt ?? string.Empty).Trim());
            return $"image:{id}";
        }

        /// <summary>
        /// Returns a stable de-duplication key for this image interaction. Includes URL/hash and core options
        /// (size, quality, style) to distinguish similar images.
        /// </summary>
        /// <returns>De-duplication key.</returns>
        public string GetDedupKey()
        {
            if (!string.IsNullOrWhiteSpace(this.TurnId))
            {
                return $"turn:{this.TurnId}";
            }

            var id = !string.IsNullOrEmpty(this.ImageUrl)
                ? this.ImageUrl
                : (!string.IsNullOrEmpty(this.ImageData) ? ComputeShortHash(this.ImageData) : (this.OriginalPrompt ?? string.Empty).Trim());
            var size = this.ImageSize ?? string.Empty;
            var quality = this.ImageQuality ?? string.Empty;
            var style = this.ImageStyle ?? string.Empty;
            return $"image:{id}:{size}:{quality}:{style}";
        }

        /// <summary>
        /// Computes a short (16 hex chars) SHA256-based hash for stable keys.
        /// </summary>
        /// <param name="value">Input string to hash.</param>
        /// <returns>Lowercase hex substring of the hash.</returns>
        private static string ComputeShortHash(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant().Substring(0, 16);
            }
        }

        /// <summary>
        /// Gets the CSS role class to use when rendering this interaction. Defaults to assistant.
        /// </summary>
        public string GetRoleClassForRender()
        {
            var role = (this.Agent == 0 ? AIAgent.Assistant : this.Agent).ToString().ToLower();
            return role;
        }

        /// <summary>
        /// Gets the display name for rendering (header label).
        /// </summary>
        public string GetDisplayNameForRender()
        {
            var agent = this.Agent == 0 ? AIAgent.Assistant : this.Agent;
            return agent.ToDescription();
        }

        /// <summary>
        /// Gets the raw markdown content to render for this interaction. Embeds the image.
        /// </summary>
        public string GetRawContentForRender()
        {
            if (!string.IsNullOrWhiteSpace(this.ImageUrl))
            {
                return $"![generated image]({this.ImageUrl})";
            }
            if (!string.IsNullOrWhiteSpace(this.ImageData))
            {
                // Assume PNG if unknown; browsers will render data URIs
                return $"![generated image](data:image/png;base64,{this.ImageData})";
            }
            return this.ToString();
        }

        /// <summary>
        /// Images do not include reasoning by default.
        /// </summary>
        public string GetRawReasoningForRender()
        {
            return string.Empty;
        }
    }
}
