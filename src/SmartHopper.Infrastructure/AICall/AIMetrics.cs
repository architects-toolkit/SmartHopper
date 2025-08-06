/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Infrastructure.AICall
{
    public class AIMetrics
    {
        /// <summary>
        /// Gets or sets the reason for the finish of the AI call.
        /// </summary>
        public string FinishReason { get; set; }

        /// <summary>
        /// Gets or sets the completion time of the AI call.
        /// </summary>
        public double CompletionTime { get; set; }

        /// <summary>
        /// Gets or sets the provider used for the AI call.
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// Gets or sets the model used for the AI call.
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// Tracks how many times this response is reused across different data tree branches.
        /// Default is 1 (used once).
        /// </summary>
        public int ReuseCount { get; set; } = 1;

        /// <summary>
        /// Gets or sets the number of cached input tokens used by the AI call.
        /// </summary>
        public int InputTokensCached { get; set; }

        /// <summary>
        /// Gets or sets the number of input tokens from the prompt that were used by the AI call.
        /// </summary>
        public int InputTokensPrompt { get; set; }

        /// <summary>
        /// Gets the number of input tokens used by the AI call.
        /// </summary>
        public int InputTokens => this.InputTokensCached + this.InputTokensPrompt;

        /// <summary>
        /// Gets or sets the number of output tokens for reasoning that were used by the AI call.
        /// </summary>
        public int OutputTokensReasoning { get; set; }

        /// <summary>
        /// Gets or sets the number of output tokens for response generation that were used by the AI call.
        /// </summary>
        public int OutputTokensGeneration { get; set; }

        /// <summary>
        /// Gets the number of output tokens used by the AI call.
        /// </summary>
        public int OutputTokens => this.OutputTokensReasoning + this.OutputTokensGeneration;

        /// <summary>
        /// Gets the total number of tokens used by the AI call.
        /// </summary>
        public int TotalTokens => this.InputTokens + this.OutputTokens;

        /// <summary>
        /// Value indicating whether the structure of this AIMetrics is valid.
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(this.Provider) || string.IsNullOrEmpty(this.Model))
            {
                return false;
            }

            if (this.InputTokens < 0 || this.OutputTokens < 0)
            {
                return false;
            }

            if (this.ReuseCount < 1)
            {
                return false;
            }

            if (string.IsNullOrEmpty(this.FinishReason))
            {
                return false;
            }

            if (this.CompletionTime < 0)
            {
                return false;
            }

            return true;
        }
    }
}
