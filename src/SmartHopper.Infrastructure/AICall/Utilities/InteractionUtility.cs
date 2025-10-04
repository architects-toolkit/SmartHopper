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
using SmartHopper.Infrastructure.AICall.Core.Interactions;

namespace SmartHopper.Infrastructure.AICall.Utilities
{
    /// <summary>
    /// Shared utilities for working with AI interactions.
    /// </summary>
    public static class InteractionUtility
    {
        /// <summary>
        /// Generates a new unique turn identifier using GUID format (32 hex characters, no hyphens).
        /// </summary>
        /// <returns>A unique turn ID string.</returns>
        public static string GenerateTurnId() => Guid.NewGuid().ToString("N");

        /// <summary>
        /// Ensures all interactions in a collection share the same turn identifier.
        /// Skips null interactions and does nothing if the turnId is null or empty.
        /// </summary>
        /// <param name="interactions">The interactions to update.</param>
        /// <param name="turnId">The turn ID to assign to all interactions.</param>
        public static void EnsureTurnId(IEnumerable<IAIInteraction> interactions, string turnId)
        {
            if (string.IsNullOrWhiteSpace(turnId) || interactions == null)
            {
                return;
            }

            foreach (var interaction in interactions)
            {
                if (interaction == null)
                {
                    continue;
                }

                interaction.TurnId = turnId;
            }
        }
    }
}
