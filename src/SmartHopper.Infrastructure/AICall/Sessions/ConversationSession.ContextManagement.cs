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

namespace SmartHopper.Infrastructure.AICall.Sessions
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Returns;
    using SmartHopper.Infrastructure.AICall.Sessions.SpecialTurns;
    using SmartHopper.Infrastructure.AICall.Sessions.SpecialTurns.BuiltIn;
    using SmartHopper.Infrastructure.AIModels;

    /// <summary>
    /// ConversationSession partial class containing context management and auto-summarization logic.
    /// </summary>
    public sealed partial class ConversationSession
    {
        /// <summary>
        /// Context usage threshold (0.0 to 1.0) at which automatic summarization is triggered.
        /// </summary>
        private const double ContextSummarizeThreshold = 0.80;

        /// <summary>
        /// Tracks whether a summarization has already been attempted for the current turn.
        /// Used to prevent infinite summarization loops.
        /// </summary>
        private bool _summarizationAttempted;

        /// <summary>
        /// Tracks whether a summarization has occurred during this session.
        /// Used to control debug logging behavior (append vs overwrite).
        /// </summary>
        private bool _summarizationOccurred;

        /// <summary>
        /// Checks if the current context usage exceeds the summarization threshold.
        /// </summary>
        /// <returns>True if context should be summarized; false otherwise.</returns>
        public bool ShouldSummarizeContext()
        {
            var usage = this.Request?.Body?.Metrics?.ContextUsagePercent;
            if (!usage.HasValue)
            {
                // No context limit known, skip threshold check
                return false;
            }

            return usage.Value >= ContextSummarizeThreshold;
        }

        /// <summary>
        /// Checks context usage and triggers summarization if above threshold.
        /// </summary>
        private async Task CheckAndSummarizeContextAsync(CancellationToken ct)
        {
            if (this.ShouldSummarizeContext())
            {
                var usage = this.Request?.Body?.Metrics?.ContextUsagePercent;
                Debug.WriteLine($"[ConversationSession] Context usage at {usage:P1}, triggering pre-emptive summarization");
                await this.TrySummarizeContextAsync(ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Attempts to summarize the conversation history to reduce context size.
        /// This is called automatically when context usage exceeds the threshold.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>True if summarization was successful; false otherwise.</returns>
        public async Task<bool> TrySummarizeContextAsync(CancellationToken cancellationToken = default)
        {
            if (this._summarizationAttempted)
            {
                Debug.WriteLine("[ConversationSession.ContextManagement] Summarization already attempted this turn, skipping");
                return false;
            }

            this._summarizationAttempted = true;

            try
            {
                var usage = this.Request?.Body?.Metrics?.ContextUsagePercent;
                var currentTokens = this.Request?.Body?.GetEffectiveTokenCount() ?? 0;

                // Get context limit from AIBody metrics for debug logging
                var provider = this.Request?.Body?.Metrics?.Provider;
                var model = this.Request?.Body?.Metrics?.Model;
                var contextLimit = !string.IsNullOrEmpty(provider) && !string.IsNullOrEmpty(model)
                    ? ModelManager.Instance?.GetCapabilities(provider, model)?.ContextLimit
                    : null;

                Debug.WriteLine($"[ConversationSession.ContextManagement] Starting context summarization: tokens={currentTokens}/{contextLimit}, usage={usage:P1}");

                // Get the current conversation history
                var interactions = this.Request?.Body?.Interactions?.ToList() ?? new List<IAIInteraction>();

                // Find the last user message - everything after this will be dropped
                var lastUserMessage = interactions.LastOrDefault(i => i.Agent == AIAgent.User);

                if (lastUserMessage == null)
                {
                    Debug.WriteLine("[ConversationSession.ContextManagement] No user message found in history, cannot summarize");
                    return false;
                }

                // Get conversation history up to (but excluding) the last user message
                // This is what will be summarized and replaced
                var lastUserIndex = interactions.LastIndexOf(lastUserMessage);
                var conversationToSummarize = interactions
                    .Take(lastUserIndex)
                    .Where(i => i.Agent != AIAgent.System && i.Agent != AIAgent.Context)
                    .ToList();

                if (conversationToSummarize.Count < 2)
                {
                    Debug.WriteLine("[ConversationSession.ContextManagement] Not enough conversation history to summarize");
                    return false;
                }

#if DEBUG
                // Append marker to debug log before summarization
                this.DebugAppendSummarizedMarker();
                this._summarizationOccurred = true;
#endif

                // Create and execute summarization special turn
                // ReplaceAbove will preserve system/context and replace everything else with summary
                var summarizeConfig = SummarizeSpecialTurn.Create(
                    this.Request.Provider,
                    this.Request.Model,
                    conversationToSummarize,
                    lastUserMessage: null); // Don't exclude anything from the summary input

                var summaryResult = await this.ExecuteSpecialTurnAsync(
                    summarizeConfig,
                    preferStreaming: false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (summaryResult?.Status != AICallStatus.Finished)
                {
                    Debug.WriteLine($"[ConversationSession.ContextManagement] Summarization failed: {summaryResult?.Messages?.FirstOrDefault()?.Message}");
                    return false;
                }

                // ReplaceAbove has already updated the history with system + summary
                // Now manually append the last user message
                var builder = AIBodyBuilder.FromImmutable(this.Request.Body)
                    .Add(lastUserMessage, markAsNew: false);

                this.Request.Body = builder.Build();
                this.UpdateLastReturn();

                var newTokens = this.Request?.Body?.GetEffectiveTokenCount() ?? 0;
                var newUsage = this.Request?.Body?.Metrics?.ContextUsagePercent;
                Debug.WriteLine($"[ConversationSession.ContextManagement] Summarization complete: {currentTokens} -> {newTokens} tokens ({usage:P1} -> {newUsage:P1})");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationSession.ContextManagement] Error during summarization: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Checks if an error message indicates a context length exceeded error.
        /// </summary>
        /// <param name="errorMessage">The error message to check.</param>
        /// <returns>True if the error is a context length exceeded error.</returns>
        public static bool IsContextExceededError(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return false;
            }

            var lowerMessage = errorMessage.ToLowerInvariant();

            // Common patterns for context exceeded errors across providers
            return lowerMessage.Contains("context length") ||
                   lowerMessage.Contains("maximum context") ||
                   lowerMessage.Contains("too large for model") ||
                   lowerMessage.Contains("token limit") ||
                   lowerMessage.Contains("tokens, too large") ||
                   lowerMessage.Contains("context window") ||
                   lowerMessage.Contains("max_tokens") ||
                   lowerMessage.Contains("context_length_exceeded");
        }

        /// <summary>
        /// Resets the summarization attempt flag for a new turn.
        /// Should be called at the start of each provider turn.
        /// </summary>
        private void ResetSummarizationFlag()
        {
            this._summarizationAttempted = false;
        }

#if DEBUG
        /// <summary>
        /// Appends a SUMMARIZED marker to the debug history file.
        /// This preserves the previous conversation history before summarization.
        /// </summary>
        private void DebugAppendSummarizedMarker()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var folder = System.IO.Path.Combine(appData, "Grasshopper", "SmartHopper", "Debug");
                System.IO.Directory.CreateDirectory(folder);
                var filePath = System.IO.Path.Combine(folder, "ConversationSession-History.md");

                // Read existing content
                var existingContent = string.Empty;
                if (System.IO.File.Exists(filePath))
                {
                    existingContent = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                }

                // Append the summarized marker and existing content
                var marker = new System.Text.StringBuilder();
                marker.AppendLine();
                marker.AppendLine("---");
                marker.AppendLine("---");
                marker.AppendLine("# SUMMARIZED");
                marker.AppendLine($"Summarized at: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
                marker.AppendLine("---");
                marker.AppendLine("---");
                marker.AppendLine();
                marker.AppendLine("## Previous Conversation (Before Summary)");
                marker.AppendLine();

                // Write existing content first, then marker, then new history will be written
                var newContent = existingContent + marker.ToString();
                System.IO.File.WriteAllText(filePath, newContent, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationSession.Debug] Error appending summarized marker: {ex.Message}");
            }
        }
#endif
    }
}
