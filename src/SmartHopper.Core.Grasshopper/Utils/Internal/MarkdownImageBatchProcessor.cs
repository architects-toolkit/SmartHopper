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
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase.Batch;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Core.Grasshopper.Utils.Internal
{
    /// <summary>
    /// Context stored per Markdown document during batched image description so that
    /// <see cref="OnBatchCompleted"/> can reconstruct the final Markdown once every
    /// image sentinel has been resolved.
    /// </summary>
    public sealed class MarkdownImageBatchContext
    {
        /// <summary>Gets or sets the Markdown with original image placeholders.</summary>
        public string BaseMarkdown { get; set; }

        /// <summary>Gets or sets the ordered list of image slots for this document.</summary>
        public List<MarkdownImageSlot> Images { get; set; } = new List<MarkdownImageSlot>();
    }

    /// <summary>
    /// Describes a single image inside a Markdown document that may be described by an AI call.
    /// </summary>
    public sealed class MarkdownImageSlot
    {
        /// <summary>Gets or sets the 1-based image index (for <c>[image N]</c> placeholders).</summary>
        public int Index { get; set; }

        /// <summary>Gets or sets the batch sentinel ID for this image's AI description.</summary>
        public string SentinelId { get; set; }

        /// <summary>Gets or sets the image identifier.</summary>
        public string ImageId { get; set; }

        /// <summary>Gets or sets the image handling mode ('embed', 'describe', or 'caption').</summary>
        public string ImageMode { get; set; }

        /// <summary>Gets or sets the image context label.</summary>
        public string ImageContext { get; set; }

        /// <summary>Gets or sets the image MIME type.</summary>
        public string MimeType { get; set; } = "image/png";

        /// <summary>Gets or sets the base64-encoded image data, if already available.</summary>
        public string Base64Data { get; set; }

        /// <summary>Gets or sets the original image URL, if applicable.</summary>
        public string Url { get; set; }

        /// <summary>Gets or sets the alt text or link-mode alt text.</summary>
        public string AltText { get; set; }

        /// <summary>
        /// Gets or sets the exact substring in the Markdown that this image replaces.
        /// For file images this is typically <c>[image N]</c>; for web images it is
        /// the original <c>![alt](url)</c> reference.
        /// </summary>
        public string Placeholder { get; set; }
    }

    /// <summary>
    /// Shared image-description pipeline for Markdown documents. Handles downloading web images,
    /// invoking an injected AI description callback, building Markdown replacements, and managing
    /// the batch sentinel context required when multiple images are described per document.
    /// </summary>
    public static class MarkdownImageBatchProcessor
    {
        /// <summary>
        /// Processes all images in the supplied Markdown, optionally calling the AI description
        /// callback for each image. In batch mode, returns a sentinel-wrapped Markdown string and
        /// populates <paramref name="batchContext"/> so the caller can reconstruct the final
        /// Markdown in <see cref="OnBatchCompleted"/>.
        /// </summary>
        /// <param name="markdown">The Markdown containing image placeholders.</param>
        /// <param name="images">The image slots to process, in document order.</param>
        /// <param name="imageMode">The image mode: 'skip'/'link' leaves images untouched; 'embed', 'describe', or 'caption' triggers AI.</param>
        /// <param name="describeImageAsync">Callback that receives img2text parameters and returns either the description text or a batch sentinel string.</param>
        /// <param name="isBatch">Whether the component is currently collecting batch requests.</param>
        /// <param name="httpClient">Optional HTTP client for downloading web images.</param>
        /// <returns>
        /// A tuple of the Markdown string (final or sentinel-wrapped) and the batch context
        /// (non-null only when <paramref name="isBatch"/> is true and at least one image produced a sentinel).
        /// </returns>
        public static async Task<(string Markdown, MarkdownImageBatchContext BatchContext)> ProcessAsync(
            string markdown,
            IReadOnlyList<MarkdownImageSlot> images,
            string imageMode,
            Func<JObject, Task<string>> describeImageAsync,
            bool isBatch,
            HttpClient httpClient = null)
        {
            if (images == null || images.Count == 0 || imageMode == "link" || imageMode == "skip")
            {
                return (markdown, null);
            }

            var sentinels = new List<string>(images.Count);
            var descriptions = new List<string>(images.Count);

            foreach (var image in images)
            {
                string base64Data = image.Base64Data;
                string mimeType = image.MimeType ?? "image/png";

                if (string.IsNullOrEmpty(base64Data) && !string.IsNullOrEmpty(image.Url))
                {
                    var (downloadedBase64, downloadedMime) = await ImageProcessingService.DownloadImageAsync(image.Url, httpClient).ConfigureAwait(false);
                    base64Data = downloadedBase64 ?? string.Empty;
                    mimeType = downloadedMime ?? mimeType;
                }

                if (string.IsNullOrEmpty(base64Data))
                {
                    descriptions.Add("[Image could not be described]");
                    sentinels.Add(null);
                    continue;
                }

                var parameters = new JObject
                {
                    ["imageBase64"] = base64Data,
                    ["mimeType"] = mimeType,
                    ["prompt"] = ImageProcessingService.GetDefaultPrompt(imageMode),
                };

                string result = await describeImageAsync(parameters).ConfigureAwait(false);

                if (isBatch && BatchSentinel.TryExtract(result, out var sentinelId))
                {
                    sentinels.Add(sentinelId);
                    descriptions.Add(null);
                }
                else
                {
                    sentinels.Add(null);
                    descriptions.Add(result ?? "[Image could not be described]");
                }
            }

            if (isBatch && sentinels.Any(s => !string.IsNullOrEmpty(s)))
            {
                var batchContext = new MarkdownImageBatchContext
                {
                    BaseMarkdown = markdown,
                    Images = new List<MarkdownImageSlot>(images.Count),
                };

                for (int i = 0; i < images.Count; i++)
                {
                    var slot = new MarkdownImageSlot
                    {
                        Index = images[i].Index,
                        SentinelId = sentinels[i],
                        ImageId = images[i].ImageId,
                        ImageMode = imageMode,
                        ImageContext = images[i].ImageContext,
                        MimeType = images[i].MimeType,
                        Base64Data = images[i].Base64Data,
                        Url = images[i].Url,
                        AltText = images[i].AltText,
                        Placeholder = images[i].Placeholder,
                    };
                    batchContext.Images.Add(slot);
                }

                string representativeSentinel = sentinels.First(s => !string.IsNullOrEmpty(s));
                return (BatchSentinel.Wrap(representativeSentinel), batchContext);
            }

            string finalMarkdown = ReplacePlaceholders(markdown, images, descriptions, imageMode);
            return (finalMarkdown, null);
        }

        /// <summary>
        /// Reconstructs the final Markdown for a batched document by resolving every image sentinel
        /// from the batch results.
        /// </summary>
        /// <param name="context">The batch context stored during <see cref="ProcessAsync"/>.</param>
        /// <param name="batchResults">Map of sentinel IDs to provider response bodies.</param>
        /// <param name="providerName">Optional provider name used to decode response bodies.</param>
        /// <param name="metrics">Optional list to populate with per-image metrics extracted from the decoded assistant text interactions.</param>
        /// <param name="onImageResolved">Optional callback invoked for each resolved image with the sentinel ID, raw response body, assistant text interaction, and description text.</param>
        /// <returns>The final Markdown with all image placeholders replaced.</returns>
        public static string Reconstruct(
            MarkdownImageBatchContext context,
            IReadOnlyDictionary<string, JObject> batchResults,
            string providerName = null,
            List<AIMetrics> metrics = null,
            Action<string, JObject, AIInteractionText, string> onImageResolved = null)
        {
            if (context == null || context.Images == null || context.Images.Count == 0)
            {
                return context?.BaseMarkdown ?? string.Empty;
            }

            var descriptions = new List<string>(context.Images.Count);
            IAIProvider provider = null;
            if (!string.IsNullOrWhiteSpace(providerName))
            {
                provider = ProviderManager.Instance.GetProvider(providerName);
            }

            foreach (var image in context.Images)
            {
                string description = "[Image could not be described]";
                JObject body = null;
                AIInteractionText assistantText = null;
                if (!string.IsNullOrEmpty(image.SentinelId) && batchResults != null && batchResults.TryGetValue(image.SentinelId, out body))
                {
                    assistantText = ExtractAssistantText(body, provider);
                    if (!string.IsNullOrWhiteSpace(assistantText?.Content))
                    {
                        description = assistantText.Content;
                    }
                }

                descriptions.Add(description);
                if (assistantText?.Metrics != null)
                {
                    metrics?.Add(assistantText.Metrics);
                }

                onImageResolved?.Invoke(image.SentinelId, body, assistantText, description);
            }

            return ReplacePlaceholders(context.BaseMarkdown, context.Images, descriptions, context.Images[0].ImageMode);
        }

        private static string ReplacePlaceholders(
            string markdown,
            IReadOnlyList<MarkdownImageSlot> images,
            IReadOnlyList<string> descriptions,
            string imageMode)
        {
            var replacements = new List<(int Index, string Placeholder, string Replacement)>(images.Count);

            for (int i = 0; i < images.Count; i++)
            {
                var image = images[i];
                string description = descriptions[i] ?? "[Image could not be described]";

                string replacement = ImageProcessingService.BuildMarkdownReplacement(
                    imageMode,
                    description,
                    image.AltText,
                    image.Url,
                    image.MimeType,
                    image.Base64Data,
                    image.ImageId,
                    image.ImageContext);

                int index = markdown.IndexOf(image.Placeholder, StringComparison.Ordinal);
                if (index >= 0)
                {
                    replacements.Add((index, image.Placeholder, replacement));
                }
            }

            var sb = new StringBuilder(markdown);
            foreach (var (index, placeholder, replacement) in replacements.OrderByDescending(r => r.Index))
            {
                sb.Remove(index, placeholder.Length);
                sb.Insert(index, replacement);
            }

            return sb.ToString();
        }

        private static AIInteractionText ExtractAssistantText(JObject body, IAIProvider provider)
        {
            if (body == null)
            {
                return null;
            }

            IReadOnlyList<IAIInteraction> interactions = null;
            if (provider != null)
            {
                interactions = provider.Decode(body);
            }
            else
            {
                try
                {
                    var aiBody = body.ToObject<AIBody>();
                    interactions = aiBody?.Interactions;
                }
                catch
                {
                    interactions = null;
                }
            }

            return interactions?.OfType<AIInteractionText>().LastOrDefault(i => i.Agent == AIAgent.Assistant);
        }
    }
}
