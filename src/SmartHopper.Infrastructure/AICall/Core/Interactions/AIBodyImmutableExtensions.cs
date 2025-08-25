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
using System.Collections.Generic;
using System.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Extension helpers for immutable AI request bodies.
    /// Provides pending tool call detection and common queries without mutation.
    /// </summary>
    public static class AIBodyImmutableExtensions
    {
        /// <summary>
        /// Gets the last interaction in the immutable body, or null if none.
        /// </summary>
        public static IAIInteraction GetLastInteraction(this AIBodyImmutable body)
        {
            return body?.Interactions?.LastOrDefault();
        }

        /// <summary>
        /// Gets the last interaction matching the specified agent, or null if none.
        /// Mirrors legacy AIBody.GetLastInteraction(AIAgent).
        /// </summary>
        public static IAIInteraction GetLastInteraction(this AIBodyImmutable body, AIAgent agent)
        {
            return body?.Interactions?.LastOrDefault(i => i.Agent == agent);
        }

        /// <summary>
        /// Gets the last interaction whose agent name matches the provided string, or null if none.
        /// Mirrors legacy AIBody.GetLastInteraction(string).
        /// </summary>
        public static IAIInteraction GetLastInteraction(this AIBodyImmutable body, string agent)
        {
            return body?.Interactions?.LastOrDefault(i => i.Agent.ToString() == agent);
        }

        /// <summary>
        /// Computes the number of pending tool calls by matching tool call Ids
        /// against tool result Ids in the interactions list.
        /// </summary>
        public static int PendingToolCallsCount(this AIBodyImmutable body)
        {
            if (body?.Interactions == null || body.Interactions.Count == 0)
            {
                return 0;
            }

            var toolCalls = body.Interactions.OfType<AIInteractionToolCall>();
            var toolResults = body.Interactions.OfType<AIInteractionToolResult>();
            var resultIds = new HashSet<string>(toolResults.Select(tr => tr.Id), StringComparer.Ordinal);

            int matched = toolCalls.Count(tc => resultIds.Contains(tc.Id));
            int pending = toolCalls.Count() - matched;
            return pending;
        }

        /// <summary>
        /// Gets the list of pending tool calls by matching tool call Ids against
        /// tool result Ids in the interactions list.
        /// </summary>
        public static List<AIInteractionToolCall> PendingToolCallsList(this AIBodyImmutable body)
        {
            if (body?.Interactions == null || body.Interactions.Count == 0)
            {
                return new List<AIInteractionToolCall>();
            }

            var toolCalls = body.Interactions.OfType<AIInteractionToolCall>();
            var toolResults = body.Interactions.OfType<AIInteractionToolResult>();
            var resultIds = new HashSet<string>(toolResults.Select(tr => tr.Id), StringComparer.Ordinal);

            return toolCalls.Where(tc => !resultIds.Contains(tc.Id)).ToList();
        }

        /// <summary>
        /// Returns a new immutable body with the provided interaction appended.
        /// </summary>
        public static AIBodyImmutable WithAppended(this AIBodyImmutable body, IAIInteraction interaction)
        {
            var builder = AIBodyBuilder.FromImmutable(body);
            builder.Add(interaction);
            return builder.Build();
        }

        /// <summary>
        /// Returns a new immutable body with the provided interactions appended.
        /// </summary>
        public static AIBodyImmutable WithAppendedRange(this AIBodyImmutable body, IEnumerable<IAIInteraction> interactions)
        {
            var builder = AIBodyBuilder.FromImmutable(body);
            builder.AddRange(interactions);
            return builder.Build();
        }
    }
}
