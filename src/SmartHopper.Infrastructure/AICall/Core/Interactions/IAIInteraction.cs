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
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Metrics;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    public interface IAIInteraction
    {
        /// <summary>
        /// Gets or sets the per-turn stable identifier for this interaction.
        /// All interactions that belong to the same logical assistant turn must share the same TurnId.
        /// UI renderers may use this as a unified key for both streaming aggregation and persisted history.
        /// </summary>
        string TurnId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the interaction.
        /// </summary>
        DateTime Time { get; set; }

        /// <summary>
        /// Gets or sets the agent of the interaction.
        /// </summary>
        AIAgent Agent { get; set; }

        /// <summary>
        /// Gets or sets the metrics associated with the interaction.
        /// </summary>
        AIMetrics Metrics { get; set; }
    }
}
