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
    public static class AIBodyExtensions
    {
        /// <summary>
        /// Gets the last interaction in the immutable body, or null if none.
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <returns>The last interaction, or null if there are no interactions.</returns>
        public static IAIInteraction GetLastInteraction(this AIBody body)
        {
            return body?.Interactions?.LastOrDefault();
        }

        /// <summary>
        /// Gets the last interaction matching the specified agent, or null if none.
        /// Mirrors legacy AIBody.GetLastInteraction(AIAgent).
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <param name="agent">Agent to filter interactions by.</param>
        /// <returns>The last matching interaction, or null if none exist.</returns>
        public static IAIInteraction GetLastInteraction(this AIBody body, AIAgent agent)
        {
            return body?.Interactions?.LastOrDefault(i => i.Agent == agent);
        }

        /// <summary>
        /// Gets the last interaction whose agent name matches the provided string, or null if none.
        /// Mirrors legacy AIBody.GetLastInteraction(string).
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <param name="agent">Agent name to filter by.</param>
        /// <returns>The last matching interaction, or null if none exist.</returns>
        public static IAIInteraction GetLastInteraction(this AIBody body, string agent)
        {
            return body?.Interactions?.LastOrDefault(i => i.Agent.ToString() == agent);
        }

        /// <summary>
        /// Gets the content of the last text interaction in the body.
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <returns>The text content of the last AIInteractionText, or null if no text interaction exists.</returns>
        public static string GetLastText(this AIBody body)
        {
            return body?.Interactions?.LastOrDefault(i => i is AIInteractionText) is AIInteractionText textInteraction 
                ? textInteraction.Content 
                : null;
        }

        /// <summary>
        /// Computes the number of pending tool calls by matching tool call Ids
        /// against tool result Ids in the interactions list.
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <returns>The number of pending tool calls.</returns>
        public static int PendingToolCallsCount(this AIBody body)
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
        /// <param name="body">The AI body to query.</param>
        /// <returns>A list of tool calls without corresponding results.</returns>
        public static List<AIInteractionToolCall> PendingToolCallsList(this AIBody body)
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
        /// <param name="body">The AI body to mutate.</param>
        /// <param name="interaction">The interaction to append.</param>
        /// <returns>A new immutable body including the appended interaction.</returns>
        public static AIBody WithAppended(this AIBody body, IAIInteraction interaction)
        {
            // When mutating an existing immutable body for session history, clear previous 'new' markers
            // so only the newly appended item is considered new.
            var builder = AIBodyBuilder.FromImmutable(body).ClearNewMarkers();
            builder.Add(interaction);
            return builder.Build();
        }

        /// <summary>
        /// Returns a new immutable body with the provided interactions appended.
        /// </summary>
        /// <param name="body">The AI body to mutate.</param>
        /// <param name="interactions">The interactions to append.</param>
        /// <returns>A new immutable body including the appended interactions.</returns>
        public static AIBody WithAppendedRange(this AIBody body, IEnumerable<IAIInteraction> interactions)
        {
            // Clear previous 'new' markers so only the appended range is considered new
            var builder = AIBodyBuilder.FromImmutable(body).ClearNewMarkers();
            builder.AddRange(interactions);
            return builder.Build();
        }

        /// <summary>
        /// Returns the interactions that were newly added or replaced in the last mutation
        /// that produced this immutable body, based on <see cref="AIBody.InteractionsNew"/>.
        /// </summary>
        /// <param name="body">The AI body to query.</param>
        /// <returns>A list of interactions that were marked as new or replaced.</returns>
        public static List<IAIInteraction> GetNewInteractions(this AIBody body)
        {
            var result = new List<IAIInteraction>();
            if (body == null || body.Interactions == null || body.Interactions.Count == 0)
            {
                return result;
            }

            var indices = body.InteractionsNew ?? new List<int>();
            foreach (var idx in indices)
            {
                if (idx >= 0 && idx < body.Interactions.Count)
                {
                    var it = body.Interactions[idx];
                    if (it != null)
                    {
                        result.Add(it);
                    }
                }
            }

            return result;
        }
    }
}
