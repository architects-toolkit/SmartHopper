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
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.ProviderSdk.AICall.Validation
{
    /// <summary>
    /// Process-wide deduplication for provider trust warnings produced by
    /// <see cref="ProviderTrustPolicy.Evaluate(AIRequestCall, IProviderTrustHost)"/>.
    /// Integrity notices such as "provider could not be verified" are provider/session-scoped,
    /// not per-component diagnostics: surfacing them at most once per provider per session
    /// avoids repeating the same warning on every component that validates a request.
    /// Callers that surface <see cref="SHMessageCode.ProviderTrustWarning"/> messages to end
    /// users should consult <see cref="ShouldSurface"/> before displaying them. Error-severity
    /// (blocking) trust messages must always be surfaced and are not deduplicated here.
    /// </summary>
    public static class ProviderTrustWarningDeduplicator
    {
        /// <summary>
        /// Marker every trust-policy diagnostic embeds in its text: <c>Provider '{name}'</c>.
        /// </summary>
        private const string ProviderMarker = "Provider '";

        /// <summary>
        /// Provider names whose trust warning has already been surfaced this session.
        /// </summary>
        private static readonly HashSet<string> SurfacedProviders = new(StringComparer.Ordinal);

        /// <summary>
        /// Guards <see cref="SurfacedProviders"/> because evaluation and surfacing can
        /// happen from worker and UI threads concurrently.
        /// </summary>
        private static readonly object SyncLock = new();

        /// <summary>
        /// Determines whether a message should be surfaced to the user. Trust-policy
        /// warnings (<see cref="SHMessageCode.ProviderTrustWarning"/>) surface only the
        /// first time each provider produces one; every other message always surfaces,
        /// including error-severity trust blocks.
        /// </summary>
        /// <param name="message">The message about to be surfaced.</param>
        /// <returns><c>true</c> when the message should be surfaced; <c>false</c> for a repeat trust warning for the same provider.</returns>
        public static bool ShouldSurface(SHRuntimeMessage message)
        {
            if (message == null || message.Code != SHMessageCode.ProviderTrustWarning)
            {
                return true;
            }

            lock (SyncLock)
            {
                return SurfacedProviders.Add(ExtractProviderKey(message.Message));
            }
        }

        /// <summary>
        /// Clears all recorded providers. Intended for tests and for hosts that want to
        /// reset session-scoped deduplication state.
        /// </summary>
        public static void Reset()
        {
            lock (SyncLock)
            {
                SurfacedProviders.Clear();
            }
        }

        /// <summary>
        /// Extracts the provider name from a trust warning text of the form
        /// <c>Provider '{name}' ...</c>, falling back to the full text when the marker
        /// is absent so unknown formats still deduplicate per unique message.
        /// </summary>
        /// <param name="text">The warning text.</param>
        /// <returns>The deduplication key (provider name when detectable).</returns>
        private static string ExtractProviderKey(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var start = text.IndexOf(ProviderMarker, StringComparison.Ordinal);
            if (start < 0)
            {
                return text;
            }

            start += ProviderMarker.Length;
            var end = text.IndexOf('\'', start);
            return end > start ? text.Substring(start, end - start) : text;
        }
    }
}
