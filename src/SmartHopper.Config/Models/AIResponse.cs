/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;

namespace SmartHopper.Config.Models
{
    public class AIResponse
    {
        public string Response { get; set; }
        public string FinishReason { get; set; }
        public int InTokens { get; set; }
        public int OutTokens { get; set; }
        public double CompletionTime { get; set; }
        /// <summary>
        /// Gets or sets the list of tool calls made by the AI model.
        /// </summary>
        public List<AIToolCall> ToolCalls { get; set; } = new List<AIToolCall>();
        public string Provider { get; set; }
        public string Model { get; set; }

        /// <summary>
        /// Tracks how many times this response is reused across different data tree branches.
        /// Default is 1 (used once).
        /// </summary>
        public int ReuseCount { get; set; } = 1;
    }
}
