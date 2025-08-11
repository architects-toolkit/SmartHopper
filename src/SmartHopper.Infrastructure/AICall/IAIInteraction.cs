/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * Portions of this code adapted from:
 * https://github.com/agreentejada/winforms-chat
 * MIT License
 * Copyright (c) 2020 agreentejada
 */

using System;
using System.Collections.Generic;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Infrastructure.AICall
{
    public interface IAIInteraction
    {
        /// <summary>
        /// Gets or sets the timestamp of the interaction.
        /// </summary>
        DateTime Time { get; set; }

        /// <summary>
        /// Gets or sets the agent of the interaction.
        /// </summary>
        AIAgent Agent { get; set; }

        /// <summary>
        /// Gets or sets list of tool calls associated with this interaction.
        /// </summary>
        List<AIToolCall> ToolCalls { get; set; }
    }
}
