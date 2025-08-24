/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using System.Diagnostics;
using SmartHopper.Infrastructure.AICall.Core.Base;

namespace SmartHopper.Infrastructure.AICall.Metrics
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
        public (bool IsValid, List<AIRuntimeMessage> Errors) IsValid()
        {
            var errors = new List<AIRuntimeMessage>();

            if (string.IsNullOrEmpty(this.Provider) || string.IsNullOrEmpty(this.Model))
            {
                errors.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, "Provider and model fields are required"));
            }

            if (this.InputTokens < 0 || this.OutputTokens < 0)
            {
                errors.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, "Input and output tokens must be greater than or equal to 0"));
            }

            if (string.IsNullOrEmpty(this.FinishReason))
            {
                errors.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, "Finish reason must be set"));
            }

            if (this.CompletionTime < 0)
            {
                errors.Add(new AIRuntimeMessage(AIRuntimeMessageSeverity.Error, AIRuntimeMessageOrigin.Validation, "Completion time must be greater than or equal to 0"));
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// Combines the metrics of another AIMetrics object into this one.
        /// </summary>
        /// <param name="other">The AIMetrics object to combine with.</param>
        public void Combine(AIMetrics other)
        {
            Debug.WriteLine($"[AIMetrics] Combining metrics:\nProvider: {this.Provider} -> {other.Provider}\nModel: {this.Model} -> {other.Model}\nInputTokensPrompt: {this.InputTokensPrompt} -> {this.InputTokensPrompt + other.InputTokensPrompt}\nInputTokensCached: {this.InputTokensCached} -> {this.InputTokensCached + other.InputTokensCached}\nOutputTokensReasoning: {this.OutputTokensReasoning} -> {this.OutputTokensReasoning + other.OutputTokensReasoning}\nOutputTokensGeneration: {this.OutputTokensGeneration} -> {this.OutputTokensGeneration + other.OutputTokensGeneration}\nCompletionTime: {this.CompletionTime} -> {this.CompletionTime + other.CompletionTime}\nFinishReason: {this.FinishReason} -> {other.FinishReason}");

            if (other.Provider != null)
            {
                this.Provider = other.Provider;
            }

            if (other.Model != null)
            {
                this.Model = other.Model;
            }

            if (other.FinishReason != null)
            {
                this.FinishReason = other.FinishReason;
            }

            this.InputTokensPrompt += other.InputTokensPrompt;
            this.InputTokensCached += other.InputTokensCached;
            this.OutputTokensReasoning += other.OutputTokensReasoning;
            this.OutputTokensGeneration += other.OutputTokensGeneration;
            this.CompletionTime += other.CompletionTime;
        }
    }
}
