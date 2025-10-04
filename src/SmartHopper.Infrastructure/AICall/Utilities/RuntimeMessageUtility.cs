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
