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

namespace SmartHopper.Core.Grasshopper.Utils.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Core.Types;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;

    /// <summary>
    /// Shared image processing utilities used by <c>file2md</c> and <c>web2md</c>.
    /// Handles AI image description and building the Markdown replacement for the
    /// supported image modes: <c>link</c>, <c>embed</c>, <c>describe</c>, and <c>caption</c>.
    /// </summary>
    public static class ImageProcessingService
    {
        /// <summary>
        /// Default prompt used for <c>describe</c> mode: long, thorough description.
        /// </summary>
        public const string DefaultImageDescriptionPrompt =
            "Describe this image thoroughly for someone who cannot see it. Include: the main subject and overall scene, all visible objects and their spatial arrangement, any text, numbers, labels, charts, diagrams, or data visible in the image, colors and lighting when relevant, the apparent purpose or context of the image (e.g., photograph, technical diagram, screenshot, infographic), and any other details necessary to fully convey the image content. Be precise, complete, and well-structured. Do not make assumptions. Do not suggest future actions. Stick to describing the image in a way that is useful for someone who cannot see it.";

        /// <summary>
        /// Default prompt used for <c>caption</c> and <c>embed</c> modes: short, one-sentence caption.
        /// </summary>
        public const string DefaultImageCaptionPrompt =
            "Write a concise, descriptive caption for this image in one sentence.";

        /// <summary>
        /// Returns the default prompt for the given image mode.
        /// </summary>
        /// <param name="imageMode">The image mode: <c>describe</c>, <c>caption</c>, or <c>embed</c>.</param>
        /// <returns>The default prompt string.</returns>
        public static string GetDefaultPrompt(string imageMode)
        {
            return imageMode == "describe" ? DefaultImageDescriptionPrompt : DefaultImageCaptionPrompt;
        }

        /// <summary>
        /// Builds the Markdown replacement for an image based on the selected mode.
        /// </summary>
        /// <param name="imageMode">The image mode: <c>link</c>, <c>embed</c>, <c>describe</c>, or <c>caption</c>.</param>
        /// <param name="aiText">The AI-generated caption or description when applicable.</param>
        /// <param name="altText">The original alt text or link-mode alt text.</param>
        /// <param name="url">The original image URL (used for <c>link</c> mode).</param>
        /// <param name="mimeType">The image MIME type (used for <c>embed</c> mode).</param>
        /// <param name="base64Data">The base64-encoded image data (used for <c>embed</c> mode).</param>
        /// <param name="imageId">The image identifier (used for <c>describe</c>/<c>caption</c> modes).</param>
        /// <param name="context">The image context (used for <c>describe</c>/<c>caption</c> modes).</param>
        /// <returns>A Markdown string that replaces the original image reference.</returns>
        public static string BuildMarkdownReplacement(
            string imageMode,
            string aiText,
            string altText,
            string? url,
            string? mimeType,
            string? base64Data,
            string imageId,
            string context)
        {
            return imageMode switch
            {
                "link" => $"![{altText}]({url})",
                "embed" => $"![{aiText}](data:{mimeType};base64,{base64Data})",
                "describe" or "caption" => $"**[{imageId} — {context}]**\n\n{aiText}",
                _ => $"![{altText}]({url})",
            };
        }

        /// <summary>
        /// Calls the <c>img2text</c> tool to obtain a text description or caption of an image.
        /// </summary>
        /// <param name="imageBase64">Base64-encoded image data.</param>
        /// <param name="mimeType">The image MIME type.</param>
        /// <param name="prompt">The description prompt to send to the AI.</param>
        /// <param name="sourceToolCall">The parent tool call providing provider and model context.</param>
        /// <returns>The AI-generated text, or a fallback string on failure.</returns>
        public static async Task<string> DescribeImageAsync(string imageBase64, string mimeType, string prompt, AIToolCall sourceToolCall)
        {
            try
            {
                var imgArgs = new JObject
                {
                    ["imageBase64"] = imageBase64,
                    ["mimeType"] = mimeType,
                    ["prompt"] = prompt,
                };

                var imgInteraction = new AIInteractionToolCall
                {
                    Name = "img2text",
                    Arguments = imgArgs,
                    Agent = AIAgent.Assistant,
                };

                var imgToolCall = new AIToolCall
                {
                    Endpoint = "img2text",
                    Provider = sourceToolCall.Provider,
                    Model = sourceToolCall.Model,
                    Parameters = sourceToolCall.Parameters,
                };

                imgToolCall.FromToolCallInteraction(imgInteraction);

                var imgResult = await imgToolCall.Exec().ConfigureAwait(false);

                var toolResult = ToolCallResult.FromAIReturn(imgResult);
                return toolResult["description"]?.ToString() ?? "[Image could not be described]";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageProcessingService] DescribeImageAsync failed: {ex.Message}");
                return "[Image description failed]";
            }
        }

        /// <summary>
        /// Downloads an image from a URL and returns it as a base64-encoded data string.
        /// </summary>
        /// <param name="imageUrl">The image URL to download.</param>
        /// <param name="httpClient">Optional HTTP client to use. If null, a new one is created.</param>
        /// <returns>A tuple of base64 data and detected MIME type, or null values on failure.</returns>
        public static async Task<(string? Base64Data, string? MimeType)> DownloadImageAsync(string imageUrl, HttpClient? httpClient = null)
        {
            HttpClient client = httpClient;
            bool ownsClient = false;
            try
            {
                if (client == null)
                {
                    client = new HttpClient();
                    ownsClient = true;
                }

                var response = await client.GetAsync(new Uri(imageUrl)).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[ImageProcessingService] Failed to download image {imageUrl}: HTTP {(int)response.StatusCode}");
                    return (null, null);
                }

                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                string mimeType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
                return (Convert.ToBase64String(bytes), mimeType);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageProcessingService] DownloadImageAsync failed for {imageUrl}: {ex.Message}");
                return (null, null);
            }
            finally
            {
                if (ownsClient)
                {
                    client.Dispose();
                }
            }
        }

        /// <summary>
        /// Processes a single image item and returns the Markdown replacement for the requested mode.
        /// For web images, the item is downloaded if base64 data is not already available.
        /// </summary>
        /// <param name="item">The image item to process.</param>
        /// <param name="imageMode">The image mode: <c>link</c>, <c>embed</c>, <c>describe</c>, or <c>caption</c>.</param>
        /// <param name="sourceToolCall">The parent tool call providing provider and model context.</param>
        /// <param name="httpClient">Optional HTTP client for downloading web images.</param>
        /// <param name="prompt">Optional custom prompt for AI image description. If null, the default prompt for the mode is used.</param>
        /// <returns>The Markdown replacement string.</returns>
        public static async Task<string> ProcessImageItemAsync(ImageProcessingItem item, string imageMode, AIToolCall sourceToolCall, HttpClient? httpClient = null, string? prompt = null)
        {
            if (imageMode == "link")
            {
                return BuildMarkdownReplacement("link", string.Empty, item.AltText, item.Url, null, null, item.Id, item.Context);
            }

            string base64Data = item.Base64Data;
            string mimeType = item.MimeType;

            if (string.IsNullOrEmpty(base64Data) && !string.IsNullOrEmpty(item.Url))
            {
                var (downloadedBase64, downloadedMime) = await DownloadImageAsync(item.Url, httpClient).ConfigureAwait(false);
                base64Data = downloadedBase64 ?? string.Empty;
                mimeType = downloadedMime ?? mimeType;
            }

            if (string.IsNullOrEmpty(base64Data))
            {
                return BuildMarkdownReplacement("link", string.Empty, item.AltText, item.Url, null, null, item.Id, item.Context);
            }

            string effectivePrompt = prompt ?? GetDefaultPrompt(imageMode);
            string aiText = await DescribeImageAsync(base64Data, mimeType, effectivePrompt, sourceToolCall).ConfigureAwait(false);
            return BuildMarkdownReplacement(imageMode, aiText, item.AltText, item.Url, mimeType, base64Data, item.Id, item.Context);
        }

        /// <summary>
        /// Processes every image in <paramref name="items"/> and replaces each matching
        /// <see cref="ImageProcessingItem.Placeholder"/> in the Markdown with the formatted
        /// result for the requested <paramref name="imageMode"/>. This is the single shared
        /// pipeline used by both <c>file2md</c> and <c>web2md</c>.
        /// </summary>
        /// <param name="markdown">The Markdown content containing image placeholders.</param>
        /// <param name="items">The image items to process, each carrying its own placeholder.</param>
        /// <param name="imageMode">The image mode: <c>link</c>, <c>embed</c>, <c>describe</c>, or <c>caption</c>.</param>
        /// <param name="sourceToolCall">The parent tool call providing provider and model context.</param>
        /// <param name="httpClient">Optional HTTP client for downloading web images.</param>
        /// <param name="prompt">Optional custom prompt for AI image description. If null, the default prompt for the mode is used.</param>
        /// <returns>The Markdown content with all image placeholders replaced.</returns>
        public static async Task<string> ProcessMarkdownImagesAsync(
            string markdown,
            IEnumerable<ImageProcessingItem> items,
            string imageMode,
            AIToolCall sourceToolCall,
            HttpClient? httpClient = null,
            string? prompt = null)
        {
            var replacements = new List<(int MatchIndex, string Original, string Replacement)>();

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Placeholder))
                {
                    continue;
                }

                string replacement = await ProcessImageItemAsync(item, imageMode, sourceToolCall, httpClient, prompt).ConfigureAwait(false);
                int index = markdown.IndexOf(item.Placeholder, StringComparison.Ordinal);
                if (index >= 0)
                {
                    replacements.Add((index, item.Placeholder, replacement));
                }
            }

            // Rebuild the Markdown from the end so earlier indices remain valid.
            var sb = new StringBuilder(markdown);
            foreach (var (matchIndex, original, replacement) in replacements.OrderByDescending(r => r.MatchIndex))
            {
                sb.Remove(matchIndex, original.Length);
                sb.Insert(matchIndex, replacement);
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Represents a single image to be processed by <see cref="ImageProcessingService"/>.
    /// </summary>
    public sealed class ImageProcessingItem
    {
        /// <summary>Gets or sets the image identifier.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the image context label.</summary>
        public string Context { get; set; } = string.Empty;

        /// <summary>Gets or sets the image MIME type.</summary>
        public string MimeType { get; set; } = "image/png";

        /// <summary>Gets or sets the base64-encoded image data, if already available.</summary>
        public string Base64Data { get; set; } = string.Empty;

        /// <summary>Gets or sets the original image URL, if applicable.</summary>
        public string? Url { get; set; }

        /// <summary>Gets or sets the alt text or caption text.</summary>
        public string AltText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the exact substring in the Markdown that this image replaces.
        /// For file images this is typically <c>[image N]</c>; for web images it is
        /// the original <c>![alt](url)</c> reference.
        /// </summary>
        public string Placeholder { get; set; } = string.Empty;
    }
}