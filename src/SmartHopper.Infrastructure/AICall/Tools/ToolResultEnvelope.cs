/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;


namespace SmartHopper.Infrastructure.AICall.Tools
{
    /// <summary>
    /// Describes a normalized metadata envelope for tool results.
    /// Placed under the reserved root property "__envelope" to avoid breaking existing payload shapes.
    /// </summary>
    public class ToolResultEnvelope
    {
        /// <summary>
        /// Envelope version for compatibility.
        /// </summary>
        [JsonProperty("version")]
        public string Version { get; set; } = "1";

        /// <summary>
        /// Tool name that produced this result (e.g. "list_generate").
        /// </summary>
        [JsonProperty("tool")]
        public string Tool { get; set; }

        /// <summary>
        /// Optional provider id and model id used for the final call that produced this result.
        /// </summary>
        [JsonProperty("provider")]
        public string Provider { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        /// <summary>
        /// Optional tool call identifier to correlate with AIInteractionToolCall.Id
        /// </summary>
        [JsonProperty("toolCallId")]
        public string ToolCallId { get; set; }

        /// <summary>
        /// Content type describing the payload.
        /// </summary>
        [JsonProperty("contentType")]
        public ToolResultContentType ContentType { get; set; } = ToolResultContentType.Unknown;

        /// <summary>
        /// Optional JSON path (dot-notation) to the payload within the root object (default: "result").
        /// </summary>
        [JsonProperty("payloadPath")]
        public string PayloadPath { get; set; } = "result";

        /// <summary>
        /// Optional reference or inline schema describing the payload.
        /// String can be a JSON schema string or a schema id/uri.
        /// </summary>
        [JsonProperty("schemaRef")]
        public string SchemaRef { get; set; }

        /// <summary>
        /// Optional additional compatibility keys that may contain the payload (e.g. ["result", "list", "items"]).
        /// Used as fallbacks during extraction.
        /// </summary>
        [JsonProperty("compat")]
        public List<string> CompatKeys { get; set; } = new List<string> { "result", "list", "items" };

        /// <summary>
        /// UTC timestamp when the envelope was created.
        /// </summary>
        [JsonProperty("createdUtc")]
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional tags for categorization.
        /// </summary>
        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Builds a default envelope for a tool.
        /// </summary>
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
        public void AttachTo(JObject root)
        {
            if (root == null) return;
            root["__envelope"] = JObject.FromObject(this);
        }

        /// <summary>
        /// Ensures the root has an envelope. If missing, creates a minimal one with provided defaults.
        /// </summary>
        public static ToolResultEnvelope Ensure(JObject root, string tool = null, ToolResultContentType type = ToolResultContentType.Unknown, string payloadPath = "result")
        {
            var env = TryGet(root) ?? Create(tool, type, payloadPath);
            env.AttachTo(root);
            return env;
        }

        /// <summary>
        /// Tries to read the envelope from the root under "__envelope".
        /// </summary>
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
        public static JObject Wrap(JToken payload, ToolResultEnvelope envelope, string payloadKey = "result")
        {
            var root = new JObject
            {
                [payloadKey] = payload ?? JValue.CreateNull()
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
        Unknown = 0,
        Text = 1,
        List = 2,
        Object = 3,
        Image = 4,
        Binary = 5,
    }
}
