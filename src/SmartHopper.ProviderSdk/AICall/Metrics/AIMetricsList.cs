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

namespace SmartHopper.ProviderSdk.AICall.Metrics
{
    /// <summary>
    /// Ordered list of per-call metrics for a single component solve.
    /// Single-entry lists represent the common case (one provider/model).
    /// Multi-entry lists arise when modality fallback, img2text, or any other
    /// sub-call uses a different provider or model from the main call.
    /// </summary>
    public sealed class AIMetricsList
    {
        private readonly List<AIMetrics> _entries = new();

        /// <summary>Gets the ordered metrics entries.</summary>
        public IReadOnlyList<AIMetrics> Entries => this._entries;

        /// <summary>True when all entries share the same Provider and Model.</summary>
        public bool IsSingleProvider =>
            this._entries.Count <= 1 ||
            this._entries.All(m =>
                string.Equals(m.Provider, this._entries[0].Provider, StringComparison.OrdinalIgnoreCase)
                && string.Equals(m.Model, this._entries[0].Model, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Adds an entry. Each call appends a distinct entry so that multi-branch
        /// solves emit one metric per branch. Entries are never merged.
        /// </summary>
        public void Add(AIMetrics metrics, string role = null)
        {
            if (metrics == null) return;
            metrics.Role = role;
            this._entries.Add(metrics);
        }

    }
}
