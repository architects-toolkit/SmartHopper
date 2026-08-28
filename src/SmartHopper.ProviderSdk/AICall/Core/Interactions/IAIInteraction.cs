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

/*
 * Portions of this code adapted from:
 * https://github.com/agreentejada/winforms-chat
 * MIT License
 * Copyright (c) 2020 agreentejada
 */

using System;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Metrics;
namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    public interface IAIInteraction
    {
        /// <summary>
        /// Gets the per-turn stable identifier for this interaction.
        /// All interactions that belong to the same logical assistant turn must share the same TurnId.
        /// UI renderers may use this as a unified key for both streaming aggregation and persisted history.
        /// </summary>
        string TurnId { get; init; }

        /// <summary>
        /// Gets the timestamp of the interaction.
        /// </summary>
        DateTime Time { get; init; }

        /// <summary>
        /// Gets the agent of the interaction.
        /// </summary>
        AIAgent Agent { get; init; }

        /// <summary>
        /// Gets the metrics associated with the interaction.
        /// </summary>
        AIMetrics Metrics { get; init; }

        /// <summary>
        /// Returns a new <see cref="IAIInteraction"/> of the same concrete type with the specified
        /// <see cref="TurnId"/>, preserving all other fields.
        /// </summary>
        IAIInteraction WithTurnId(string turnId);

        /// <summary>
        /// Returns a new <see cref="IAIInteraction"/> of the same concrete type with the specified
        /// <see cref="Time"/>, preserving all other fields.
        /// </summary>
        IAIInteraction WithTime(DateTime time);

        /// <summary>
        /// Returns a new <see cref="IAIInteraction"/> of the same concrete type with the specified
        /// <see cref="Agent"/>, preserving all other fields.
        /// </summary>
        IAIInteraction WithAgent(AIAgent agent);

        /// <summary>
        /// Returns a new <see cref="IAIInteraction"/> of the same concrete type with the specified
        /// <see cref="Metrics"/>, preserving all other fields.
        /// </summary>
        IAIInteraction WithMetrics(AIMetrics metrics);
    }
}
