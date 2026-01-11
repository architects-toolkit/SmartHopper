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

using System;

namespace SmartHopper.Infrastructure.AICall.Sessions
{
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AICall.Core.Requests;
    using SmartHopper.Infrastructure.AICall.Core.Returns;

    /// <summary>
    /// Observer of conversation session lifecycle and streaming deltas.
    /// Provides comprehensive event notifications for all conversation stages.
    /// </summary>
    public interface IConversationObserver
    {
        /// <summary>
        /// Called when the conversation session starts.
        /// </summary>
        /// <param name="request">The request that is about to be executed by the conversation session.</param>
        void OnStart(AIRequestCall request);

        /// <summary>
        /// Called for streaming text deltas during live response generation.
        /// Used for live UI updates of partial text content.
        /// </summary>
        /// <param name="interaction">The partial interaction being streamed (e.g., a text chunk).</param>
        void OnDelta(IAIInteraction interaction);

        /// <summary>
        /// Called when an interaction is completed, but there will be more.
        /// </summary>
        /// <param name="interaction">The interaction that has been completed and persisted to history.</param>
        void OnInteractionCompleted(IAIInteraction interaction);

        /// <summary>
        /// Called when a tool call is made.
        /// </summary>
        /// <param name="toolCall">The tool call interaction that has been requested.</param>
        void OnToolCall(AIInteractionToolCall toolCall);

        /// <summary>
        /// Called when a tool result is returned.
        /// </summary>
        /// <param name="toolResult">The tool result interaction returned by the executed tool.</param>
        void OnToolResult(AIInteractionToolResult toolResult);

        /// <summary>
        /// Called when the final result is available and the conversation is stable.
        /// </summary>
        /// <param name="finalResult">The final <see cref="AIReturn"/> representing the stable conversation state.</param>
        void OnFinal(AIReturn finalResult);

        /// <summary>
        /// Called when an error occurs during the conversation.
        /// </summary>
        /// <param name="error">The exception describing the error condition.</param>
        void OnError(Exception error);
    }
}
