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
using System.Linq;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Bounded, serializable projection of an <see cref="SHRuntimeMessage"/> for MCP
    /// <c>tools/call</c> responses. Runtime messages previously died at the adapter
    /// boundary (only the first Error was surfaced); every non-Debug message is now
    /// forwarded with stable severity/origin/code fields.
    /// </summary>
    public sealed class McpDiagnostic
    {
        /// <summary>Maximum number of diagnostics projected per tool call.</summary>
        internal const int MaxDiagnostics = 64;

        /// <summary>Maximum characters kept per message.</summary>
        internal const int MaxMessageLength = 2048;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpDiagnostic"/> class.
        /// </summary>
        /// <param name="severity">Message severity.</param>
        /// <param name="origin">Message origin.</param>
        /// <param name="code">Machine-readable message code.</param>
        /// <param name="message">Message text.</param>
        public McpDiagnostic(SHRuntimeMessageSeverity severity, SHRuntimeMessageOrigin origin, SHMessageCode code, string message)
        {
            this.Severity = severity;
            this.Origin = origin;
            this.Code = code;
            this.Message = message ?? string.Empty;
        }

        /// <summary>Gets the message severity.</summary>
        public SHRuntimeMessageSeverity Severity { get; }

        /// <summary>Gets the message origin.</summary>
        public SHRuntimeMessageOrigin Origin { get; }

        /// <summary>Gets the machine-readable message code.</summary>
        public SHMessageCode Code { get; }

        /// <summary>Gets the message text.</summary>
        public string Message { get; }

        /// <summary>
        /// Projects runtime messages into bounded MCP diagnostics. Debug-severity
        /// messages stay private to the host log; overflow is reported by a final
        /// truncation diagnostic so callers can see that entries were dropped.
        /// </summary>
        /// <param name="messages">Runtime messages from the executed call.</param>
        /// <returns>Projected diagnostics, or <c>null</c> when there is nothing to forward.</returns>
        public static IReadOnlyList<McpDiagnostic>? Project(IReadOnlyList<SHRuntimeMessage>? messages)
        {
            if (messages == null || messages.Count == 0)
            {
                return null;
            }

            var surfaceable = messages
                .Where(m => m != null && m.Severity != SHRuntimeMessageSeverity.Debug)
                .ToList();
            if (surfaceable.Count == 0)
            {
                return null;
            }

            var projected = surfaceable
                .Take(MaxDiagnostics)
                .Select(m => new McpDiagnostic(m.Severity, m.Origin, m.Code, Truncate(m.Message)))
                .ToList();

            var dropped = surfaceable.Count - projected.Count;
            if (dropped > 0)
            {
                projected.Add(new McpDiagnostic(
                    SHRuntimeMessageSeverity.Warning,
                    SHRuntimeMessageOrigin.Return,
                    SHMessageCode.Unknown,
                    $"{dropped} diagnostic(s) were truncated to the {MaxDiagnostics}-entry MCP limit."));
            }

            return projected;
        }

        /// <summary>Serializes this diagnostic to its stable JSON projection.</summary>
        /// <returns>A JSON object with severity, origin, code and message fields.</returns>
        public JObject ToJson()
        {
            return new JObject
            {
                ["severity"] = this.Severity.ToString(),
                ["origin"] = this.Origin.ToString(),
                ["code"] = this.Code.ToString(),
                ["message"] = this.Message,
            };
        }

        private static string Truncate(string? message)
        {
            if (string.IsNullOrEmpty(message) || message.Length <= MaxMessageLength)
            {
                return message ?? string.Empty;
            }

            return message.Substring(0, MaxMessageLength) + "...";
        }
    }
}
