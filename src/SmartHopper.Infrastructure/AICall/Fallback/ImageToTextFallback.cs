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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AICall.Utilities;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// Converts model-visible image content into faithful textual evidence records via a
    /// vision-capable helper model. Each <see cref="AIInteractionImage"/> and each
    /// <see cref="ToolResultImage"/> flagged <see cref="ToolResultImage.SendToModel"/> is
    /// replaced by one contextual evidence record that preserves the original Agent, TurnId,
    /// Time and <see cref="SourceContentReference"/>. Missing or failed conversions record
    /// blocking errors — an unavailable description is never submitted as a success.
    /// </summary>
    public sealed class ImageToTextFallback : IModalityFallback
    {
        /// <inheritdoc/>
        public string Name => "ImageToText";

        /// <inheritdoc/>
        public AICapability Handles => AICapability.ImageInput;

        /// <inheritdoc/>
        // The contextual helper input is text+image: a pure OCR model (Image2Text
        // only) cannot consume the surrounding source context, so TextInput must
        // also be required for selection/pin validation.
        public AICapability RequiresCapability => AICapability.Image2Text | AICapability.TextInput;

        /// <inheritdoc/>
        public AICapability ResultsIn => AICapability.TextInput;

        /// <inheritdoc/>
        public string Description => "Images converted to contextual textual evidence via vision model";

        /// <inheritdoc/>
        public bool IsAvailable(string providerName)
        {
            // An unconfigured provider cannot execute the conversion call, so it
            // is never a usable fallback candidate regardless of catalogued models.
            var provider = ProviderSdkHost.ProviderRegistry.GetProvider(providerName);
            if (provider?.IsConfigured != true)
            {
                return false;
            }

            var model = AIModelCapabilityRegistry.Instance.SelectBestModel(providerName, null, this.RequiresCapability);
            return !string.IsNullOrWhiteSpace(model) &&
                AIModelCapabilityRegistry.Instance.ValidateCapabilities(providerName, model, this.RequiresCapability);
        }

        /// <inheritdoc/>
        public async Task<ModalityFallbackResult> ApplyAsync(AIBody body, string providerName, string modelName, CancellationToken ct, AIRequestParameters requestParameters = null, ModalityFallbackContext context = null)
        {
            var result = new ModalityFallbackResult();
            var newInteractions = new List<IAIInteraction>();
            var cache = context?.EvidenceCache ?? new ConcurrentDictionary<string, string>();
            var userGuidance = context?.UserGuidance;
            var imageCount = 0;
            var index = -1;

            foreach (var interaction in body.Interactions)
            {
                index++;
                if (ShouldStopForCancellation(result, imageCount, ct))
                {
                    break;
                }

                if (interaction is AIInteractionImage imgInteraction)
                {
                    imageCount++;
                    var evidence = await this.ConvertImageAsync(
                        imgInteraction,
                        imgInteraction.SourceReference,
                        BuildContextText(imgInteraction.SourceReference, index, imgInteraction.TurnId, body),
                        providerName,
                        modelName,
                        cache,
                        userGuidance,
                        result,
                        ct).ConfigureAwait(false);

                    if (evidence == null)
                    {
                        // Blocking error already recorded; keep the original media so strict
                        // outbound validation also reports the leftover modality.
                        newInteractions.Add(imgInteraction);
                        continue;
                    }

                    // One contextual evidence record per image; original provenance preserved.
                    newInteractions.Add(new AIInteractionText
                    {
                        Agent = imgInteraction.Agent,
                        TurnId = imgInteraction.TurnId,
                        Time = imgInteraction.Time,
                        SourceReference = imgInteraction.SourceReference,
                        Content = evidence,
                    });
                }
                else if (interaction is AIInteractionToolResult toolResult
                    && toolResult.Images != null
                    && toolResult.Images.Any(i => i?.IsModelVisible == true))
                {
                    var updatedImages = new List<ToolResultImage>(toolResult.Images.Count);
                    var evidenceEntries = new JArray();

                    foreach (var image in toolResult.Images)
                    {
                        if (ShouldStopForCancellation(result, imageCount, ct))
                        {
                            break;
                        }

                        if (image?.IsModelVisible != true)
                        {
                            updatedImages.Add(image);
                            continue;
                        }

                        imageCount++;
                        var evidence = await this.ConvertImageAsync(
                            new AIInteractionImage
                            {
                                // Helper input is always a single user-role turn; the
                                // original tool agent is preserved only in the outbound record.
                                Agent = AIAgent.User,
                                ImageData = image.ImageData,
                                MimeType = image.MimeType,
                            },
                            image.SourceReference,
                            BuildContextText(image.SourceReference, index, toolResult.TurnId, body),
                            providerName,
                            modelName,
                            cache,
                            userGuidance,
                            result,
                            ct).ConfigureAwait(false);

                        if (evidence == null)
                        {
                            updatedImages.Add(image);
                            continue;
                        }

                        // Keep the tool-result image associated but no longer model-visible;
                        // its evidence joins the tool-result JSON so tool correlation survives.
                        updatedImages.Add(image with { SendToModel = false });
                        evidenceEntries.Add(BuildEvidenceEntry(image.SourceReference, evidence));
                    }

                    // Original tool result keeps id/name/arguments/TurnId; evidence attaches
                    // as a stable array of provenance records (never a bare string), so the
                    // consumer does not depend on generated text repeating identifiers.
                    var updatedResult = toolResult.Result != null
                        ? (JObject)toolResult.Result.DeepClone()
                        : new JObject();
                    if (evidenceEntries.Count > 0)
                    {
                        updatedResult["imageEvidence"] = evidenceEntries;
                    }

                    newInteractions.Add(toolResult with { Result = updatedResult, Images = updatedImages });
                }
                else
                {
                    newInteractions.Add(interaction);
                }
            }

            result.TransformedBody = new AIBody(
                newInteractions,
                body.ToolFilter,
                body.ContextFilter,
                body.JsonOutputSchema,
                body.InteractionsNew);

            Logger.For<ImageToTextFallback>().Trace($"[ImageToTextFallback] Converted {imageCount} model-visible images to contextual evidence");
            return result;
        }

        /// <summary>
        /// Cancellation policy for conversion loops: when no paid work has been recorded
        /// yet, cancellation propagates as <see cref="OperationCanceledException"/> for a
        /// consistent zero-work contract; once helper calls have been charged, the loop
        /// stops with a blocking diagnostic so already-billed metrics are never discarded.
        /// </summary>
        /// <returns>True when the loop should stop (blocking diagnostic already recorded).</returns>
        private static bool ShouldStopForCancellation(ModalityFallbackResult result, int imageCount, CancellationToken ct)
        {
            if (!ct.IsCancellationRequested)
            {
                return false;
            }

            if (imageCount == 0 && result.ExtraMetricsList.Count == 0)
            {
                ct.ThrowIfCancellationRequested();
            }

            result.Messages.Add(new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Error,
                SHRuntimeMessageOrigin.Provider,
                SHMessageCode.ConversionFailed,
                "Visual evidence conversion was cancelled after paid helper work; the partial result is not submittable."));
            return true;
        }

        /// <summary>
        /// Runs the vision helper for one image. Returns the evidence record text, or null
        /// when conversion was not possible; in that case a blocking error carrying the
        /// underlying provider cause has been added to <paramref name="result"/>. Successful
        /// results are memoized per run in <paramref name="cache"/> so identical evidence is
        /// not converted — or billed — twice.
        /// </summary>
        private async Task<string> ConvertImageAsync(
            AIInteractionImage image,
            SourceContentReference source,
            string contextText,
            string providerName,
            string modelName,
            ConcurrentDictionary<string, string> cache,
            string userGuidance,
            ModalityFallbackResult result,
            CancellationToken ct)
        {
            var cacheKey = ComputeConversionKey(image, contextText, providerName, modelName, userGuidance);
            if (cache.TryGetValue(cacheKey, out var memoized))
            {
                return memoized;
            }

            Logger.For<ImageToTextFallback>().Trace($"[ImageToTextFallback] Converting image via {providerName}/{modelName}");

            // One helper turn: shared instruction + mechanical context + user-role image.
            // The original interaction agent AND turn id are never forwarded — a tool-role
            // image or foreign turn id would corrupt the single-turn helper conversation.
            // Source metadata is preserved only in the outbound replacement.
            var helperTurnId = Guid.NewGuid().ToString("N");
            var helperImage = image with { Agent = AIAgent.User, TurnId = helperTurnId };
            var bodyBuilder = AIBodyBuilder.Create()
                .WithTurnId(helperTurnId)
                .AddSystem(VisualEvidencePrompts.ContextualEvidence)
                .AddUser(contextText ?? string.Empty);

            // Optional caller guidance rides alongside the shared master instruction as a
            // labeled user turn — never a replacement for it.
            if (!string.IsNullOrWhiteSpace(userGuidance))
            {
                bodyBuilder.AddUser($"Additional user guidance:\n{userGuidance}");
            }

            var imgBody = bodyBuilder.Add(helperImage).Build();

            // The helper request uses its own provider/model and generic settings: the
            // primary request's Extras are provider-specific and must not be forwarded to
            // a different provider's helper. 'modality_fallback=disabled' guarantees a
            // malformed/unknown helper capability fails explicitly instead of recursing.
            var request = new AIRequestCall();
            request.Initialize(
                provider: providerName,
                model: modelName,
                body: imgBody,
                origin: "fallback:img2text",
                capability: this.RequiresCapability);
            request.Parameters = AIRequestParameters.Create()
                .WithModel(modelName)
                .WithExtra("modality_fallback", "disabled")
                .Build();

            var aiResult = await request.Exec(ct).ConfigureAwait(false);
            if (aiResult?.Metrics != null)
            {
                // Charged helper usage is recorded once, even when conversion fails.
                result.ExtraMetricsList.Add(aiResult.Metrics);
            }

            if (aiResult == null || !aiResult.Success || aiResult.Body == null)
            {
                var cause = aiResult?.Messages?
                    .Where(m => m != null && m.Severity == SHRuntimeMessageSeverity.Error && !string.IsNullOrWhiteSpace(m.Message))
                    .Select(m => m.Message)
                    .FirstOrDefault()
                    ?? (aiResult == null ? "helper call returned no result" : "helper call did not succeed");
                result.Messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Provider,
                    SHMessageCode.ConversionFailed,
                    $"Visual evidence conversion failed for {DescribeSource(source)} via '{providerName}' / '{modelName}': {cause}"));
                return null;
            }

            var evidence = aiResult.Body.Interactions?
                .OfType<AIInteractionText>()
                .LastOrDefault(t => !string.IsNullOrWhiteSpace(t.Content))
                ?.Content;

            if (string.IsNullOrWhiteSpace(evidence))
            {
                result.Messages.Add(new SHRuntimeMessage(
                    SHRuntimeMessageSeverity.Error,
                    SHRuntimeMessageOrigin.Provider,
                    SHMessageCode.ConversionFailed,
                    $"Visual evidence conversion produced no textual evidence for {DescribeSource(source)} via '{providerName}' / '{modelName}'."));
                return null;
            }

            cache[cacheKey] = evidence;
            return evidence;
        }

        /// <summary>
        /// Composes the helper's mechanical context: source fields, caption and surrounding
        /// extracted text; then the nearest matching source text (same document/page) or, for
        /// unsourced images, the nearest preceding user text in the same turn. System and
        /// developer instructions are never eligible, and arbitrary tool-result JSON is never
        /// serialized into the helper context (it can carry nested base64 or unrelated
        /// document content).
        /// </summary>
        private static string BuildContextText(SourceContentReference source, int targetIndex, string turnId, AIBody body)
        {
            var sb = new StringBuilder();
            if (source != null)
            {
                var fields = new List<string>();
                if (!string.IsNullOrWhiteSpace(source.SourceName)) fields.Add($"source '{source.SourceName}'");
                if (!string.IsNullOrWhiteSpace(source.DocumentId)) fields.Add($"document '{source.DocumentId}'");
                if (source.PageNumber != null) fields.Add($"page {source.PageNumber.Value}");
                if (!string.IsNullOrWhiteSpace(source.FigureLabel)) fields.Add($"figure '{source.FigureLabel}'");
                if (!string.IsNullOrWhiteSpace(source.AssetId)) fields.Add($"asset '{source.AssetId}'");
                if (source.Kind != SourceContentKind.Unknown) fields.Add($"kind {source.Kind.ToString().ToLowerInvariant()}");
                if (source.Region != null)
                {
                    fields.Add($"region x={source.Region.X:0.###},y={source.Region.Y:0.###},w={source.Region.Width:0.###},h={source.Region.Height:0.###}");
                }

                if (fields.Count > 0)
                {
                    sb.AppendLine($"Source reference: {string.Join(", ", fields)}.");
                }

                if (!string.IsNullOrWhiteSpace(source.Caption))
                {
                    sb.AppendLine($"Caption: {source.Caption}");
                }

                if (!string.IsNullOrWhiteSpace(source.ContextText))
                {
                    sb.AppendLine($"Surrounding extracted text: {source.ContextText}");
                }
            }

            var texts = body?.Interactions?
                .Take(targetIndex >= 0 ? targetIndex : 0)
                .OfType<AIInteractionText>()
                .Where(t => t.Agent != AIAgent.System && !string.IsNullOrWhiteSpace(t.Content))
                .ToList();
            if (texts != null && texts.Count > 0)
            {
                AIInteractionText nearest = null;
                if (source != null && !string.IsNullOrWhiteSpace(source.DocumentId))
                {
                    // Same document (and page when known): never an arbitrary last text.
                    nearest = texts.LastOrDefault(t =>
                        t.SourceReference != null
                        && string.Equals(t.SourceReference.DocumentId, source.DocumentId, StringComparison.Ordinal)
                        && t.SourceReference.PageNumber == source.PageNumber);
                    nearest ??= texts.LastOrDefault(t =>
                        t.SourceReference != null
                        && string.Equals(t.SourceReference.DocumentId, source.DocumentId, StringComparison.Ordinal));
                }
                else if (!string.IsNullOrWhiteSpace(turnId))
                {
                    // Unsourced image: nearest preceding user text in the same turn only.
                    nearest = texts.LastOrDefault(t =>
                        t.Agent == AIAgent.User
                        && string.Equals(t.TurnId, turnId, StringComparison.Ordinal));
                }

                if (nearest != null)
                {
                    sb.AppendLine($"Nearest associated source text: {nearest.Content}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Mechanically serializes one evidence entry with full source identity so
        /// provenance survives independently of what the helper text repeats.
        /// </summary>
        private static JObject BuildEvidenceEntry(SourceContentReference source, string evidence)
        {
            var entry = new JObject { ["evidence"] = evidence };
            var src = SourceContentReferenceJsonCodec.Encode(source, includeContextText: false);
            if (src != null)
            {
                entry["source"] = src;
            }

            return entry;
        }

        /// <summary>
        /// Builds a stable key for per-run memoization: full SHA-256 over the MIME type,
        /// image bytes/URL, composed context, helper provider/model and the shared
        /// instruction identity, so a different image or context can never silently reuse
        /// the wrong scientific evidence.
        /// </summary>
        private static string ComputeConversionKey(AIInteractionImage image, string contextText, string providerName, string modelName, string userGuidance = null)
        {
            var material = string.Join(
                "|",
                "v1",
                providerName ?? string.Empty,
                modelName ?? string.Empty,
                image.MimeType ?? string.Empty,
                image.ImageData ?? image.ImageUrl?.ToString() ?? string.Empty,
                contextText ?? string.Empty,
                VisualEvidencePrompts.ContextualEvidence,
                userGuidance ?? string.Empty);

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(material));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        private static string DescribeSource(SourceContentReference source)
        {
            if (source == null)
            {
                return "an image";
            }

            var page = source.PageNumber != null ? $" page {source.PageNumber.Value}" : string.Empty;
            var name = !string.IsNullOrWhiteSpace(source.SourceName) ? $" '{source.SourceName}'" : string.Empty;
            return $"image{name}{page}";
        }
    }
}
