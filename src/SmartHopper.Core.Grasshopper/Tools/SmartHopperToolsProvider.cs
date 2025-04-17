/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using System.Linq;
using SmartHopper.Config.Interfaces;
using SmartHopper.Config.Models;

namespace SmartHopper.Core.Grasshopper.Tools
{
    /// <summary>
    /// Provider for all SmartHopper tools. This class is automatically discovered by the AIToolManager.
    /// </summary>
    public class SmartHopperToolsProvider : IAIToolProvider
    {
        /// <summary>
        /// Get all tools provided by TextTools and ListTools
        /// </summary>
        /// <returns>Collection of AI tools</returns>
        public IEnumerable<AITool> GetTools()
        {
            // Combine tools from both TextTools and ListTools
            var textTools = TextTools.GetTools();
            var listTools = ListTools.GetTools();
            
            // Return all tools
            return textTools.Concat(listTools);
        }
    }
}
