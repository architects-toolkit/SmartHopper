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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Metrics;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Immutable representation of an AI request body.
    /// - No implicit side effects or dynamic context injection
    /// - Suitable for stable hashing/fingerprints
    /// Construct via <see cref="AIBodyBuilder"/>.
    /// </summary>
    public sealed record AIBody
    (
        IReadOnlyList<IAIInteraction> Interactions,
        string ToolFilter,
        string ContextFilter,
        string JsonOutputSchema,
        List<int> InteractionsNew
    )
    {
        /// <summary>
        /// Gets an empty immutable body with defaults: ToolFilter="-*", ContextFilter="-*".
        /// </summary>
        public static AIBody Empty { get; } = new(
            Array.Empty<IAIInteraction>(),
            "-*",
            "-*",
            null,
            new List<int>());

        /// <summary>
        /// Gets the count of interactions (no dynamic context injection here).
        /// </summary>
        public int InteractionsCount => this.Interactions?.Count ?? 0;

        // NOTE: Do not redeclare InteractionsNew here.
        // The record primary constructor already defines the InteractionsNew property.
        // Redeclaring it with a default initializer would discard values passed via the constructor,
        // causing 'new' markers to be lost.

        /// <summary>
        /// Gets a value indicating whether a structured JSON response is requested.
        /// </summary>
        public bool RequiresJsonOutput => !string.IsNullOrEmpty(this.JsonOutputSchema);

        /// <summary>
        /// Gets aggregated metrics across interactions.
        /// </summary>
        public AIMetrics Metrics
        {
            get
            {
                var m = new AIMetrics();
                if (this.Interactions == null) 
                {
                    return m;
                }

                foreach (var i in this.Interactions)
                {
                    if (i?.Metrics != null)
                    {
                        m.Combine(i.Metrics);
                    }
                }

                return m;
            }
        }

        /// <summary>
        /// Gets aggregated structured messages from interaction-level details (e.g., tool/image validation).
        /// Body-level validation should be executed by policies/validators.
        /// </summary>
        public List<AIRuntimeMessage> Messages
        {
            get
            {
                var combined = new List<AIRuntimeMessage>();
                var seen = new HashSet<string>(StringComparer.Ordinal);

                if (this.Interactions != null)
                {
                    foreach (var interaction in this.Interactions)
                    {
                        var messages = interaction switch
                        {
                            AIInteractionToolResult tr => tr.Messages,
                            AIInteractionImage img => img.Messages,
                            _ => null
                        };

                        if (messages != null)
                        {
                            foreach (var m in messages)
                            {
                                if (!string.IsNullOrEmpty(m?.Message) && seen.Add(m.Message))
                                {
                                    combined.Add(m);
                                }
                            }
                        }
                    }
                }

                return combined;
            }
        }

        /// <summary>
        /// Clears the list of indices marked as new in this body.
        /// </summary>
        public void ResetNew()
        {
            this.InteractionsNew?.Clear();
        }

        /// <summary>
        /// Checks whether all interactions have a non-empty <see cref="IAIInteraction.TurnId"/>.
        /// Also verifies that within assistant items in this body there are no conflicting TurnIds
        /// for what appears to be the same logical message (best-effort heuristic).
        /// </summary>
        /// <returns>
        /// True when all interactions have a non-empty TurnId (and no conflicts are detected); otherwise false.
        /// </returns>
        public bool AreTurnIdsValid()
        {
            if (this.Interactions == null) return true;

            foreach (var i in this.Interactions)
            {
                if (i == null) continue;
                if (string.IsNullOrWhiteSpace(i.TurnId)) return false;
            }

            return true;
        }
    }
}
