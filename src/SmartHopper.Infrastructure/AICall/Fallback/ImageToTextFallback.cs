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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// Converts image inputs to text descriptions via a vision-capable model.
    /// Replaces <see cref="AIInteractionImage"/> entries with <see cref="AIInteractionText"/>
    /// descriptions so that a text-only model can process the body.
    /// </summary>
    public sealed class ImageToTextFallback : IModalityFallback
    {
        /// <inheritdoc/>
        public string Name => "ImageToText";

        /// <inheritdoc/>
        public AICapability Handles => AICapability.ImageInput;

        /// <inheritdoc/>
        public AICapability RequiresCapability => AICapability.Image2Text;

        /// <inheritdoc/>
        public AICapability ResultsIn => AICapability.TextInput;

        /// <inheritdoc/>
        public string Description => "Images converted to text descriptions via vision model";

        /// <inheritdoc/>
        public bool IsAvailable(string providerName)
        {
            return ModelManager.Instance.ValidateCapabilities(
                providerName,
                ModelManager.Instance.SelectBestModel(providerName, null, this.RequiresCapability),
                this.RequiresCapability);
        }

        /// <inheritdoc/>
        public async Task<ModalityFallbackResult> ApplyAsync(AIBody body, string providerName, string modelName, CancellationToken ct)
        {
            var result = new ModalityFallbackResult();
            var newInteractions = new List<IAIInteraction>();
            var imageCount = 0;

            foreach (var interaction in body.Interactions)
            {
                ct.ThrowIfCancellationRequested();

                if (interaction is AIInteractionImage imgInteraction)
                {
                    imageCount++;
                    Debug.WriteLine($"[ImageToTextFallback] Converting image {imageCount} via {providerName}/{modelName}");

                    // Build a vision request for this image
                    var imgBody = AIBodyBuilder.Create()
                        .AddSystem("Describe this image in detail. Include all visible content, text, objects, and context.")
                        .Add(imgInteraction)
                        .Build();

                    var request = new AIRequestCall();
                    request.Initialize(
                        provider: providerName,
                        model: modelName,
                        body: imgBody,
                        endpoint: "fallback:img2text",
                        capability: this.RequiresCapability);

                    var aiResult = await request.Exec().ConfigureAwait(false);

                    if (aiResult.Success && aiResult.Body != null)
                    {
                        var textInteraction = aiResult.Body.Interactions?
                            .OfType<AIInteractionText>()
                            .LastOrDefault();

                        var description = textInteraction?.Content ?? "[Image description unavailable]";
                        var replacement = new AIInteractionText
                        {
                            Agent = imgInteraction.Agent,
                            Content = $"[Image description: {description}]",
                        };
                        newInteractions.Add(replacement);
                    }
                    else
                    {
                        var replacement = new AIInteractionText
                        {
                            Agent = imgInteraction.Agent,
                            Content = "[Image description unavailable — fallback AI call failed]",
                        };
                        newInteractions.Add(replacement);
                    }

                    if (aiResult.Metrics != null)
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

            Debug.WriteLine($"[ImageToTextFallback] Converted {imageCount} images to text");
            return result;
        }
    }
}