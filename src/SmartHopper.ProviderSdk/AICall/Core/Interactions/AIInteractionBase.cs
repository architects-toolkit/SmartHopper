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
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Metrics;
namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Base record for all interactions, centralizing common properties and defaults.
    /// Provides a unified TurnId used to identify a logical turn across streaming and finalization.
    /// </summary>
    public abstract record AIInteractionBase : IAIInteraction
    {
        /// <inheritdoc />
        public virtual string TurnId { get; init; }

        /// <inheritdoc />
        public virtual DateTime Time { get; init; } = DateTime.UtcNow;

        /// <inheritdoc />
        public virtual AIAgent Agent { get; init; }

        /// <inheritdoc />
        public virtual AIMetrics Metrics { get; init; } = new AIMetrics();

        /// <inheritdoc />
        public virtual IAIInteraction WithTurnId(string turnId)
            => this with { TurnId = turnId };

        /// <inheritdoc />
        public virtual IAIInteraction WithTime(DateTime time)
            => this with { Time = time };

        /// <inheritdoc />
        public virtual IAIInteraction WithAgent(AIAgent agent)
            => this with { Agent = agent };

        /// <inheritdoc />
        public virtual IAIInteraction WithMetrics(AIMetrics metrics)
            => this with { Metrics = metrics };
    }
}
