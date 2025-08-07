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
    /// <summary>
    /// Specifies the originator of an AI interaction.
    /// </summary>
    public enum AIAgent
    {
        /// <summary>The context provided by Context Manager.</summary>
        Context,

        /// <summary>The system (e.g. system prompts).</summary>
        System,

        /// <summary>A user message.</summary>
        User,

        /// <summary>The AI assistant response.</summary>
        Assistant,

        /// <summary>A tool call.</summary>
        ToolCall,

        /// <summary>A tool result.</summary>
        ToolResult,

        /// <summary>Unknown agent.</summary>
        Unknown,
    }

    /// <summary>
    /// Extension methods for AIAgent.
    /// </summary>
    public static class AIAgentExtensions
    {
        /// <summary>
        /// Converts an AIAgent to a string.
        /// </summary>
        /// <param name="agent">The agent to convert.</param>
        /// <returns>The string representation of the agent.</returns>
        public static string ToString(this AIAgent agent)
        {
            return agent switch
            {
                AIAgent.Context => "context",
                AIAgent.System => "system",
                AIAgent.User => "user",
                AIAgent.Assistant => "assistant",
                AIAgent.ToolCall => "tool_call",
                AIAgent.ToolResult => "tool",
                _ => "unknown",
            };
        }

        /// <summary>
        /// Converts an AIAgent to a description.
        /// </summary>
        /// <param name="agent">The agent to convert.</param>
        /// <returns>The description of the agent.</returns>
        public static string ToDescription(this AIAgent agent)
        {
            return agent switch
            {
                AIAgent.Context => "Context",
                AIAgent.System => "System",
                AIAgent.User => "User",
                AIAgent.Assistant => "Assistant",
                AIAgent.ToolCall => "Tool Call",
                AIAgent.ToolResult => "Tool Result",
                _ => "Unknown",
            };
        }

        /// <summary>
        /// Converts a string to an AIAgent.
        /// </summary>
        /// <param name="agent">The string to convert.</param>
        /// <returns>The AIAgent.</returns>
        public static AIAgent FromString(string agent)
        {
            return agent switch
            {
                "context" => AIAgent.Context,
                "system" => AIAgent.System,
                "user" => AIAgent.User,
                "assistant" => AIAgent.Assistant,
                "tool_call" => AIAgent.ToolCall,
                "tool" => AIAgent.ToolResult,
                _ => AIAgent.Unknown,
            };
        }
    }
}
