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
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Core.ComponentBase.Batch
{
    /// <summary>
    /// Centralized helpers for the <c>##SH_BATCH:{customId}##</c> placeholder protocol used by
    /// <see cref="AIStatefulAsyncComponentBase"/> to mark queued batch requests in output trees.
    /// </summary>
    /// <remarks>
    /// Single source of truth for the sentinel format. Replaces the previously duplicated
    /// <c>const string sentinelPrefix = "##SH_BATCH:"</c> / <c>sentinelSuffix = "##"</c> declarations
    /// scattered across the AI components and the base class.
    /// </remarks>
    public static class BatchSentinel
    {
        /// <summary>Prefix marker. Always at the start of a sentinel string.</summary>
        public const string Prefix = "##SH_BATCH:";

        /// <summary>Suffix marker. Always at the end of a sentinel string.</summary>
        public const string Suffix = "##";

        /// <summary>
        /// Wraps a customId in the standard sentinel format: <c>##SH_BATCH:{customId}##</c>.
        /// </summary>
        /// <param name="customId">The unique batch item identifier.</param>
        /// <returns>The full sentinel placeholder string.</returns>
        public static string Wrap(string customId) => Prefix + customId + Suffix;

        /// <summary>
        /// Returns true if <paramref name="value"/> is non-null and starts with the sentinel prefix.
        /// </summary>
        /// <param name="value">String to inspect.</param>
        public static bool Is(string value)
            => value != null && value.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>
        /// Attempts to extract the customId from a sentinel string.
        /// </summary>
        /// <param name="value">Candidate sentinel string.</param>
        /// <param name="customId">The extracted customId on success; <c>null</c> otherwise.</param>
        /// <returns><c>true</c> if the string matched the sentinel format and a customId was extracted.</returns>
        public static bool TryExtract(string value, out string customId)
        {
            customId = null;
            if (!Is(value)) return false;
            if (!value.EndsWith(Suffix, StringComparison.Ordinal)) return false;
            int contentLength = value.Length - Prefix.Length - Suffix.Length;
            if (contentLength <= 0) return false;
            customId = value.Substring(Prefix.Length, contentLength);
            return true;
        }
    }
}
