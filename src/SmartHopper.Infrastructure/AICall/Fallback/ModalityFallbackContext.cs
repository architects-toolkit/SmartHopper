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

using System.Collections.Concurrent;

namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// Execution-scoped context passed alongside a modality fallback application.
    /// Carries the run's memoization cache so repeated preparations of identical
    /// evidence within the same request run do not pay conversion costs twice.
    /// The cache is bound to the executing run — never shared globally across documents.
    /// </summary>
    public sealed class ModalityFallbackContext
    {
        /// <summary>
        /// Gets the per-run conversion cache (media+context+helper key → evidence text).
        /// May be null; fallbacks then use a call-scoped cache.
        /// </summary>
        public ConcurrentDictionary<string, string> EvidenceCache { get; init; }

        /// <summary>
        /// Gets the optional caller guidance appended as a labeled user turn inside the
        /// helper request — never a replacement for the shared system instruction.
        /// Included in the memoization key.
        /// </summary>
        public string UserGuidance { get; init; }
    }
}
