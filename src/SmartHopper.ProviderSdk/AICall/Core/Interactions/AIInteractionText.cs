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
using System.Globalization;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.AICall.Utilities;
namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Represents an AI-generated text result with associated metadata.
    /// </summary>
    public sealed record AIInteractionText : AIInteractionBase, IAIKeyedInteraction, IAIRenderInteraction
    {
        /// <summary>
        /// Gets the content of the message.
        /// </summary>
        public string Content { get; init; }

        /// <summary>
        /// Gets the reasoning of the message.
        /// </summary>
        public string Reasoning { get; init; }

        /// <summary>
        /// Returns a string representation of the <see cref="AIInteractionText"/>.
        /// </summary>
        /// <returns>A formatted string containing text metadata.</returns>
        public override string ToString()
        {
            var result = string.Empty;

            if (!string.IsNullOrEmpty(this.Reasoning))
            {
                result += $"thinking:{this.Reasoning}\n\n";
            }

            if (!string.IsNullOrEmpty(this.Content))
            {
                result += $"{this.Content}";
            }

            return result;
        }

        /// <summary>
        /// Returns a new <see cref="AIInteractionText"/> with the given result values.
        /// </summary>
        /// <param name="agent">The agent that generated the text.</param>
        /// <param name="content">The content to generate the text from.</param>
        /// <param name="reasoning">The reasoning to generate the text from.</param>
        /// <returns>A new immutable <see cref="AIInteractionText"/>.</returns>
        public AIInteractionText WithResult(AIAgent agent, string content, string reasoning = null)
            => this with { Agent = agent, Content = content, Reasoning = reasoning };

        /// <summary>
        /// Returns a new <see cref="AIInteractionText"/> with the given metrics combined
        /// into the existing metrics.
        /// </summary>
        /// <param name="metricsDelta">Optional metrics to combine.</param>
        /// <returns>A new immutable <see cref="AIInteractionText"/>.</returns>
        public AIInteractionText WithDeltaMetrics(AIMetrics metricsDelta)
        {
            if (metricsDelta == null)
            {
                return this;
            }

            return this with { Metrics = (this.Metrics ?? new AIMetrics()).WithCombined(metricsDelta) };
        }

        /// <summary>
        /// Returns a stable stream grouping key for this interaction.
        /// When a TurnId exists, the key is stable across streaming chunks (no timestamp),
        /// ensuring UI upserts replace the same DOM node. For non-turn messages, includes a timestamp.
        /// </summary>
        /// <returns>Stream group key.</returns>
        public string GetStreamKey()
        {
            var agent = (this.Agent.ToString() ?? "assistant").ToLowerInvariant();
            var timestamp = this.Time.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(this.TurnId))
            {
                // Stable per-turn key (no timestamp) so streaming chunks upsert the same element
                return $"turn:{this.TurnId}:{agent}";
            }

            // Fallback for messages without a TurnId
            return $"text:{agent}";
        }

        /// <summary>
        /// Returns a stable de-duplication key for this interaction using agent and trimmed content.
        /// </summary>
        /// <returns>De-duplication key.</returns>
        public string GetDedupKey()
        {
            var turnIdPart = !string.IsNullOrWhiteSpace(this.TurnId) ? this.TurnId : string.Empty;
            var agentPart = (this.Agent.ToString() ?? "assistant").ToLowerInvariant();
            var content = (this.Content ?? string.Empty).Trim();
            var hash = HashUtility.ComputeShortHash($"{turnIdPart}:{agentPart}:{content}");

            return $"{this.GetStreamKey()}:{hash}";
        }

        /// <summary>
        /// Gets the CSS role class to use when rendering this interaction.
        /// </summary>
        public string GetRoleClassForRender()
        {
            return (this.Agent.ToString() ?? "assistant").ToLowerInvariant();
        }

        /// <summary>
        /// Gets the display name for rendering (header label).
        /// </summary>
        public string GetDisplayNameForRender()
        {
            return this.Agent.ToDescription();
        }

        /// <summary>
        /// Gets the raw markdown content to render for this interaction.
        /// </summary>
        public string GetRawContentForRender()
        {
            return this.Content ?? string.Empty;
        }

        /// <summary>
        /// Gets the raw reasoning content to render for this interaction.
        /// </summary>
        public string GetRawReasoningForRender()
        {
            return this.Reasoning ?? string.Empty;
        }

        /// <summary>
        /// Mutable builder used to incrementally construct an <see cref="AIInteractionText"/>
        /// during streaming, provider decoding, or UI aggregation.
        /// </summary>
        public sealed class Builder
        {
            /// <summary>
            /// Gets or sets the turn identifier.
            /// </summary>
            public string TurnId { get; set; }

            /// <summary>
            /// Gets or sets the timestamp.
            /// </summary>
            public DateTime Time { get; set; } = DateTime.UtcNow;

            /// <summary>
            /// Gets or sets the agent.
            /// </summary>
            public AIAgent Agent { get; set; }

            /// <summary>
            /// Gets or sets the metrics.
            /// </summary>
            public AIMetrics Metrics { get; set; }

            /// <summary>
            /// Gets or sets the content.
            /// </summary>
            public string Content { get; set; }

            /// <summary>
            /// Gets or sets the reasoning.
            /// </summary>
            public string Reasoning { get; set; }

            /// <summary>
            /// Initializes a new empty builder.
            /// </summary>
            public Builder()
            {
            }

            /// <summary>
            /// Initializes a builder from an existing interaction.
            /// </summary>
            /// <param name="source">The interaction to copy.</param>
            public Builder(AIInteractionText? source)
            {
                if (source != null)
                {
                    this.TurnId = source.TurnId;
                    this.Time = source.Time;
                    this.Agent = source.Agent;
                    this.Metrics = source.Metrics;
                    this.Content = source.Content;
                    this.Reasoning = source.Reasoning;
                }
            }

            /// <summary>
            /// Sets the result values on the builder.
            /// </summary>
            /// <param name="agent">The agent that generated the text.</param>
            /// <param name="content">The content to generate the text from.</param>
            /// <param name="reasoning">The reasoning to generate the text from.</param>
            /// <returns>The same builder for chaining.</returns>
            public Builder WithResult(AIAgent agent, string content, string reasoning = null)
            {
                this.Agent = agent;
                this.Content = content;
                this.Reasoning = reasoning;
                return this;
            }

            /// <summary>
            /// Appends streamed content to the builder.
            /// </summary>
            /// <param name="contentDelta">Content to append.</param>
            /// <returns>The same builder for chaining.</returns>
            public Builder AppendContent(string contentDelta)
            {
                if (!string.IsNullOrEmpty(contentDelta))
                {
                    this.Content = (this.Content ?? string.Empty) + contentDelta;
                }

                return this;
            }

            /// <summary>
            /// Appends streamed reasoning to the builder.
            /// </summary>
            /// <param name="reasoningDelta">Reasoning to append.</param>
            /// <returns>The same builder for chaining.</returns>
            public Builder AppendReasoning(string reasoningDelta)
            {
                if (!string.IsNullOrEmpty(reasoningDelta))
                {
                    this.Reasoning = (this.Reasoning ?? string.Empty) + reasoningDelta;
                }

                return this;
            }

            /// <summary>
            /// Combines streamed metrics into the builder's metrics.
            /// </summary>
            /// <param name="metricsDelta">Metrics to combine.</param>
            /// <returns>The same builder for chaining.</returns>
            public Builder CombineMetrics(AIMetrics? metricsDelta)
            {
                if (metricsDelta != null)
                {
                    this.Metrics = (this.Metrics ?? new AIMetrics()).WithCombined(metricsDelta);
                }

                return this;
            }

            /// <summary>
            /// Builds an immutable <see cref="AIInteractionText"/> from the builder.
            /// </summary>
            /// <returns>The finalized interaction.</returns>
            public AIInteractionText Build()
                => new AIInteractionText
                {
                    TurnId = this.TurnId,
                    Time = this.Time,
                    Agent = this.Agent,
                    Metrics = this.Metrics,
                    Content = this.Content,
                    Reasoning = this.Reasoning,
                };
        }
    }
}
