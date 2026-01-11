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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System.Globalization;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AICall.Utilities;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Represents an AI-generated text result with associated metadata.
    /// </summary>
    public class AIInteractionText : AIInteractionBase, IAIKeyedInteraction, IAIRenderInteraction
    {
        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the reasoning of the message.
        /// </summary>
        public string Reasoning { get; set; }

        /// <summary>
        /// Returns a string representation of the AIInteractionText.
        /// </summary>
        /// <returns>A formatted string containing text metadata.</returns>
        public override string ToString()
        {
            var result = string.Empty;

            if (!string.IsNullOrEmpty(this.Reasoning))
            {
                result += $"<think>{this.Reasoning}</think>";
            }

            if (!string.IsNullOrEmpty(this.Content))
            {
                result += $"{this.Content}";
            }

            return result;
        }

        /// <summary>
        /// Sets the result for text generation.
        /// </summary>
        /// <param name="agent">The agent that generated the text.</param>
        /// <param name="content">The content to generate the text from.</param>
        /// <param name="reasoning">The reasoning to generate the text from.</param>
        public void SetResult(AIAgent agent, string content, string reasoning = null)
        {
            this.Agent = agent;
            this.Content = content;
            this.Reasoning = reasoning;
        }

        /// <summary>
        /// Appends streamed deltas to this interaction. Intended for provider-local aggregation
        /// during streaming so that providers can incrementally build up the assistant message
        /// without mutating shared chat history.
        /// </summary>
        /// <param name="contentDelta">Optional content to append to <see cref="Content"/>.</param>
        /// <param name="reasoningDelta">Optional reasoning to append to <see cref="Reasoning"/>.</param>
        /// <param name="metricsDelta">Optional metrics to combine into <see cref="Metrics"/>.</param>
        public void AppendDelta(string contentDelta = null, string reasoningDelta = null, AIMetrics metricsDelta = null)
        {
            if (!string.IsNullOrEmpty(contentDelta))
            {
                this.Content = (this.Content ?? string.Empty) + contentDelta;
            }

            if (!string.IsNullOrEmpty(reasoningDelta))
            {
                this.Reasoning = (this.Reasoning ?? string.Empty) + reasoningDelta;
            }

            if (metricsDelta != null)
            {
                if (this.Metrics == null)
                {
                    this.Metrics = new AIMetrics();
                }

                this.Metrics.Combine(metricsDelta);
            }
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
    }
}
