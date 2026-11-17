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
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Metrics;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Outcome of an MCP <c>tools/call</c> invocation.
    /// </summary>
    public sealed class McpToolCallResult
    {
        private McpToolCallResult(
            bool isError,
            JToken payload,
            string? errorMessage,
            IReadOnlyList<ToolResultImage>? images = null,
            IReadOnlyList<AIMetrics>? extraMetrics = null,
            IReadOnlyList<McpDiagnostic>? diagnostics = null)
        {
            this.IsError = isError;
            this.Payload = payload;
            this.ErrorMessage = errorMessage;
            this.Images = images;
            this.ExtraMetrics = extraMetrics;
            this.Diagnostics = diagnostics;
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

        /// <summary>
        /// Gets the typed tool-result images: bytes live here, never inside
        /// <see cref="Payload"/> JSON, descriptors or Markdown. Null when the tool
        /// produced no images.
        /// </summary>
        public IReadOnlyList<ToolResultImage>? Images { get; }

        /// <summary>
        /// Gets helper/usage metrics forwarded verbatim from the underlying
        /// <c>AIReturn</c> — surfaced through <c>_meta</c>, never derived or altered.
        /// </summary>
        public IReadOnlyList<AIMetrics>? ExtraMetrics { get; }

        /// <summary>
        /// Gets the bounded runtime-message diagnostics forwarded from the tool call.
        /// Warnings and information previously died at the adapter boundary; they now
        /// reach the caller alongside the payload. Null when there is nothing to report.
        /// </summary>
        public IReadOnlyList<McpDiagnostic>? Diagnostics { get; }

        /// <summary>Creates a success result.</summary>
        public static McpToolCallResult Ok(
            JToken payload,
            IReadOnlyList<ToolResultImage>? images = null,
            IReadOnlyList<AIMetrics>? extraMetrics = null,
            IReadOnlyList<McpDiagnostic>? diagnostics = null)
        {
            return new McpToolCallResult(false, payload ?? new JObject(), null, images, extraMetrics, diagnostics);
        }

        /// <summary>
        /// Creates an error result. Optional <paramref name="details"/> are merged into the
        /// error payload alongside <c>error</c> (e.g. the called tool's expectedSchema).
        /// </summary>
        public static McpToolCallResult Error(string message, JObject? details = null)
        {
            var payload = details != null ? (JObject)details.DeepClone() : new JObject();
            payload["error"] = message ?? string.Empty;
            return new McpToolCallResult(true, payload, message);
        }

        /// <summary>Creates an error result carrying forwarded helper metrics and diagnostics.</summary>
        public static McpToolCallResult Error(
            string message,
            JObject? details,
            IReadOnlyList<AIMetrics>? extraMetrics,
            IReadOnlyList<McpDiagnostic>? diagnostics = null,
            IReadOnlyList<ToolResultImage>? images = null)
        {
            var payload = details != null ? (JObject)details.DeepClone() : new JObject();
            payload["error"] = message ?? string.Empty;
            return new McpToolCallResult(true, payload, message, images, extraMetrics, diagnostics);
        }
    }
}
