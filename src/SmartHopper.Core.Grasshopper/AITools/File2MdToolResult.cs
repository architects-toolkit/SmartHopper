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
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Types;
using SmartHopper.DocumentProcessing;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Documents;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Typed result of a <c>file2md</c> tool call, shared across all components that invoke the tool.
    /// Eliminates duplicated result-parsing boilerplate from <c>File2MdComponent</c>,
    /// <c>File2AIComponent</c>, and <c>AIFile2MdComponent</c>.
    /// </summary>
    public sealed class File2MdToolResult
    {
        /// <summary>Gets the Markdown content returned by the tool, with inline <c>[image N]</c> placeholders.</summary>
        public string Markdown { get; }

        /// <summary>Gets the detected original format (e.g., "pdf", "docx", "html").</summary>
        public string Format { get; }

        /// <summary>Gets the images extracted from the document, in document order.</summary>
        public IReadOnlyList<VersatileImage> Images { get; }

        /// <summary>Gets the typed tool-result images carrying bytes and source provenance
        /// (never nested inside the result JSON).</summary>
        public IReadOnlyList<ToolResultImage> TypedImages { get; }

        /// <summary>Gets the neutral document evidence when the source produced it (PDF); null otherwise.</summary>
        public DocumentEvidence Document { get; }

        /// <summary>Gets warnings emitted by the tool during conversion.</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>Gets the raw tool result JObject, for callers that need access to additional fields.</summary>
        public JObject Raw { get; }

        private File2MdToolResult(
            string markdown,
            string format,
            IReadOnlyList<VersatileImage> images,
            IReadOnlyList<string> warnings,
            JObject raw,
            IReadOnlyList<ToolResultImage> typedImages = null,
            DocumentEvidence document = null)
        {
            this.Markdown = markdown;
            this.Format = format;
            this.Images = images;
            this.Warnings = warnings;
            this.Raw = raw;
            this.TypedImages = typedImages ?? Array.Empty<ToolResultImage>();
            this.Document = document;
        }

        // -----------------------------------------------------------------------------------------
        // Invocation
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Builds and executes a <c>file2md</c> tool call, returning a typed result.
        /// </summary>
        /// <param name="filePath">Absolute path to the file to convert.</param>
        /// <param name="removeHeadersFooters">Whether to attempt to remove headers and footers.</param>
        /// <param name="extractImages">Whether to extract embedded images.</param>
        /// <param name="sourceDocument">
        /// Value stored in <see cref="VersatileImage.SourceDocument"/> for each extracted image.
        /// Typically the file path.
        /// </param>
        /// <param name="preserveFormatting">Whether to preserve inline text formatting. DOCX preserves colors, highlights, bold, and italic; XLSX and PPTX preserve bold and italic.</param>
        /// <param name="preserveComments">Whether to preserve comments in DOCX files.</param>
        /// <param name="preserveFootnotes">Whether to preserve footnotes in DOCX files.</param>
        /// <param name="preserveEndnotes">Whether to preserve endnotes in DOCX files.</param>
        /// <param name="describeImages">Whether to use a configured vision helper for contextual textual evidence.</param>
        /// <param name="pages">Optional unique 1-based PDF page numbers; null means the whole document.</param>
        /// <param name="region">Optional normalized PDF detail region on a selected page.</param>
        /// <param name="consumerSupportsImages">Whether the consuming model accepts image input. False requests explicit vision-helper evidence; null reports unknown capability without a paid call.</param>
        /// <param name="imageDescriptionPrompt">Optional user guidance appended to the shared enrichment instruction.</param>
        /// <returns>
        /// A <see cref="File2MdToolResult"/> on success, or <c>null</c> if the tool returned no result.
        /// </returns>
        public static async Task<File2MdToolResult> CallAsync(
            string filePath,
            bool removeHeadersFooters = true,
            bool extractImages = true,
            string sourceDocument = null,
            bool preserveFormatting = true,
            bool preserveComments = true,
            bool preserveFootnotes = true,
            bool preserveEndnotes = true,
            bool describeImages = false,
            IReadOnlyList<int> pages = null,
            DocumentRegionSelection region = null,
            bool? consumerSupportsImages = null,
            string imageDescriptionPrompt = null,
            CancellationToken cancellationToken = default)
        {
            var parameters = new JObject
            {
                ["filePath"] = filePath,
                ["removeHeadersFooters"] = removeHeadersFooters,
                ["preserveFormatting"] = preserveFormatting,
                ["preserveComments"] = preserveComments,
                ["preserveFootnotes"] = preserveFootnotes,
                ["preserveEndnotes"] = preserveEndnotes,
                ["extractImages"] = extractImages,
                ["describeImages"] = describeImages,
            };

            if (pages != null)
            {
                parameters["pages"] = new JArray(pages);
            }

            if (region != null)
            {
                parameters["region"] = new JObject
                {
                    ["page"] = region.Page,
                    ["x"] = region.X,
                    ["y"] = region.Y,
                    ["width"] = region.Width,
                    ["height"] = region.Height,
                };
            }

            if (consumerSupportsImages != null)
            {
                parameters["consumerSupportsImages"] = consumerSupportsImages.Value;
            }

            if (!string.IsNullOrWhiteSpace(imageDescriptionPrompt))
            {
                parameters["imageDescriptionPrompt"] = imageDescriptionPrompt;
            }

            var toolCallInteraction = new AIInteractionToolCall
            {
                Name = "file2md",
                Arguments = parameters,
                Agent = AIAgent.Assistant,
            };

            var toolCall = new AIToolCall
            {
                Endpoint = "file2md",
            };

            toolCall.FromToolCallInteraction(toolCallInteraction);
            toolCall.SkipMetricsValidation = true;

            AIReturn aiResult = await toolCall.Exec(cancellationToken).ConfigureAwait(false);
            var toolResult = ToolCallResult.FromAIReturn(aiResult);

            if (toolResult.Result == null)
            {
                return null;
            }

            return Parse(toolResult, sourceDocument ?? filePath);
        }

        // -----------------------------------------------------------------------------------------
        // Typed parsing
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Parses a typed <see cref="ToolCallResult"/> — image bytes and provenance come
        /// from <see cref="ToolCallResult.Images"/> while the JSON supplies compact
        /// descriptors, pages and coverage. This is the contract new producers write;
        /// components use it instead of mining nested base64 JSON.
        /// </summary>
        /// <param name="result">The typed tool call result.</param>
        /// <param name="sourceDocument">Value stored in <see cref="VersatileImage.SourceDocument"/>.</param>
        public static File2MdToolResult Parse(ToolCallResult result, string sourceDocument)
        {
            if (result?.Result == null)
            {
                return null;
            }

            var raw = result.Result;
            string markdown = raw["content"]?.ToString() ?? string.Empty;
            string format = raw["originalFormat"]?.ToString() ?? string.Empty;
            var warnings = ParseWarnings(raw["warnings"] as JArray);

            var typed = (result.Images ?? new List<ToolResultImage>()).Where(i => i != null).ToList();
            var images = typed.Select(i => VersatileImage.FromExtractedDocument(
                base64Data: i.ImageData,
                mimeType: i.MimeType,
                id: i.SourceReference?.AssetId ?? "img",
                context: i.SourceReference?.PageNumber != null ? $"Page {i.SourceReference.PageNumber.Value}" : "Document",
                pageOrSlide: i.SourceReference?.PageNumber ?? 0,
                sourceDocument: sourceDocument ?? string.Empty,
                source: i.SourceReference)).ToList();

            var document = DocumentEvidenceJsonCodec.Decode(raw, typed);
            return new File2MdToolResult(markdown, format, images, warnings, raw, typed, document);
        }

        /// <summary>
        /// Rebuilds neutral <see cref="DocumentEvidence"/> from the compact result
        /// descriptors — page metadata, coverage and the typed images (whose bytes and
        /// provenance never round-tripped through JSON).
        /// </summary>

        // -----------------------------------------------------------------------------------------
        // Parsing
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Parses a raw <c>file2md</c> tool result JObject into a typed <see cref="File2MdToolResult"/>.
        /// Use this overload when the tool was called via <c>CallAIToolAsync</c> (component path)
        /// and you already have the <c>JObject</c>.
        /// </summary>
        /// <param name="toolResult">The raw JObject returned by the tool.</param>
        /// <param name="sourceDocument">Value stored in <see cref="VersatileImage.SourceDocument"/>.</param>
        public static File2MdToolResult Parse(JObject toolResult, string sourceDocument)
        {
            if (toolResult == null)
            {
                return null;
            }

            string markdown = toolResult["content"]?.ToString() ?? string.Empty;
            string format = toolResult["originalFormat"]?.ToString() ?? string.Empty;

            var images = ParseImages(toolResult["images"] as JArray, sourceDocument);
            var warnings = ParseWarnings(toolResult["warnings"] as JArray);

            return new File2MdToolResult(markdown, format, images, warnings, toolResult);
        }

        // -----------------------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Parses the <c>images</c> JSON array from a tool result into <see cref="VersatileImage"/> objects.
        /// </summary>
        public static IReadOnlyList<VersatileImage> ParseImages(JArray imagesArray, string sourceDocument)
        {
            var images = new List<VersatileImage>();
            if (imagesArray == null)
            {
                return images;
            }

            foreach (var imgObj in imagesArray.OfType<JObject>())
            {
                string base64Data = imgObj["base64Data"]?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(base64Data)) continue;

                images.Add(VersatileImage.FromExtractedDocument(
                    base64Data: base64Data,
                    mimeType: imgObj["mimeType"]?.ToString() ?? "image/png",
                    id: imgObj["id"]?.ToString() ?? "img",
                    context: imgObj["context"]?.ToString() ?? string.Empty,
                    pageOrSlide: imgObj["pageOrSlide"]?.Value<int>() ?? 0,
                    sourceDocument: sourceDocument ?? string.Empty));
            }

            return images;
        }

        /// <summary>
        /// Parses the <c>warnings</c> JSON array from a tool result into a string list.
        /// </summary>
        public static IReadOnlyList<string> ParseWarnings(JArray warningsArray)
        {
            var warnings = new List<string>();
            if (warningsArray == null)
            {
                return warnings;
            }

            warnings.AddRange(
                warningsArray
                    .Select(w => w?.ToString())
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Select(text => text));

            return warnings;
        }

        /// <summary>
        /// Converts a <see cref="GH_Structure{GH_Boolean}"/> to a <see cref="GH_Structure{GH_String}"/>
        /// with lowercase string values ("true"/"false"). Shared utility used by components that pass
        /// boolean inputs into the unified string-based processing pipeline.
        /// </summary>
        /// <param name="boolTree">The boolean data tree to convert.</param>
        /// <param name="defaultValue">Default string value when a branch is empty.</param>
        public static GH_Structure<GH_String> ConvertBoolTreeToString(GH_Structure<GH_Boolean> boolTree, string defaultValue)
        {
            var result = new GH_Structure<GH_String>();
            foreach (var path in boolTree.Paths)
            {
                var branch = boolTree.get_Branch(path);
                if (branch != null && branch.Count > 0)
                {
                    var firstBool = branch[0] as GH_Boolean;
                    result.Append(new GH_String(firstBool?.Value.ToString().ToLowerInvariant() ?? defaultValue), path);
                }
                else
                {
                    result.Append(new GH_String(defaultValue), path);
                }
            }

            return result;
        }
    }
}
