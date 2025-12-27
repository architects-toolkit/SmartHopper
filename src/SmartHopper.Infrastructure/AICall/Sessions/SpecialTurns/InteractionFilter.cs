/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Infrastructure.AICall.Sessions.SpecialTurns
{
    using System.Collections.Generic;
    using SmartHopper.Infrastructure.AICall.Core.Base;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;

    /// <summary>
    /// Filter to control which interaction types are included/excluded during persistence.
    /// Uses an allowlist/blocklist approach for flexible filtering.
    /// </summary>
    public class InteractionFilter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InteractionFilter"/> class.
        /// </summary>
        public InteractionFilter()
        {
            this.AllowedAgents = new HashSet<AIAgent>();
            this.BlockedAgents = new HashSet<AIAgent>();
        }

        /// <summary>
        /// Gets or sets the set of explicitly allowed agent types.
        /// If empty, all agents are allowed by default (unless blocked).
        /// </summary>
        public HashSet<AIAgent> AllowedAgents { get; set; }

        /// <summary>
        /// Gets or sets the set of explicitly blocked agent types.
        /// Blocked agents take precedence over allowed agents.
        /// </summary>
        public HashSet<AIAgent> BlockedAgents { get; set; }

        /// <summary>
        /// Gets the default filter (includes conversation interactions, excludes system/context).
        /// </summary>
        public static InteractionFilter Default =>
            InteractionFilter.Allow(AIAgent.User, AIAgent.Assistant, AIAgent.ToolCall, AIAgent.ToolResult);

        /// <summary>
        /// Gets a filter for preserving system/context while replacing conversation.
        /// Used with ReplaceAbove strategy to keep system prompts and context.
        /// </summary>
        public static InteractionFilter PreserveSystemContext =>
            InteractionFilter.Block(AIAgent.System, AIAgent.Context);

        /// <summary>
        /// Gets a filter that allows all interaction types.
        /// </summary>
        public static InteractionFilter AllowAll => new InteractionFilter();

        /// <summary>
        /// Creates a filter that allows only the specified agent types.
        /// </summary>
        /// <param name="agents">The agent types to allow.</param>
        /// <returns>A new InteractionFilter configured with the allowed agents.</returns>
        public static InteractionFilter Allow(params AIAgent[] agents)
        {
            return new InteractionFilter
            {
                AllowedAgents = new HashSet<AIAgent>(agents),
            };
        }

        /// <summary>
        /// Creates a filter that blocks the specified agent types.
        /// All other types are allowed.
        /// </summary>
        /// <param name="agents">The agent types to block.</param>
        /// <returns>A new InteractionFilter configured with the blocked agents.</returns>
        public static InteractionFilter Block(params AIAgent[] agents)
        {
            return new InteractionFilter
            {
                BlockedAgents = new HashSet<AIAgent>(agents),
            };
        }

        /// <summary>
        /// Adds an agent type to the allowlist.
        /// </summary>
        /// <param name="agent">The agent type to allow.</param>
        /// <returns>This filter for fluent chaining.</returns>
        public InteractionFilter WithAllow(AIAgent agent)
        {
            this.AllowedAgents.Add(agent);
            return this;
        }

        /// <summary>
        /// Adds multiple agent types to the allowlist.
        /// </summary>
        /// <param name="agents">The agent types to allow.</param>
        /// <returns>This filter for fluent chaining.</returns>
        public InteractionFilter WithAllow(params AIAgent[] agents)
        {
            foreach (var agent in agents)
            {
                this.AllowedAgents.Add(agent);
            }

            return this;
        }

        /// <summary>
        /// Adds an agent type to the blocklist.
        /// </summary>
        /// <param name="agent">The agent type to block.</param>
        /// <returns>This filter for fluent chaining.</returns>
        public InteractionFilter WithBlock(AIAgent agent)
        {
            this.BlockedAgents.Add(agent);
            return this;
        }

        /// <summary>
        /// Adds multiple agent types to the blocklist.
        /// </summary>
        /// <param name="agents">The agent types to block.</param>
        /// <returns>This filter for fluent chaining.</returns>
        public InteractionFilter WithBlock(params AIAgent[] agents)
        {
            foreach (var agent in agents)
            {
                this.BlockedAgents.Add(agent);
            }

            return this;
        }

        /// <summary>
        /// Determines whether the specified interaction should be included based on the filter settings.
        /// </summary>
        /// <param name="interaction">The interaction to evaluate.</param>
        /// <returns>True if the interaction should be included; otherwise, false.</returns>
        public bool ShouldInclude(IAIInteraction interaction)
        {
            if (interaction == null)
            {
                return false;
            }

            var agent = interaction.Agent;

            // Blocklist takes precedence
            if (this.BlockedAgents.Contains(agent))
            {
                return false;
            }

            // If allowlist is empty, allow by default (unless blocked above)
            if (this.AllowedAgents.Count == 0)
            {
                return true;
            }

            // If allowlist is specified, check if agent is in it
            return this.AllowedAgents.Contains(agent);
        }
    }
}
