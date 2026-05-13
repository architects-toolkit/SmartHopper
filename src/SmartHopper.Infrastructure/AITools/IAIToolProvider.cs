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

using System.Collections.Generic;

namespace SmartHopper.Infrastructure.AITools
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
