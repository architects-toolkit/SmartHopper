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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// Base implementation for AI-call-backed modality fallbacks that replace
    /// unsupported <typeparamref name="TModality"/> interactions with
    /// <see cref="AIInteractionText"/> results of a per-item conversion request.
    /// Holds the shared availability contract (provider configured + a concrete
    /// catalogued model satisfying <see cref="RequiresCapability"/>) and the full
    /// conversion template so implementations only declare their specifics.
    /// </summary>
    /// <typeparam name="TModality">The interaction type this fallback consumes.</typeparam>
    public abstract class ModalityFallbackBase<TModality> : IModalityFallback
        where TModality : class, IAIInteraction
    {
        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public abstract AICapability Handles { get; }

        /// <inheritdoc/>
        public abstract AICapability RequiresCapability { get; }

        /// <inheritdoc/>
        public abstract AICapability ResultsIn { get; }

        /// <inheritdoc/>
        public abstract string Description { get; }

        /// <summary>Semantic origin label attached to the conversion request (e.g. "fallback:stt").</summary>
        protected abstract string Origin { get; }

        /// <summary>Label wrapping a successful conversion, e.g. "Audio transcript".</summary>
        protected abstract string ConvertedLabel { get; }

        /// <summary>Replacement content when a conversion succeeds but yields no text.</summary>
        protected abstract string UnavailableContent { get; }

        /// <summary>Replacement content when the conversion call fails.</summary>
        protected abstract string FailureContent { get; }

        /// <summary>Item noun used in progress/summary traces, e.g. "audio" or "image".</summary>
        protected abstract string ItemNoun { get; }

        /// <inheritdoc/>
        public virtual bool IsAvailable(string providerName)
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
            var count = 0;

            // Conversion calls must opt out of central modality preparation or their own
            // modality input would recurse through it at Exec; any caller-supplied extras
            // (e.g. voice/speed for TTS) are preserved alongside the guard.
            var conversionExtras = requestParameters?.Extras != null
                ? new Dictionary<string, JToken>(requestParameters.Extras)
                : new Dictionary<string, JToken>();
            conversionExtras["modality_fallback"] = "disabled";
            var conversionParameters = (requestParameters ?? AIRequestParameters.Empty) with { Extras = conversionExtras };

            foreach (var interaction in body.Interactions)
            {
                ct.ThrowIfCancellationRequested();

                if (interaction is TModality modality)
                {
                    count++;
                    Logger.Trace(this.GetType().FullName, $"[{this.GetType().Name}] Converting {this.ItemNoun} {count} via {providerName}/{modelName}");

                    var request = new AIRequestCall();
                    request.Initialize(
                        provider: providerName,
                        model: modelName,
                        body: this.BuildConversionBody(modality),
                        origin: this.Origin,
                        capability: this.RequiresCapability);

                    request.Parameters = conversionParameters;

                    var aiResult = await request.Exec().ConfigureAwait(false);

                    newInteractions.Add(this.CreateTransformedInteraction(modality, aiResult));

                    var convertedItem = this.CreateConvertedItem(aiResult);
                    if (convertedItem != null)
                    {
                        result.ConvertedItems.Add(convertedItem);
                    }

                    if (aiResult?.Metrics != null)
                    {
                        result.ExtraMetricsList.Add(aiResult.Metrics);
                    }
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

            Logger.Trace(this.GetType().FullName, $"[{this.GetType().Name}] Converted {count} {this.ItemNoun} to text");
            return result;
        }

        /// <summary>
        /// Builds the single-item body sent to the conversion request.
        /// Override to add system prompts or extra context for the conversion.
        /// </summary>
        protected virtual AIBody BuildConversionBody(TModality modality)
        {
            return AIBodyBuilder.Create()
                .Add(modality)
                .Build();
        }

        /// <summary>
        /// Extracts the converted text from a successful conversion result.
        /// Default: the content of the last <see cref="AIInteractionText"/>.
        /// </summary>
        protected virtual string? ExtractConvertedText(AIReturn aiResult)
        {
            return aiResult.Body?.Interactions?
                .OfType<AIInteractionText>()
                .LastOrDefault()?
                .Content;
        }

        /// <summary>
        /// Builds the interaction written into the transformed body for one
        /// converted <typeparamref name="TModality"/> item. Default: a labeled
        /// <see cref="AIInteractionText"/> carrying the extracted conversion text
        /// (or <see cref="UnavailableContent"/>/<see cref="FailureContent"/>).
        /// </summary>
        /// <param name="source">The consumed modality interaction.</param>
        /// <param name="aiResult">The conversion call's result.</param>
        protected virtual IAIInteraction CreateTransformedInteraction(TModality source, AIReturn aiResult)
        {
            var content = aiResult?.Success == true && aiResult.Body != null
                ? $"[{this.ConvertedLabel}: {this.ExtractConvertedText(aiResult) ?? this.UnavailableContent}]"
                : this.FailureContent;

            return new AIInteractionText
            {
                Agent = source.Agent,
                Content = content,
            };
        }

        /// <summary>
        /// Builds the raw passthrough output for one converted item — unwrapped
        /// conversion content emitted directly by passthrough consumers instead of
        /// a follow-up request. Return <c>null</c> to contribute no item.
        /// Default: an assistant <see cref="AIInteractionText"/> carrying the
        /// extracted text (or <see cref="UnavailableContent"/>/<see cref="FailureContent"/>).
        /// </summary>
        /// <param name="aiResult">The conversion call's result.</param>
        protected virtual IAIInteraction CreateConvertedItem(AIReturn aiResult)
        {
            var content = aiResult?.Success == true && aiResult.Body != null
                ? this.ExtractConvertedText(aiResult) ?? this.UnavailableContent
                : this.FailureContent;

            return new AIInteractionText
            {
                Agent = AIAgent.Assistant,
                Content = content,
            };
        }
    }
}
