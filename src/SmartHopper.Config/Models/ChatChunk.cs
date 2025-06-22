/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;

namespace SmartHopper.Config.Models
{
    /// <summary>
    /// Represents a partial chunk of an AI response, used for streaming.
    /// </summary>
    public class ChatChunk
    {
        /// <summary>
        /// The content of this chunk.
        /// </summary>
        public string Content { get; init; }

        /// <summary>
        /// True if this is the final chunk of the response.
        /// </summary>
        public bool   IsFinal  { get; init; }

        // Additional metadata (e.g. token usage) can be added here.
    }
}
