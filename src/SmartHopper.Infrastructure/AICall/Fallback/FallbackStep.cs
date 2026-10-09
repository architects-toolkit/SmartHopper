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

namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// A single modality-conversion step in a <see cref="FallbackChain"/>, bound to the
    /// provider and model that were resolved for it. Different steps in one chain may
    /// resolve to different providers/models, so each step carries its own.
    /// </summary>
    public sealed class FallbackStep
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FallbackStep"/> class.
        /// </summary>
        /// <param name="fallback">The conversion implementation.</param>
        /// <param name="provider">The provider that executes the conversion call.</param>
        /// <param name="model">The model that executes the conversion call.</param>
        public FallbackStep(IModalityFallback fallback, string provider, string model)
        {
            this.Fallback = fallback;
            this.Provider = provider;
            this.Model = model;
        }

        /// <summary>The conversion implementation.</summary>
        public IModalityFallback Fallback { get; }

        /// <summary>Provider executing this step's conversion call.</summary>
        public string Provider { get; }

        /// <summary>Model executing this step's conversion call.</summary>
        public string Model { get; }
    }
}
