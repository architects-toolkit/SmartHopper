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
    using SmartHopper.Infrastructure.AICall;

    /// <summary>
    /// Observer of conversation session lifecycle and streaming deltas.
    /// Non-streaming minimal contract; streaming will be added in Suggestion 9.
    /// </summary>
    public interface IConversationObserver
    {
        void OnStart(AIRequestCall request);

        void OnPartial(AIReturn delta);

        void OnToolCall(AIInteractionToolCall toolCall);

        void OnToolResult(AIInteractionToolResult toolResult);

        void OnFinal(AIReturn finalResult);

        void OnError(Exception error);
    }
}
