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
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.AICall.JsonSchemas
{
    /// <summary>
    /// Adapter contract that allows each provider project to define how
    /// standardized target schemas should be transformed into the provider-end schema
    /// and how content should be unwrapped.
    /// </summary>
    public interface IJsonSchemaAdapter
    {
        /// <summary>
        /// Provider name this adapter applies to (e.g., "OpenAI").
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Transforms the standardized target schema to the provider-end schema.
        /// Return the original schema with IsWrapped=false if no wrapping is needed.
        /// </summary>
        (JObject wrapped, SchemaWrapperInfo info) Wrap(JObject schema);

        /// <summary>
        /// Optional unwrapping customization. For most providers the default logic
        /// in JsonSchemaService is enough; adapters can return input content if
        /// they don't need custom behavior.
        /// </summary>
        string Unwrap(string content, SchemaWrapperInfo info);
    }

    /// <summary>
    /// Global registry for provider schema adapters. Providers register their adapter
    /// at runtime (in their constructor or initialization path) to avoid Infrastructure referencing providers.
    /// </summary>
    public static class JsonSchemaAdapterRegistry
    {
        private static readonly ConcurrentDictionary<string, IJsonSchemaAdapter> _adapters = new ConcurrentDictionary<string, IJsonSchemaAdapter>(StringComparer.OrdinalIgnoreCase);

        public static IJsonSchemaAdapter Default { get; } = new DefaultJsonSchemaAdapter();

        public static void Register(IJsonSchemaAdapter adapter)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));
            if (string.IsNullOrWhiteSpace(adapter.ProviderName)) throw new ArgumentException("ProviderName cannot be empty", nameof(adapter));
            _adapters[adapter.ProviderName] = adapter;
        }

        public static IJsonSchemaAdapter Get(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName)) return Default;
            if (_adapters.TryGetValue(providerName, out var adapter)) return adapter;
            return Default;
        }
    }

    /// <summary>
    /// Default adapter implementing the current wrapping convention:
    /// - Providers require object root for structured output
    /// - Arrays are wrapped under { "items": [...] }
    /// - Primitives wrapped under { "value": ... }
    /// - Unknown root types wrapped under { "data": ... }
    /// </summary>
    internal sealed class DefaultJsonSchemaAdapter : IJsonSchemaAdapter
    {
        public string ProviderName => "__default__";

        public (JObject wrapped, SchemaWrapperInfo info) Wrap(JObject schema)
        {
            if (schema is null) throw new ArgumentNullException(nameof(schema));
            var schemaType = schema["type"]?.ToString();

            // If already an object, no wrapping is needed
            if (string.Equals(schemaType, "object", StringComparison.OrdinalIgnoreCase))
            {
                return (schema, new SchemaWrapperInfo { IsWrapped = false });
            }

            if (string.Equals(schemaType, "array", StringComparison.OrdinalIgnoreCase))
            {
                var wrapped = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["items"] = schema },
                    ["required"] = new JArray { "items" },
                    ["additionalProperties"] = false,
                };
                return (wrapped, new SchemaWrapperInfo { IsWrapped = true, WrapperType = "array", PropertyName = "items" });
            }

            if (schemaType == "string" || schemaType == "number" || schemaType == "integer" || schemaType == "boolean")
            {
                var wrapped = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["value"] = schema },
                    ["required"] = new JArray { "value" },
                    ["additionalProperties"] = false,
                };
                return (wrapped, new SchemaWrapperInfo { IsWrapped = true, WrapperType = schemaType, PropertyName = "value" });
            }

            // Unknown type: wrap under data
            var generic = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject { ["data"] = schema },
                ["required"] = new JArray { "data" },
                ["additionalProperties"] = false,
            };
            return (generic, new SchemaWrapperInfo { IsWrapped = true, WrapperType = "unknown", PropertyName = "data" });
        }

        public string Unwrap(string content, SchemaWrapperInfo info)
        {
            // Defer to JsonSchemaService default unwrapping in most cases; keep method for extensibility
            return content;
        }
    }
}
