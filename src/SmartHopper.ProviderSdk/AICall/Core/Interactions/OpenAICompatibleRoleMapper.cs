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

using SmartHopper.ProviderSdk.AICall.Core.Base;

namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Maps <see cref="AIAgent"/> values to OpenAI-compatible chat message roles.
    /// This is the default mapping used by providers that target the
    /// <c>/v1/chat/completions</c> endpoint shape.
    /// </summary>
    public static class OpenAICompatibleRoleMapper
    {
        /// <summary>
        /// Maps an <see cref="AIAgent"/> to an OpenAI-compatible role string.
        /// Returns <c>null</c> for agents that have no provider-facing role
        /// (e.g., UI-only diagnostics or unknown agents).
        /// </summary>
        /// <param name="agent">The agent to map.</param>
        /// <returns>
        /// The OpenAI-compatible role ("system", "user", "assistant" or "tool"),
        /// or <c>null</c> when the agent should not be sent to the provider.
        /// </returns>
        public static string? MapRole(AIAgent agent)
        {
            return agent switch
            {
                AIAgent.System => "system",
                AIAgent.Context => "system",
                AIAgent.User => "user",
                AIAgent.Assistant => "assistant",
                AIAgent.ToolCall => "assistant",
                AIAgent.ToolResult => "tool",
                _ => null,
            };
        }

        /// <summary>
        /// Maps an <see cref="AIAgent"/> to an OpenAI-compatible role string,
        /// throwing <see cref="System.ArgumentException"/> for unsupported agents.
        /// Useful for providers that fail fast rather than silently skipping
        /// unknown interactions.
        /// </summary>
        /// <param name="agent">The agent to map.</param>
        /// <param name="providerName">The provider name included in the exception message.</param>
        /// <returns>The OpenAI-compatible role.</returns>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="agent"/> cannot be mapped to an OpenAI-compatible role.
        /// </exception>
        public static string MapRoleOrThrow(AIAgent agent, string providerName)
        {
            var role = MapRole(agent);
            if (role != null)
            {
                return role;
            }

            throw new System.ArgumentException($"Agent {agent} is not supported by {providerName}");
        }
    }
}
