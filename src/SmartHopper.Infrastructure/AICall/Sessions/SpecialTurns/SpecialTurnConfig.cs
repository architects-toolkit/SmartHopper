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
    using System;
    using System.Collections.Generic;
    using SmartHopper.Infrastructure.AICall.Core.Interactions;
    using SmartHopper.Infrastructure.AIModels;

    /// <summary>
    /// Configuration for executing a special turn with custom overrides.
    /// Special turns are executed through the regular conversation flow but can override
    /// interactions, provider settings, tools, and history persistence behavior.
    /// </summary>
    public class SpecialTurnConfig
    {
        /// <summary>
        /// Gets or sets the interactions to use for this special turn.
        /// When set, these replace the current conversation history for the turn execution.
        /// </summary>
        public List<IAIInteraction>? OverrideInteractions { get; set; }

        /// <summary>
        /// Gets or sets the provider name to use for this special turn.
        /// When null, uses the session's configured provider.
        /// </summary>
        public string? OverrideProvider { get; set; }

        /// <summary>
        /// Gets or sets the model name to use for this special turn.
        /// When null, uses the session's configured model.
        /// </summary>
        public string? OverrideModel { get; set; }

        /// <summary>
        /// Gets or sets the endpoint to use for this special turn.
        /// When null, uses the session's configured endpoint.
        /// </summary>
        public string? OverrideEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the capability requirements for this special turn.
        /// When null, uses the session's configured capability.
        /// </summary>
        public AICapability? OverrideCapability { get; set; }

        /// <summary>
        /// Gets or sets the context filter for this special turn.
        /// Controls which context providers are active.
        /// </summary>
        public string? OverrideContextFilter { get; set; }

        /// <summary>
        /// Gets or sets the tool filter for this special turn.
        /// Controls which tools are available during execution.
        /// </summary>
        public string? OverrideToolFilter { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether tool calls should be processed during this special turn.
        /// </summary>
        public bool ProcessTools { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to force non-streaming execution.
        /// When true, the turn will always use non-streaming mode regardless of the caller's preference.
        /// Useful for operations like image generation that don't support streaming.
        /// </summary>
        public bool ForceNonStreaming { get; set; } = false;

        /// <summary>
        /// Gets or sets the timeout in milliseconds for this special turn.
        /// When null, uses the default session timeout.
        /// </summary>
        public int? TimeoutMs { get; set; }

        /// <summary>
        /// Gets or sets the history persistence strategy for this special turn.
        /// Controls how the turn's interactions are persisted to the conversation history.
        /// </summary>
        public HistoryPersistenceStrategy PersistenceStrategy { get; set; } = HistoryPersistenceStrategy.PersistResult;

        /// <summary>
        /// Gets or sets the filter for controlling which interaction types are persisted.
        /// Used with PersistAll and ReplaceAbove strategies.
        /// </summary>
        public InteractionFilter? PersistenceFilter { get; set; }

        /// <summary>
        /// Gets or sets a descriptive name for the turn type (e.g., "greeting", "summary").
        /// </summary>
        public string? TurnType { get; set; }

        /// <summary>
        /// Gets or sets additional metadata for the special turn.
        /// Can be used to store custom data for logging or processing.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
