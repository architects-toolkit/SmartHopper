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

namespace SmartHopper.Infrastructure.AICall.Sessions.SpecialTurns.BuiltIn
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using SmartHopper.ProviderSdk.AICall.Core.Base;
    using SmartHopper.ProviderSdk.AICall.Core.Interactions;
    using SmartHopper.ProviderSdk.AIModels;
    /// <summary>
    /// Factory for the suggested-prompts special turn.
    /// Runs after a completed turn on a bounded digest of the recent history (not the full
    /// history), requires <see cref="AICapability.Text2Json"/>, and persists nothing so the
    /// suggestions never enter the conversation.
    /// </summary>
    public static class SuggestedPromptsSpecialTurn
    {
        /// <summary>Maximum number of suggestions the model may return.</summary>
        public const int MaxSuggestions = 3;

        /// <summary>JSON schema requested from structured-output capable providers.</summary>
        public const string JsonOutputSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""suggestions"": {
                    ""type"": ""array"",
                    ""items"": { ""type"": ""string"" },
                    ""maxItems"": 3
                }
            },
            ""required"": [""suggestions""]
        }";

        private const int MaxDigestInteractions = 8;
        private const int MaxTextChars = 2000;
        private const int MaxToolArgsChars = 300;
        private const int MaxToolResultChars = 500;

        /// <summary>
        /// Creates the special turn configuration for suggested-prompt generation.
        /// The model/provider are not overridden: the turn runs on the session's current model,
        /// which callers must gate for <see cref="AICapability.Text2Json"/> support beforehand.
        /// </summary>
        /// <param name="recentHistory">Recent conversation interactions to digest.</param>
        /// <returns>A configured special turn for suggested-prompt generation.</returns>
        public static SpecialTurnConfig Create(IEnumerable<IAIInteraction> recentHistory)
        {
            return new SpecialTurnConfig
            {
                TurnType = "suggested_prompts",
                OverrideInteractions = BuildInteractions(recentHistory),
                OverrideCapability = AICapability.Text2Json,
                OverrideContextFilter = "-*",
                OverrideToolFilter = "-*",
                OverrideJsonOutputSchema = JsonOutputSchema,
                ProcessTools = false,
                ForceNonStreaming = true,
                TimeoutMs = 20000,
                PersistenceStrategy = HistoryPersistenceStrategy.Ephemeral,
                Metadata = new Dictionary<string, object>
                {
                    ["is_suggested_prompts"] = true,
                },
            };
        }

        /// <summary>
        /// Flattens the last <see cref="MaxDigestInteractions"/> meaningful interactions into a
        /// single user message: text is truncated, tool calls collapse to their name and bounded
        /// arguments, tool results to name and bounded output. System/context entries are excluded.
        /// </summary>
        private static List<IAIInteraction> BuildInteractions(IEnumerable<IAIInteraction> recentHistory)
        {
            var meaningful = (recentHistory ?? Enumerable.Empty<IAIInteraction>())
                .Where(i => i != null && i.Agent != AIAgent.System && i.Agent != AIAgent.Context)
                .TakeLast(MaxDigestInteractions)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("Based on the following recent conversation digest, suggest follow-up prompts the user might send next.");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("RECENT CONVERSATION:");
            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var interaction in meaningful)
            {
                switch (interaction)
                {
                    case AIInteractionToolResult toolResult:
                        sb.AppendLine($"[Tool Result]: {toolResult.Name}");
                        if (toolResult.Result != null)
                        {
                            sb.AppendLine($"Result: {Truncate(toolResult.Result.ToString(), MaxToolResultChars)}");
                        }

                        sb.AppendLine();
                        break;

                    case AIInteractionToolCall toolCall:
                        sb.AppendLine($"[Tool Call]: {toolCall.Name}");
                        if (toolCall.Arguments != null)
                        {
                            sb.AppendLine($"Arguments: {Truncate(toolCall.Arguments.ToString(), MaxToolArgsChars)}");
                        }

                        sb.AppendLine();
                        break;

                    case AIInteractionText text when !string.IsNullOrWhiteSpace(text.Content):
                        sb.AppendLine($"[{text.Agent.ToDescription()}]: {Truncate(text.Content, MaxTextChars)}");
                        sb.AppendLine();
                        break;
                }
            }

            sb.AppendLine("---");
            sb.AppendLine("END OF CONVERSATION");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"Return JSON only, matching the required schema: an object with a \"suggestions\" array of 0 to {MaxSuggestions} strings. " +
                "Each suggestion is a short, complete instruction phrased exactly as the user would type it (at most ~100 characters). " +
                "Suggestions must be specific to this conversation and continue it naturally. " +
                "Return an empty suggestions array when no natural follow-up exists.");

            return new List<IAIInteraction>
            {
                new AIInteractionText
                {
                    Agent = AIAgent.System,
                    Content = "You generate short follow-up prompt suggestions for a chat assistant that helps with Grasshopper and Rhino canvas work. You answer with JSON only.",
                },
                new AIInteractionText
                {
                    Agent = AIAgent.User,
                    Content = sb.ToString(),
                },
            };
        }

        private static string Truncate(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxChars) + "... [truncated]";
        }
    }
}
