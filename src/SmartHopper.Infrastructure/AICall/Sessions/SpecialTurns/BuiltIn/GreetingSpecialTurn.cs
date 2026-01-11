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
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AIModels;

    /// <summary>
    /// Factory for creating greeting special turn configurations.
    /// </summary>
    public static class GreetingSpecialTurn
    {
        /// <summary>
        /// Creates a special turn configuration for generating an AI greeting.
        /// </summary>
        /// <param name="providerName">The provider to use for greeting generation.</param>
        /// <param name="systemPrompt">Optional system prompt from the main conversation to contextualize the greeting.</param>
        /// <returns>A configured special turn for greeting generation.</returns>
        public static SpecialTurnConfig Create(string providerName, string? systemPrompt = null)
        {
            var greetingInteractions = BuildGreetingInteractions(systemPrompt);
            var defaultModel = ModelManager.Instance.GetDefaultModel(providerName, AICapability.Text2Text);

            return new SpecialTurnConfig
            {
                TurnType = "greeting",
                OverrideInteractions = greetingInteractions,
                OverrideModel = defaultModel,
                OverrideCapability = AICapability.Text2Text,
                OverrideToolFilter = "-*",
                ProcessTools = false,
                TimeoutMs = 30000, // 30 second timeout for greeting
                PersistenceStrategy = HistoryPersistenceStrategy.PersistResult,
                Metadata = new Dictionary<string, object>
                {
                    ["is_greeting"] = true,
                },
            };
        }

        /// <summary>
        /// Builds the interaction list for greeting generation.
        /// </summary>
        /// <param name="systemPrompt">Optional system prompt to contextualize the greeting.</param>
        /// <returns>List of interactions for greeting generation.</returns>
        private static List<IAIInteraction> BuildGreetingInteractions(string? systemPrompt)
        {
            string greetingPrompt;
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                greetingPrompt = $"You are a chat assistant. The user has provided the following instructions:\n---\n{systemPrompt}\n---\nBased on the instructions, generate a brief, friendly greeting message that welcomes the user to the chat and naturally guides the conversation toward your area of expertise. Be warm and professional, highlighting your unique capabilities without overwhelming the user with technical details. Keep it concise and engaging. One or two sentences maximum.";
            }
            else
            {
                greetingPrompt = "Your job is to generate a brief, friendly greeting message that welcomes the user to the chat. This is a generic purpose chat. Keep the greeting concise: one or two sentences maximum.";
            }

            return new List<IAIInteraction>
            {
                new AIInteractionText
                {
                    Agent = AIAgent.System,
                    Content = greetingPrompt,
                },
                new AIInteractionText
                {
                    Agent = AIAgent.User,
                    Content = "Please send a short friendly greeting to start the chat. Keep it to one or two sentences.",
                },
            };
        }
    }
}
