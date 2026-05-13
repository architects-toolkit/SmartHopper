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
using SmartHopper.Infrastructure.AICall.Core.Base;

namespace SmartHopper.Infrastructure.AICall.Utilities
{
    /// <summary>
    /// Shared utilities for working with AIRuntimeMessage collections.
    /// </summary>
    public static class RuntimeMessageUtility
    {
        /// <summary>
        /// Checks if a collection of runtime messages contains at least one message
        /// with severity at or above the specified threshold.
        /// </summary>
        /// <param name="messages">The messages to check.</param>
        /// <param name="threshold">The minimum severity level to match.</param>
        /// <returns>True if any message has severity >= threshold; otherwise false.</returns>
        public static bool HasSeverityAtOrAbove(List<AIRuntimeMessage> messages, AIRuntimeMessageSeverity threshold)
        {
            if (messages == null)
            {
                return false;
            }

            foreach (var m in messages)
            {
                if (m != null && m.Severity >= threshold)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
