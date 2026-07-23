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

using System.Collections.Generic;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Metrics;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// Result of applying a modality fallback step. Contains the transformed body,
    /// per-call metrics for token accounting, and any diagnostic messages.
    /// </summary>
    public sealed class ModalityFallbackResult
    {
        /// <summary>The body with unsupported interactions replaced.</summary>
        public AIBody TransformedBody { get; set; }

        /// <summary>
        /// One entry per AI call made during the fallback transformation.
        /// Callers pass each entry to CombineIntoPersistedMetrics(m, role).
        /// </summary>
        public List<AIMetrics> ExtraMetricsList { get; set; } = new();

        /// <summary>Diagnostic messages generated during the fallback.</summary>
        public List<SHRuntimeMessage> Messages { get; set; } = new();
    }
}
