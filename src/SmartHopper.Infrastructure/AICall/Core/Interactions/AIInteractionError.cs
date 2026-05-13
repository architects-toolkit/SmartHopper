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

using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AICall.Utilities;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Represents an error with associated metadata.
    /// </summary>
    public class AIInteractionError : AIInteractionBase, IAIKeyedInteraction, IAIRenderInteraction
    {
        /// <inheritdoc/>
        public override AIAgent Agent { get; set; } = AIAgent.Error;

        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Returns a string representation of the AIInteractionText.
        /// </summary>
        /// <returns>A formatted string containing text metadata.</returns>
        public override string ToString()
        {
            var result = string.Empty;

            if (!string.IsNullOrEmpty(this.Content))
            {
                result += $"{this.Content}";
            }

            return result;
        }

        /// <summary>
        /// Sets the result for text generation.
        /// </summary>
        /// <param name="content">The content to generate the text from.</param>
        /// <param name="metrics">Optional metrics to attach; a new instance is created if null.</param>
        public void SetResult(string content, AIMetrics metrics = null)
        {
            this.Content = content;
            this.Metrics = metrics ?? new AIMetrics();
        }

        /// <summary>
        /// Gets the CSS role class to use when rendering this interaction (force 'error' styling).
        /// </summary>
        /// <returns>The CSS role class string for UI rendering ("error").</returns>
        public string GetRoleClassForRender()
        {
            return "error";
        }

        /// <summary>
        /// Gets the display name for rendering (header label).
        /// </summary>
        /// <returns>The display name to show in the UI ("Error").</returns>
        public string GetDisplayNameForRender()
        {
            return "Error";
        }

        /// <summary>
        /// Gets the raw markdown content to render for this interaction.
        /// </summary>
        /// <returns>The raw content string; empty string when no content is set.</returns>
        public string GetRawContentForRender()
        {
            return this.Content ?? string.Empty;
        }

        /// <summary>
        /// Error interactions do not include reasoning by default.
        /// </summary>
        /// <returns>An empty string, as errors have no reasoning section.</returns>
        public string GetRawReasoningForRender()
        {
            return string.Empty;
        }

        /// <summary>
        /// Returns a stable stream grouping key for this error interaction. Uses TurnId when available.
        /// </summary>
        /// <returns>Stream group key.</returns>
        public string GetStreamKey()
        {
            var hash = HashUtility.ComputeShortHash(this.Content ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(this.TurnId))
            {
                return $"turn:{this.TurnId}:error:{hash}";
            }

            return $"error:{hash}";
        }

        /// <summary>
        /// Returns a stable de-duplication key for this error interaction. Based on TurnId when available.
        /// </summary>
        /// <returns>De-duplication key.</returns>
        public string GetDedupKey()
        {
            return this.GetStreamKey();
        }
    }
}
