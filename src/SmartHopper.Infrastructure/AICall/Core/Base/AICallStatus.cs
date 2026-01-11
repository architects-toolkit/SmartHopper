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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

namespace SmartHopper.Infrastructure.AICall.Core.Base
{
    /// <summary>
    /// Specifies the originator of an AI interaction.
    /// </summary>
    public enum AICallStatus
    {
        /// <summary>Default state, no call in progress.</summary>
        Idle,

        /// <summary>Processing is the state when the call has started and no data was received yet. Non-streaming calls are in this state until the call is finished.</summary>
        Processing,

        /// <summary>Status when the reception of data from the AI is in progress. Streaming calls transition from Processing to Streaming when the first chunk of data is received.</summary>
        Streaming,

        /// <summary>Status when the AI is calling a tool.</summary>
        CallingTools,

        /// <summary>Finished status indicates that the call has completed.</summary>
        Finished,
    }

    /// <summary>
    /// Extension methods for AICallStatus.
    /// </summary>
    public static class AICallStatusExtensions
    {
        /// <summary>
        /// Converts an AICallStatus to a string.
        /// </summary>
        /// <param name="status">The status to convert.</param>
        /// <returns>The string representation of the status.</returns>
        public static string ToString(this AICallStatus status)
        {
            return status switch
            {
                AICallStatus.Idle => "idle",
                AICallStatus.Processing => "processing",
                AICallStatus.Streaming => "streaming",
                AICallStatus.CallingTools => "calling_tools",
                AICallStatus.Finished => "finished",
                _ => "unknown",
            };
        }

        /// <summary>
        /// Converts an AICallStatus to a description.
        /// </summary>
        /// <param name="status">The status to convert.</param>
        /// <returns>The description of the status.</returns>
        public static string ToDescription(this AICallStatus status)
        {
            return status switch
            {
                AICallStatus.Idle => "Idle",
                AICallStatus.Processing => "Processing",
                AICallStatus.Streaming => "Streaming",
                AICallStatus.CallingTools => "Calling tools",
                AICallStatus.Finished => "Finished",
                _ => "Unknown",
            };
        }

        /// <summary>
        /// Converts a string to an AICallStatus.
        /// </summary>
        /// <param name="status">The string to convert.</param>
        /// <returns>The AICallStatus.</returns>
        public static AICallStatus FromString(string status)
        {
            return status switch
            {
                "idle" => AICallStatus.Idle,
                "processing" => AICallStatus.Processing,
                "streaming" => AICallStatus.Streaming,
                "calling_tools" => AICallStatus.CallingTools,
                "finished" => AICallStatus.Finished,
                _ => AICallStatus.Idle,
            };
        }
    }
}
