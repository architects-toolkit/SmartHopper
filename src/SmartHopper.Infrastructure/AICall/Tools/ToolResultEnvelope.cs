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

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace SmartHopper.Infrastructure.AICall.Tools
{
    /// <summary>
    /// Describes a normalized metadata envelope for tool results.
    /// Placed under the reserved root property "__envelope" to avoid breaking existing payload shapes.
    /// </summary>
    public class ToolResultEnvelope
    {
        /// <summary>
        /// Gets or sets the envelope version for compatibility.
        /// </summary>
        [JsonProperty("version")]
        public string Version { get; set; } = "1";

        /// <summary>
        /// Gets or sets the tool name that produced this result (e.g. "list_generate").
        /// </summary>
        [JsonProperty("tool")]
        public string Tool { get; set; }

        /// <summary>
        /// Gets or sets the optional provider id and model id used for the final call that produced this result.
        /// </summary>
        [JsonProperty("provider")]
        public string Provider { get; set; }

        /// <summary>
        /// Gets or sets the optional model identifier used by the provider to generate this result.
        /// </summary>
        [JsonProperty("model")]
        public string Model { get; set; }

        /// <summary>
        /// Gets or sets the optional tool call identifier to correlate with AIInteractionToolCall.Id.
        /// </summary>
        [JsonProperty("toolCallId")]
        public string ToolCallId { get; set; }

        /// <summary>
        /// Gets or sets the content type describing the payload.
        /// </summary>
        [JsonProperty("contentType")]
        public ToolResultContentType ContentType { get; set; } = ToolResultContentType.Unknown;

        /// <summary>
        /// Gets or sets the optional JSON path (dot-notation) to the payload within the root object (default: "result").
        /// </summary>
        [JsonProperty("payloadPath")]
        public string PayloadPath { get; set; } = "result";

        /// <summary>
        /// Gets or sets the optional reference or inline schema describing the payload.
        /// String can be a JSON schema string or a schema id/uri.
        /// </summary>
        [JsonProperty("schemaRef")]
        public string SchemaRef { get; set; }

        /// <summary>
        /// Gets or sets the optional additional compatibility keys that may contain the payload (e.g. ["result", "list", "items"]).
        /// Used as fallbacks during extraction.
        /// </summary>
        [JsonProperty("compat")]
        public List<string> CompatKeys { get; set; } = new List<string> { "result", "list", "items" };

        /// <summary>
        /// Gets or sets the UTC timestamp when the envelope was created.
        /// </summary>
        [JsonProperty("createdUtc")]
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the optional tags for categorization.
        /// </summary>
        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Builds a default envelope for a tool.
        /// </summary>
        /// <param name="tool">The tool name that produced this result (e.g., "list_generate").</param>
        /// <param name="type">The content type classification of the payload.</param>
        /// <param name="payloadPath">The JSON key under which the payload resides (default: "result").</param>
        /// <param name="provider">Optional provider identifier.</param>
        /// <param name="model">Optional model identifier.</param>
        /// <param name="toolCallId">Optional tool call identifier for correlation.</param>
        /// <returns>A new envelope initialized with the provided parameters.</returns>
        public static ToolResultEnvelope Create(string tool, ToolResultContentType type = ToolResultContentType.Unknown, string payloadPath = "result", string provider = null, string model = null, string toolCallId = null)
        {
            return new ToolResultEnvelope
            {
                Tool = tool,
                ContentType = type,
                PayloadPath = string.IsNullOrWhiteSpace(payloadPath) ? "result" : payloadPath,
                Provider = provider,
                Model = model,
                ToolCallId = toolCallId,
                CreatedUtc = DateTime.UtcNow,
            };
        }

        /// <summary>
        /// Attaches this envelope under "__envelope" of the provided root object.
        /// </summary>
        /// <param name="root">The root JSON object to attach the envelope to.</param>
        public void AttachTo(JObject root)
        {
            if (root == null) return;
            root["__envelope"] = JObject.FromObject(this);
        }

        /// <summary>
        /// Ensures the root has an envelope. If missing, creates a minimal one with provided defaults.
        /// </summary>
        /// <param name="root">The root JSON object to inspect and attach to.</param>
        /// <param name="tool">Optional tool name to set when creating a new envelope.</param>
        /// <param name="type">Optional content type for the new envelope.</param>
        /// <param name="payloadPath">Optional payload path for the new envelope.</param>
        /// <returns>The existing or newly created envelope attached to the root.</returns>
        public static ToolResultEnvelope Ensure(JObject root, string tool = null, ToolResultContentType type = ToolResultContentType.Unknown, string payloadPath = "result")
        {
            var env = TryGet(root) ?? Create(tool, type, payloadPath);
            env.AttachTo(root);
            return env;
        }

        /// <summary>
        /// Tries to read the envelope from the root under "__envelope".
        /// </summary>
        /// <param name="root">The root JSON object containing an optional envelope.</param>
        /// <returns>The parsed envelope if present and valid; otherwise null.</returns>
        public static ToolResultEnvelope TryGet(JObject root)
        {
            if (root == null) return null;
            if (root.TryGetValue("__envelope", out var token) && token is JObject envObj)
            {
                try
                {
                    return envObj.ToObject<ToolResultEnvelope>();
                }
                catch
                {
                    // ignore malformed envelope
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts the payload token using the envelope's PayloadPath and CompatKeys.
        /// Returns null if not found.
        /// </summary>
        /// <param name="root">The root JSON object to extract the payload from.</param>
        /// <returns>The payload token extracted from the root, or null if not found.</returns>
        public static JToken GetPayload(JObject root)
        {
            if (root == null) return null;

            var env = TryGet(root);

            // 1) Try payloadPath if present
            var paths = new List<string>();
            if (!string.IsNullOrWhiteSpace(env?.PayloadPath))
            {
                paths.Add(env.PayloadPath);
            }

            // 2) Fallback to compat keys
            if (env?.CompatKeys != null)
            {
                paths.AddRange(env.CompatKeys);
            }
            else
            {
                paths.AddRange(new[] { "result", "list", "items" });
            }

            foreach (var path in paths)
            {
                if (root.TryGetValue(path, out var t))
                {
                    return t;
                }
            }

            // 3) If no obvious key, return the whole object as payload
            return root;
        }

        /// <summary>
        /// Builds a root result object with payload under payloadKey and attached envelope.
        /// Does not mutate the payload token.
        /// </summary>
        /// <param name="payload">The JSON payload token to place under the payload key.</param>
        /// <param name="envelope">The envelope to attach; if null, a default will be created.</param>
        /// <param name="payloadKey">The JSON key under which to store the payload (default: "result").</param>
        /// <returns>A new JObject containing the payload and attached envelope.</returns>
        public static JObject Wrap(JToken payload, ToolResultEnvelope envelope, string payloadKey = "result")
        {
            var root = new JObject
            {
                [payloadKey] = payload ?? JValue.CreateNull(),
            };
            (envelope ?? new ToolResultEnvelope()).AttachTo(root);
            return root;
        }
    }

    /// <summary>
    /// Standard content type classification for tool result payloads.
    /// </summary>
    public enum ToolResultContentType
    {
        /// <summary>
        /// Content type could not be determined.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Plain text payload.
        /// </summary>
        Text = 1,

        /// <summary>
        /// A homogeneous list/array payload.
        /// </summary>
        List = 2,

        /// <summary>
        /// A JSON object payload with named properties.
        /// </summary>
        Object = 3,

        /// <summary>
        /// An image payload (e.g., URL or base64-encoded image data).
        /// </summary>
        Image = 4,

        /// <summary>
        /// Arbitrary binary payload.
        /// </summary>
        Binary = 5,
    }
}
