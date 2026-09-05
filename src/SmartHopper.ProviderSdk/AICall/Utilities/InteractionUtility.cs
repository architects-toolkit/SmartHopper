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
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
namespace SmartHopper.ProviderSdk.AICall.Utilities
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
        /// Returns a new sequence where every non-null interaction carries the supplied
        /// <paramref name="turnId"/>, replacing any pre-existing turn identifier.
        /// Does nothing if the turnId is null or empty.
        /// </summary>
        /// <remarks>
        /// Turn identifiers are owned by the conversation session, not by providers. Provider-produced
        /// bodies (via <c>AIReturn.SetBody</c>) are built without a default turn ID, so
        /// <c>AIBodyBuilder.Build()</c> stamps each interaction with a fresh random GUID. This method
        /// must therefore overwrite unconditionally; an "assign only if missing" policy would silently
        /// leave every provider interaction in its own random turn.
        /// </remarks>
        /// <param name="interactions">The interactions to update.</param>
        /// <param name="turnId">The turn ID to assign to all interactions.</param>
        /// <returns>A sequence of interactions sharing the given turn identifier.</returns>
        public static IEnumerable<IAIInteraction> EnsureTurnId(IEnumerable<IAIInteraction> interactions, string turnId)
        {
            if (string.IsNullOrWhiteSpace(turnId) || interactions == null)
            {
                return interactions;
            }

            return interactions.Select(interaction =>
                interaction == null || string.Equals(interaction.TurnId, turnId, StringComparison.Ordinal)
                    ? interaction
                    : interaction.WithTurnId(turnId));
        }
    }
}
