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
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Utilities;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Represents an AI image interaction — either a generated image result or an image input for vision.
    /// </summary>
    public class AIInteractionImage : AIInteractionBase, IAIKeyedInteraction, IAIRenderInteraction
    {
        /// <summary>
        /// Gets or sets the URL of the generated image.
        /// </summary>
        public Uri ImageUrl { get; set; }

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
        /// Gets or sets the aspect ratio of the generated image (e.g., "1:1", "16:9").
        /// Provider-specific; used by Gemini image generation.
        /// </summary>
        public string AspectRatio { get; set; }

        /// <summary>
        /// Gets or sets the MIME type of the image (e.g., "image/png", "image/jpeg").
        /// Used primarily for vision input to indicate the format of base64-encoded image data.
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// Gets or sets the structured runtime messages associated with this image interaction.
        /// Used to propagate warnings, infos, or provider notes alongside the result.
        /// </summary>
        public List<SHRuntimeMessage> Messages { get; set; } = new List<SHRuntimeMessage>();

        /// <summary>
        /// Returns a string representation of the AIInteractionImage.
        /// </summary>
        /// <returns>A formatted string containing image metadata.</returns>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(this.OriginalPrompt))
            {
                return $"AIInteractionImage ({this.ImageSize}) [vision input]";
            }

            var preview = this.OriginalPrompt.Length <= 50
                ? this.OriginalPrompt
                : this.OriginalPrompt.Substring(0, 50) + "...";
            return $"AIInteractionImage ({this.ImageSize}) generated from '{preview}'";
        }

        /// <summary>
        /// Creates a vision input interaction from a URL.
        /// Use this when sending an image to an AI model for understanding/description.
        /// </summary>
        /// <param name="imageUrl">The URL of the image to analyze.</param>
        public void CreateVisionInput(Uri imageUrl)
        {
            this.ImageUrl = imageUrl ?? throw new ArgumentNullException(nameof(imageUrl));
        }

        /// <summary>
        /// Creates a vision input interaction from a URL string.
        /// Use this when sending an image to an AI model for understanding/description.
        /// </summary>
        /// <param name="imageUrl">The URL of the image to analyze.</param>
        public void CreateVisionInput(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                throw new ArgumentException("Image URL cannot be null or empty.", nameof(imageUrl));
            }

            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
            {
                this.ImageUrl = uri;
            }
            else
            {
                throw new ArgumentException($"Invalid image URL: {imageUrl}", nameof(imageUrl));
            }
        }

        /// <summary>
        /// Creates a vision input interaction from base64-encoded image data.
        /// Use this when sending an image to an AI model for understanding/description.
        /// </summary>
        /// <param name="base64Data">The base64-encoded image data (without data URI prefix).</param>
        /// <param name="mimeType">The MIME type of the image (e.g., "image/png", "image/jpeg"). Defaults to "image/png".</param>
        public void CreateVisionInputFromBase64(string base64Data, string mimeType = "image/png")
        {
            if (string.IsNullOrWhiteSpace(base64Data))
            {
                throw new ArgumentException("Base64 image data cannot be null or empty.", nameof(base64Data));
            }

            this.ImageData = base64Data;
            this.MimeType = mimeType ?? "image/png";
        }

        /// <summary>
        /// Creates a request for image generation.
        /// </summary>
        /// <param name="prompt">The prompt to generate the image from.</param>
        /// <param name="size">The size of the image to generate (e.g., "1024x1024").</param>
        /// <param name="quality">The quality setting used for image generation (e.g., "standard", "hd").</param>
        /// <param name="style">The style setting used for image generation (e.g., "vivid", "natural").</param>
        /// <param name="aspectRatio">The aspect ratio of the image to generate (e.g., "1:1", "16:9").</param>
        public void CreateRequest(string prompt, string size = null, string quality = null, string style = null, string aspectRatio = null)
        {
            this.OriginalPrompt = prompt;
            this.ImageSize = size ?? this.ImageSize;
            this.ImageQuality = quality ?? this.ImageQuality;
            this.ImageStyle = style ?? this.ImageStyle;
            this.AspectRatio = aspectRatio ?? this.AspectRatio;
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
                if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
                {
                    this.SetResult(uri, imageData, revisedPrompt);
                    return;
                }

                if (imageData == null)
                {
                    throw new ArgumentException(
                        $"imageUrl '{imageUrl}' is not a valid absolute URI and no imageData was provided.",
                        nameof(imageUrl));
                }
            }

            // Handle case where only imageData is provided (no URL, or URL was invalid but data is present)
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
        /// Sets the result of the image generation using strong types. imageUrl or imageData must be provided.
        /// </summary>
        /// <param name="imageUrl">The URL of the generated image.</param>
        /// <param name="imageData">The raw image data (base64 encoded, if available).</param>
        /// <param name="revisedPrompt">The revised prompt used by the AI model.</param>
        public void SetResult(Uri imageUrl = null, string imageData = null, string revisedPrompt = null)
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
            var id = this.ImageUrl != null
                ? this.ImageUrl.ToString()
                : (!string.IsNullOrEmpty(this.ImageData) ? HashUtility.ComputeShortHash(this.ImageData) : (this.OriginalPrompt ?? string.Empty).Trim());

            if (!string.IsNullOrWhiteSpace(this.TurnId))
            {
                return $"turn:{this.TurnId}:image:{id}";
            }

            return $"image:{id}";
        }

        /// <summary>
        /// Returns a stable de-duplication key for this image interaction. Includes URL/hash and core options
        /// (size, quality, style) to distinguish similar images.
        /// </summary>
        /// <returns>De-duplication key.</returns>
        public string GetDedupKey()
        {
            var size = this.ImageSize ?? string.Empty;
            var quality = this.ImageQuality ?? string.Empty;
            var style = this.ImageStyle ?? string.Empty;
            return $"{this.GetStreamKey()}:{size}:{quality}:{style}";
        }

        /// <summary>
        /// Gets the CSS role class to use when rendering this interaction. Defaults to assistant.
        /// </summary>
        /// <returns>The role string used as a CSS class for rendering.</returns>
        public string GetRoleClassForRender()
        {
            var role = (this.Agent == 0 ? AIAgent.Assistant : this.Agent).ToString().ToLowerInvariant();
            return role;
        }

        /// <summary>
        /// Gets the display name for rendering (header label).
        /// </summary>
        /// <returns>The human-readable agent display name.</returns>
        public string GetDisplayNameForRender()
        {
            var agent = this.Agent == 0 ? AIAgent.Assistant : this.Agent;
            return agent.ToDescription();
        }

        /// <summary>
        /// Gets the raw markdown content to render for this interaction. Embeds the image.
        /// </summary>
        /// <returns>A markdown string representing the image to render.</returns>
        public string GetRawContentForRender()
        {
            if (this.ImageUrl != null)
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
        /// <returns>An empty string.</returns>
        public string GetRawReasoningForRender()
        {
            return string.Empty;
        }
    }
}
