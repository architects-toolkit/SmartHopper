/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using SmartHopper.Infrastructure.Models;
using System.Collections.Generic;

namespace SmartHopper.Infrastructure.Interfaces
{
    /// <summary>
    /// Interface for classes that provide AI tools.
    /// Implement this interface in tool classes to enable auto-discovery.
    /// </summary>
    public interface IAIToolProvider
    {
        /// <summary>
        /// Get all tools provided by this class
        /// </summary>
        /// <returns>Collection of AI tools</returns>
        IEnumerable<AITool> GetTools();
    }
}
