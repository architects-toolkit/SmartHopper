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
using System.Threading;

namespace SmartHopper.Infrastructure.AICall.Sessions
{
    using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;

    /// <summary>
    /// Observer of conversation session lifecycle and streaming deltas.
    /// Provides comprehensive event notifications for all conversation stages.
    /// </summary>
    public interface IConversationObserver
    {
        /// <summary>
        /// Called when the conversation session starts.
        /// </summary>
        /// <param name="request"></param>
        void OnStart(AIRequestCall request);

        /// <summary>
        /// Called for streaming text deltas during live response generation.
        /// Used for live UI updates of partial text content.
        /// </summary>
        /// <param name="interaction">The partial interaction being streamed.</param>
        void OnDelta(IAIInteraction interaction);

        /// <summary>
        /// Called when an interaction is completed, but there will be more.
        /// </summary>
        /// <param name="interaction">The completed partial interaction.</param>
        void OnPartial(IAIInteraction interaction);

        /// <summary>
        /// Called when a tool call is made.
        /// </summary>
        /// <param name="toolCall"></param>
        void OnToolCall(AIInteractionToolCall toolCall);

        /// <summary>
        /// Called when a tool result is returned.
        /// </summary>
        /// <param name="toolResult"></param>
        void OnToolResult(AIInteractionToolResult toolResult);

        /// <summary>
        /// Called when the final result is available and the conversation is stable.
        /// </summary>
        /// <param name="finalResult"></param>
        void OnFinal(AIReturn finalResult);

        /// <summary>
        /// Called when an error occurs during the conversation.
        /// </summary>
        /// <param name="error"></param>
        void OnError(Exception error);
    }
}

