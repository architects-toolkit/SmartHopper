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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Metrics;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Base class for all interactions, centralizing common properties and defaults.
    /// Provides a unified TurnId used to identify a logical turn across streaming and finalization.
    /// </summary>
    public abstract class AIInteractionBase : IAIInteraction
    {
        /// <inheritdoc />
        public virtual string TurnId { get; set; }

        /// <inheritdoc />
        public virtual DateTime Time { get; set; } = DateTime.UtcNow;

        /// <inheritdoc />
        public virtual AIAgent Agent { get; set; }

        /// <inheritdoc />
        public virtual AIMetrics Metrics { get; set; } = new AIMetrics();
    }
}
