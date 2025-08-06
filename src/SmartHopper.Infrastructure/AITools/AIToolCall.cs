/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Infrastructure.AITools
{
    /// <summary>
    /// Represents a tool call made by an AI model.
    /// </summary>
    public class AIToolCall
    {
        /// <summary>
        /// Gets or sets the ID of the tool call.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the tool being called.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the arguments passed to the tool as a JSON string.
        /// </summary>
        public string Arguments { get; set; }
    }
}
