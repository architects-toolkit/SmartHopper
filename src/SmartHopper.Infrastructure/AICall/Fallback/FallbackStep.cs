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

namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// A single modality-conversion step in a <see cref="FallbackChain"/>, bound to an
    /// ordered list of provider/model candidates resolved for it. Different steps in
    /// one chain may resolve to different providers/models, so each step carries its
    /// own. Candidates beyond the first are alternates the executor may try when a
    /// target fails — candidate selection is resolution-time policy, execution
    /// failover is bounded separately by <see cref="FallbackChain.MaxAttemptsPerStep"/>.
    /// </summary>
    public sealed class FallbackStep
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FallbackStep"/> class with a
        /// single resolved target.
        /// </summary>
        /// <param name="fallback">The conversion implementation.</param>
        /// <param name="provider">The provider that executes the conversion call.</param>
        /// <param name="model">The model that executes the conversion call.</param>
        public FallbackStep(IModalityFallback fallback, string provider, string model)
            : this(fallback, new List<(string Provider, string Model)> { (provider, model) })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FallbackStep"/> class with
        /// ordered candidates, most-preferred first.
        /// </summary>
        /// <param name="fallback">The conversion implementation.</param>
        /// <param name="candidates">Ordered (provider, model) targets for this step.</param>
        public FallbackStep(IModalityFallback fallback, IReadOnlyList<(string Provider, string Model)> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                throw new ArgumentException("A fallback step requires at least one provider/model candidate.", nameof(candidates));
            }

            this.Fallback = fallback;
            this.Candidates = candidates;
        }

        /// <summary>The conversion implementation.</summary>
        public IModalityFallback Fallback { get; }

        /// <summary>Ordered (provider, model) targets for this step, most-preferred first.</summary>
        public IReadOnlyList<(string Provider, string Model)> Candidates { get; }

        /// <summary>Provider executing this step's conversion call (the first candidate).</summary>
        public string Provider => this.Candidates[0].Provider;

        /// <summary>Model executing this step's conversion call (the first candidate).</summary>
        public string Model => this.Candidates[0].Model;
    }
}
