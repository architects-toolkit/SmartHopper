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

namespace SmartHopper.Core.ComponentBase.Cores
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Grasshopper-free decision helpers for batch sentinel resolution. Kept
    /// separate from <see cref="UnitResultStore"/> so the resolvability contract
    /// — which drives the direct-vs-collect-only import choice — stays unit
    /// testable without Grasshopper references.
    /// </summary>
    public static class SentinelResolution
    {
        /// <summary>
        /// Returns true when a decoded result for <paramref name="customId"/>
        /// could be written into a live fragment right now: the live
        /// customId → unitKey map holds the id and a pending fragment exists
        /// for the mapped unit. An already-resolved id reports false —
        /// re-resolution is a dedup no-op that keeps the first result, so an
        /// import targeting it cannot place its data in place and must rebuild
        /// a fresh mapping through the collect-only path instead.
        /// </summary>
        /// <param name="customId">Batch custom identifier.</param>
        /// <param name="customIdToUnitKey">Live customId → unitKey map.</param>
        /// <param name="resolvedIds">CustomIds already resolved this run.</param>
        /// <param name="hasPendingFragment">Probe reporting whether a unit key has a pending run fragment.</param>
        public static bool CanResolve(
            string customId,
            IReadOnlyDictionary<string, string> customIdToUnitKey,
            ISet<string> resolvedIds,
            Func<string, bool> hasPendingFragment)
        {
            if (string.IsNullOrEmpty(customId)
                || (resolvedIds != null && resolvedIds.Contains(customId)))
            {
                return false;
            }

            return customIdToUnitKey != null
                && customIdToUnitKey.TryGetValue(customId, out var unitKey)
                && unitKey != null
                && hasPendingFragment != null
                && hasPendingFragment(unitKey);
        }
    }
}
