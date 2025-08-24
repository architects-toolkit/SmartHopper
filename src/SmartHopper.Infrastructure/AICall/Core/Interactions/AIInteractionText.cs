/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Metrics;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Represents an AI-generated text result with associated metadata.
    /// Used as the Result type for AIInteractionText in text generation operations.
    /// </summary>
    public class AIInteractionText : IAIInteraction
    {
        /// <inheritdoc/>
        public AIAgent Agent { get; set; }

        /// <inheritdoc/>
        public DateTime Time { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Gets or sets the reasoning of the message.
        /// </summary>
        public string Reasoning { get; set; }

        /// <inheritdoc/>
        public AIMetrics Metrics { get; set; } = new AIMetrics();

        /// <summary>
        /// Returns a string representation of the AIInteractionText.
        /// </summary>
        /// <returns>A formatted string containing text metadata.</returns>
        public override string ToString()
        {
            var result = string.Empty;

            if(!string.IsNullOrEmpty(Reasoning))
            {
                result += $"<think>{Reasoning}</think>";
            }

            if(!string.IsNullOrEmpty(Content))
            {
                result += $"{Content}";
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
    }
}
