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

namespace SmartHopper.Infrastructure.AICall.Sessions.SpecialTurns.BuiltIn
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AIModels;

    /// <summary>
    /// Factory for creating summarization special turn configurations.
    /// Used to compress long conversation histories when approaching context limits.
    /// </summary>
    public static class SummarizeSpecialTurn
    {
        /// <summary>
        /// Creates a special turn configuration for summarizing the conversation history.
        /// The summary replaces all previous messages except the system message and the last user message.
        /// </summary>
        /// <param name="providerName">The provider to use for summarization.</param>
        /// <param name="conversationModel">The model used by the conversation. When provided, summarization uses the same model.</param>
        /// <param name="conversationHistory">The full conversation history to summarize (excluding system message).</param>
        /// <param name="lastUserMessage">The last user message to preserve (will not be included in summary).</param>
        /// <returns>A configured special turn for conversation summarization.</returns>
        public static SpecialTurnConfig Create(
            string providerName,
            string conversationModel,
            IEnumerable<IAIInteraction> conversationHistory,
            IAIInteraction lastUserMessage = null)
        {
            var summarizeInteractions = BuildSummarizeInteractions(conversationHistory, lastUserMessage);
            var defaultModel = ModelManager.Instance.GetDefaultModel(providerName, AICapability.Text2Text);
            var effectiveModel = !string.IsNullOrWhiteSpace(conversationModel) ? conversationModel : defaultModel;

            return new SpecialTurnConfig
            {
                TurnType = "summarize",
                OverrideInteractions = summarizeInteractions,
                OverrideModel = effectiveModel,
                OverrideCapability = AICapability.Text2Text,
                OverrideToolFilter = "-*", // Disable all tools during summarization
                ProcessTools = false,
                TimeoutMs = 60000, // 60 second timeout for summarization
                PersistenceStrategy = HistoryPersistenceStrategy.ReplaceAbove,
                PersistenceFilter = InteractionFilter.PreserveSystemContext,
                Metadata = new Dictionary<string, object>
                {
                    ["is_summarize"] = true,
                },
            };
        }

        /// <summary>
        /// Builds the interaction list for conversation summarization.
        /// </summary>
        /// <param name="conversationHistory">The conversation history to summarize.</param>
        /// <param name="lastUserMessage">The last user message to exclude from summary.</param>
        /// <returns>List of interactions for summarization.</returns>
        private static List<IAIInteraction> BuildSummarizeInteractions(
            IEnumerable<IAIInteraction> conversationHistory,
            IAIInteraction lastUserMessage)
        {
            // Build a text representation of the conversation to summarize
            var sb = new StringBuilder();
            sb.AppendLine("Please summarize the following conversation. Create a concise summary that captures:");
            sb.AppendLine("1. The key topics discussed");
            sb.AppendLine("2. Important decisions or conclusions reached");
            sb.AppendLine("3. Any pending questions or tasks");
            sb.AppendLine("4. Relevant context that would be needed to continue the conversation");
            sb.AppendLine();
            sb.AppendLine("Format the summary as a coherent narrative that an AI assistant can use to continue helping the user.");
            sb.AppendLine("Be concise but ensure no critical information is lost.");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("CONVERSATION TO SUMMARIZE:");
            sb.AppendLine("---");
            sb.AppendLine();

            var historyList = conversationHistory?.ToList() ?? new List<IAIInteraction>();

            foreach (var interaction in historyList)
            {
                // Skip system messages, context, and the last user message
                if (interaction == null)
                {
                    continue;
                }

                if (interaction.Agent == AIAgent.System || interaction.Agent == AIAgent.Context)
                {
                    continue;
                }

                // Skip the last user message if provided (it will be sent separately after summary)
                if (lastUserMessage != null && ReferenceEquals(interaction, lastUserMessage))
                {
                    continue;
                }

                switch (interaction)
                {
                    case AIInteractionText text:
                        var roleName = text.Agent.ToDescription();
                        sb.AppendLine($"[{roleName}]: {text.Content}");
                        sb.AppendLine();
                        break;

                    case AIInteractionToolResult toolResult:
                        sb.AppendLine($"[Tool Result]: {toolResult.Name}");
                        if (toolResult.Result != null)
                        {
                            var resultStr = toolResult.Result.ToString();
                            // Truncate very long tool results
                            if (resultStr.Length > 500)
                            {
                                resultStr = resultStr.Substring(0, 500) + "... [truncated]";
                            }

                            sb.AppendLine($"Result: {resultStr}");
                        }

                        sb.AppendLine();
                        break;

                    case AIInteractionToolCall toolCall:
                        sb.AppendLine($"[Tool Call]: {toolCall.Name}");
                        if (toolCall.Arguments != null)
                        {
                            sb.AppendLine($"Arguments: {toolCall.Arguments}");
                        }

                        sb.AppendLine();
                        break;
                }
            }

            sb.AppendLine("---");
            sb.AppendLine("END OF CONVERSATION");
            sb.AppendLine("---");

            return new List<IAIInteraction>
            {
                new AIInteractionText
                {
                    Agent = AIAgent.System,
                    Content = "You are a helpful assistant that creates concise, accurate summaries of conversations. Your summaries preserve all essential information while reducing token count significantly. Do not add a main title or section headers. You can use ordered or unordered lists to organize information. Format your response as markdown.",
                },
                new AIInteractionText
                {
                    Agent = AIAgent.User,
                    Content = sb.ToString(),
                },
            };
        }
    }
}
