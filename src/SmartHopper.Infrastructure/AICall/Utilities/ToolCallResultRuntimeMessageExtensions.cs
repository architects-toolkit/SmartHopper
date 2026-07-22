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
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Utilities;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Infrastructure.AICall.Utilities
{
    /// <summary>
    /// Host-side extension that bridges <see cref="ToolCallResult"/> (host-only) into
    /// <see cref="RuntimeMessageUtility"/> (SDK) for runtime-message extraction.
    /// </summary>
    public static class ToolCallResultRuntimeMessageExtensions
    {
        /// <summary>
        /// Extracts runtime messages from a <see cref="ToolCallResult"/> envelope.
        /// Combines diagnostics carried on the envelope with messages found inside
        /// the underlying JSON payload.
        /// </summary>
        /// <param name="toolResult">The envelope to extract messages from.</param>
        /// <returns>A list of extracted SHRuntimeMessage objects, or empty list if none found.</returns>
        public static List<SHRuntimeMessage> ExtractMessages(ToolCallResult toolResult)
        {
            var messages = new List<SHRuntimeMessage>();
            if (toolResult == null)
            {
                return messages;
            }

            if (toolResult.Messages != null)
            {
                foreach (var m in toolResult.Messages)
                {
                    if (m != null)
                    {
                        messages.Add(m);
                    }
                }
            }

            messages.AddRange(RuntimeMessageUtility.ExtractMessages(toolResult.Result));
            return messages;
        }
    }
}
