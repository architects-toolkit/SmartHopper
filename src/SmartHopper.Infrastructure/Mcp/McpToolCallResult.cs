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

using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Outcome of an MCP <c>tools/call</c> invocation.
    /// </summary>
    public sealed class McpToolCallResult
    {
        private McpToolCallResult(bool isError, JToken payload, string? errorMessage)
        {
            this.IsError = isError;
            this.Payload = payload;
            this.ErrorMessage = errorMessage;
        }

        /// <summary>Gets a value indicating whether the call failed.</summary>
        public bool IsError { get; }

        /// <summary>
        /// Gets the JSON payload returned by the tool. For successful calls this is the
        /// tool's <c>Result</c> JObject; for errors it is an object describing the failure.
        /// </summary>
        public JToken Payload { get; }

        /// <summary>Gets the error message when <see cref="IsError"/> is <c>true</c>.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Creates a success result.</summary>
        public static McpToolCallResult Ok(JToken payload)
        {
            return new McpToolCallResult(false, payload ?? new JObject(), null);
        }

        /// <summary>Creates an error result.</summary>
        public static McpToolCallResult Error(string message)
        {
            var payload = new JObject
            {
                ["error"] = message ?? string.Empty,
            };
            return new McpToolCallResult(true, payload, message);
        }
    }
}
