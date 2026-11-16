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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Infrastructure.AICall.Fallback;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Documents;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Shared projection that turns a <see cref="FileConversionResult"/> into a typed
    /// <see cref="AIReturn"/> for converter tools (file2md, web2md): compact image
    /// descriptors, consumer-capability contract, explicit AI visual enrichment through
    /// the shared <see cref="VisualEvidenceEnrichmentService"/>, and the result body.
    /// Binary image bytes live only on the typed <see cref="ToolResultImage"/> collection,
    /// never inside the JSON payload.
    /// </summary>
    internal static class ConversionToolResultBuilder
    {
        /// <summary>
        /// Build the tool result for a completed conversion.
        /// </summary>
        /// <param name="result">The conversion result (Markdown, images, document evidence).</param>
        /// <param name="source">Display source identity (file path or URL) recorded in the result.</param>
        /// <param name="toolCall">The executing tool call.</param>
        /// <param name="toolInfo">The tool-call interaction carrying id/name/arguments.</param>
        /// <param name="describeImages">True when the caller explicitly requested AI descriptions.</param>
        /// <param name="consumerSupportsImages">Consumer image capability: true/false or null (unknown).</param>
        /// <param name="imageDescriptionPrompt">Optional additional user guidance for the helper.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The populated <see cref="AIReturn"/>.</returns>
        public static async Task<AIReturn> BuildAsync(
            FileConversionResult result,
            string source,
            AIToolCall toolCall,
            AIInteractionToolCall toolInfo,
            bool describeImages,
            bool? consumerSupportsImages,
            string imageDescriptionPrompt,
            CancellationToken ct)
        {
            var output = new AIReturn()
            {
                Request = toolCall,
            };

            var document = result.Document;

            // Typed image payloads — bytes live on the tool-result interaction, never
            // in the JSON descriptors, Markdown or structuredContent.
            var typedImages = new List<ToolResultImage>();
            if (document?.Images != null)
            {
                typedImages.AddRange(document.Images.Where(i => i != null));
            }
            else
            {
                typedImages.AddRange(result.Images.Select(i => new ToolResultImage
                {
                    ImageData = i.RawValue,
                    MimeType = i.MimeType,
                    SendToModel = i.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true,
                    SourceReference = i.SourceReference,
                }));
            }

            if (document == null)
            {
                // Non-document sources (DOCX/CSV/TXT/…) get a neutral page-less
                // DocumentEvidence so all consumers share one coherent source
                // identity — same formula everywhere, never two differing IDs.
                var neutralId = "sha256-" + Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        Encoding.UTF8.GetBytes(source + "\n" + (result.MarkdownContent ?? string.Empty))))
                    .ToLowerInvariant();
                var neutralImages = typedImages
                    .Select((img, i) => img with
                    {
                        SourceReference = img.SourceReference == null
                            ? new SmartHopper.ProviderSdk.AICall.Core.Interactions.SourceContentReference
                            {
                                DocumentId = neutralId,
                                SourceName = source,
                                AssetId = $"img-{i + 1}",
                                ContextText = result.MarkdownContent,
                                Kind = SmartHopper.ProviderSdk.AICall.Core.Interactions.SourceContentKind.Image,
                            }
                            : img.SourceReference with
                            {
                                DocumentId = string.IsNullOrEmpty(img.SourceReference.DocumentId)
                                    ? neutralId
                                    : img.SourceReference.DocumentId,
                                SourceName = string.IsNullOrEmpty(img.SourceReference.SourceName)
                                    ? source
                                    : img.SourceReference.SourceName,
                                AssetId = string.IsNullOrEmpty(img.SourceReference.AssetId)
                                    ? $"img-{i + 1}"
                                    : img.SourceReference.AssetId,
                                ContextText = string.IsNullOrEmpty(img.SourceReference.ContextText)
                                    ? result.MarkdownContent
                                    : img.SourceReference.ContextText,
                            },
                    })
                    .ToList();
                document = new DocumentEvidence
                {
                    DocumentId = neutralId,
                    SourceName = source,
                    Format = result.DetectedFormat ?? string.Empty,
                    Images = neutralImages,
                    Metadata = result.Metadata.Count > 0
                        ? result.Metadata.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty)
                        : new Dictionary<string, string>(),
                    Warnings = result.Warnings.ToList(),
                    Coverage = new DocumentCoverage { IsComplete = true },
                };
                typedImages = neutralImages;
            }

            // Build result — compact descriptors only.
            var toolResult = new JObject
            {
                ["content"] = result.MarkdownContent,
                ["originalFormat"] = result.DetectedFormat,
                ["source"] = source,
            };

            if (document != null)
            {
                // Compact descriptor fields via the shared SDK codec — the same
                // projection File2MdToolResult/chat decode on the other side.
                toolResult.Merge(DocumentEvidenceJsonCodec.Encode(document));
            }

            if (result.Metadata.Count > 0)
            {
                var metadata = new JObject();
                foreach (var kvp in result.Metadata)
                {
                    metadata[kvp.Key] = kvp.Value;
                }

                toolResult["metadata"] = metadata;
            }

            // Top-level retrieval timestamp for web sources (existing output schema).
            if (result.Metadata.TryGetValue("retrievedAt", out var retrievedAt))
            {
                toolResult["retrievedAt"] = retrievedAt?.ToString();
            }

            if (typedImages.Count > 0)
            {
                toolResult["images"] = new JArray(typedImages.Select((img, i) => new JObject
                {
                    ["id"] = img.SourceReference?.AssetId ?? $"img-{i + 1}",
                    ["mimeType"] = img.MimeType,
                    ["width"] = img.Width,
                    ["height"] = img.Height,
                    ["sendToModel"] = img.SendToModel,
                    ["source"] = SourceContentReferenceJsonCodec.Encode(img.SourceReference, includeContextText: false),
                }));
                toolResult["imageCount"] = typedImages.Count;
            }

            // Consumer capability contract: false → explicit paid enrichment; omitted →
            // unknown, original native evidence without an unconsented provider call.
            if (consumerSupportsImages == true)
            {
                toolResult["consumerCapability"] = "supported";
            }
            else if (consumerSupportsImages == null && !describeImages)
            {
                toolResult["consumerCapability"] = "unknown";
                toolResult["clientAdaptationRequired"] = true;
            }

            var explicitAi = describeImages || consumerSupportsImages == false;
            if (explicitAi)
            {
                if (consumerSupportsImages == false)
                {
                    toolResult["consumerCapability"] = "unsupported";
                }

                // Explicit AI comprehension: a failed/partial visual coverage is an
                // error, never a silent text-only success.
                if (document != null && !document.Coverage.IsComplete)
                {
                    var coverageError =
                        "Visual evidence coverage is incomplete; cannot satisfy the explicit visual comprehension request. "
                        + $"Selected [{string.Join(", ", document.Coverage.SelectedPages)}], produced [{string.Join(", ", document.Coverage.RenderedPages)}], failed [{string.Join(", ", document.Coverage.FailedPages)}]."
                        + (result.Warnings.Count > 0 ? " Warnings: " + string.Join("; ", result.Warnings) : string.Empty);
                    output.CreateError(coverageError);
                    return output;
                }

                // Zero-cost contract: with no model-visible images there is nothing to
                // describe — succeed without resolving or invoking any helper.
                if (typedImages.Any(i => i != null && i.SendToModel && !string.IsNullOrEmpty(i.ImageData)))
                {
                    // Enrich through the same ImageToTextFallback implementation and
                    // shared instruction as request fallback.
                    var resultInteraction = new AIInteractionToolResult
                    {
                        Id = toolInfo.Id,
                        Name = toolInfo.Name,
                        Arguments = toolInfo.Arguments,
                        Result = toolResult,
                        Images = typedImages,
                    };
                    var sourceBody = AIBodyBuilder.Create().Add(resultInteraction).Build();
                    var enriched = await VisualEvidenceEnrichmentService.EnrichAsync(
                        sourceBody,
                        toolCall.Provider,
                        toolCall.Model,
                        ct,
                        userGuidance: imageDescriptionPrompt).ConfigureAwait(false);

                    foreach (var metric in enriched.ExtraMetricsList)
                    {
                        if (metric != null)
                        {
                            output.ExtraMetrics.Add(metric);
                        }
                    }

                    // Propagate every helper message (warnings+info), then fail on any error.
                    output.Messages = output.Messages
                        .Concat(enriched.Messages.Where(m => m != null))
                        .ToList();
                    var blocking = enriched.Messages.FirstOrDefault(m => m?.Severity == SHRuntimeMessageSeverity.Error);
                    if (blocking != null)
                    {
                        output.CreateError($"Visual evidence enrichment failed: {blocking.Message}");
                        return output;
                    }

                    var enrichedResult = enriched.TransformedBody?.Interactions
                        .OfType<AIInteractionToolResult>()
                        .FirstOrDefault(r => r.Result?["imageEvidence"] is JArray);
                    if (enrichedResult == null)
                    {
                        output.CreateError("Visual evidence enrichment produced no usable evidence.");
                        return output;
                    }

                    toolResult = (JObject)enrichedResult.Result.DeepClone();
                    var adapted = enrichedResult.Images?.Where(i => i != null).ToList() ?? typedImages;
                    if (consumerSupportsImages == true)
                    {
                        // Supported consumers still receive the original visuals —
                        // keep their SendToModel flags on the preserved bytes/provenance.
                        for (var fi = 0; fi < adapted.Count && fi < typedImages.Count; fi++)
                        {
                            adapted[fi] = adapted[fi] with { SendToModel = typedImages[fi].SendToModel };
                        }
                    }
                    else
                    {
                        // Textual-only consumers: adapted evidence is display-side,
                        // never re-sent to the model.
                        for (var fi = 0; fi < adapted.Count; fi++)
                        {
                            adapted[fi] = adapted[fi] with { SendToModel = false };
                        }
                    }

                    typedImages = adapted;

                    // Source Markdown stays canonical; generated evidence is appended under
                    // clearly labeled page/asset headings, never replacing source text.
                    var evidenceArray = toolResult["imageEvidence"] as JArray;
                    if (evidenceArray?.Count > 0)
                    {
                        var appended = new StringBuilder(result.MarkdownContent ?? string.Empty);
                        appended.AppendLine().AppendLine("## Visual Evidence (AI-Generated)");
                        foreach (var entry in evidenceArray.OfType<JObject>())
                        {
                            var src = entry["source"] as JObject;
                            var assetId = src?["assetId"]?.ToString() ?? "asset";
                            var page = src?["pageNumber"]?.ToString();
                            appended.AppendLine().AppendLine(page != null
                                ? $"### Page {page}, Asset {assetId}"
                                : $"### Asset {assetId}");
                            appended.AppendLine().AppendLine(entry["evidence"]?.ToString() ?? string.Empty);
                        }

                        toolResult["sourceMarkdown"] = result.MarkdownContent;
                        toolResult["content"] = appended.ToString();
                    }

                    // Descriptor array resyncs with post-enrichment SendToModel flags.
                    if (toolResult["images"] is JArray descriptors)
                    {
                        for (var di = 0; di < descriptors.Count && di < typedImages.Count; di++)
                        {
                            descriptors[di]["sendToModel"] = typedImages[di].SendToModel;
                        }
                    }
                }
            }

            if (result.Warnings.Count > 0)
            {
                var warnings = new JArray();
                foreach (var warning in result.Warnings)
                {
                    warnings.Add(warning);
                }

                toolResult["warnings"] = warnings;
            }

            var builder = AIBodyBuilder.Create();
            builder.Add(new AIInteractionToolResult
            {
                Id = toolInfo.Id,
                Name = toolInfo.Name,
                Arguments = toolInfo.Arguments,
                Result = toolResult,
                Images = typedImages,
            });
            var immutable = builder.Build();
            output.CreateSuccess(immutable, toolCall);
            return output;
        }
    }
}
