/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;


namespace SmartHopper.Infrastructure.AICall.Tools
{
    /// <summary>
    /// Convenience extensions over JObject for ToolResultEnvelope usage.
    /// </summary>
    public static class ToolResultEnvelopeExtensions
    {
        /// <summary>
        /// Attaches the envelope to this root object under "__envelope".
        /// </summary>
        public static JObject WithEnvelope(this JObject root, ToolResultEnvelope envelope)
        {
            if (root == null) return null;
            (envelope ?? new ToolResultEnvelope()).AttachTo(root);
            return root;
        }

        /// <summary>
        /// Ensures the root has an envelope, creating a minimal one if missing.
        /// </summary>
        public static ToolResultEnvelope EnsureEnvelope(this JObject root, string tool = null, ToolResultContentType type = ToolResultContentType.Unknown, string payloadPath = "result")
        {
            return ToolResultEnvelope.Ensure(root, tool, type, payloadPath);
        }

        /// <summary>
        /// Gets the envelope from the root, or null.
        /// </summary>
        public static ToolResultEnvelope GetEnvelope(this JObject root)
        {
            return ToolResultEnvelope.TryGet(root);
        }

        /// <summary>
        /// Gets the payload token using the envelope rules.
        /// </summary>
        public static JToken GetPayload(this JObject root)
        {
            return ToolResultEnvelope.GetPayload(root);
        }

        /// <summary>
        /// Wraps the payload into a new root object and attaches the envelope.
        /// </summary>
        public static JObject WrapPayload(this JToken payload, ToolResultEnvelope envelope, string payloadKey = "result")
        {
            return ToolResultEnvelope.Wrap(payload, envelope, payloadKey);
        }
    }
}
