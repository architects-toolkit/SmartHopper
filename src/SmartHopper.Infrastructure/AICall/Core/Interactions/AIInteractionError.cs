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
using System.Security.Cryptography;
using System.Text;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Metrics;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Represents an AI-generated text result with associated metadata.
    /// Used as the Result type for AIInteractionText in text generation operations.
    /// </summary>
    public class AIInteractionError : IAIInteraction, IAIKeyedInteraction
    {
        /// <inheritdoc/>
        public AIAgent Agent { get; set; } = AIAgent.Assistant;

        /// <inheritdoc/>
        public DateTime Time { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        public string Content { get; set; }

        /// <inheritdoc/>
        public AIMetrics Metrics { get; set; } = new AIMetrics();

        /// <summary>
        /// Returns a string representation of the AIInteractionText.
        /// </summary>
        /// <returns>A formatted string containing text metadata.</returns>
        public override string ToString()
        {
            var result = string.Empty;

            if(!string.IsNullOrEmpty(this.Content))
            {
                result += $"{this.Content}";
            }

            return result;
        }

        /// <summary>
        /// Sets the result for text generation.
        /// </summary>
        /// <param name="content">The content to generate the text from.</param>
        public void SetResult(string content, AIMetrics metrics = null)
        {
            this.Content = content;
            this.Metrics = metrics ?? new AIMetrics();
        }

        /// <summary>
        /// Returns a stable stream grouping key for this error interaction. Uses a short hash of content.
        /// </summary>
        /// <returns>Stream group key.</returns>
        public string GetStreamKey()
        {
            var hash = ComputeShortHash(this.Content);
            return $"error:{hash}";
        }

        /// <summary>
        /// Returns a stable de-duplication key for this error interaction. Based on content hash.
        /// </summary>
        /// <returns>De-duplication key.</returns>
        public string GetDedupKey()
        {
            var hash = ComputeShortHash(this.Content);
            return $"error:{hash}";
        }

        /// <summary>
        /// Computes a short (16 hex chars) SHA256-based hash for stable keys.
        /// </summary>
        /// <param name="value">Input string.</param>
        /// <returns>Lowercase hex substring of the hash.</returns>
        private static string ComputeShortHash(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant().Substring(0, 16);
            }
        }
    }
}
