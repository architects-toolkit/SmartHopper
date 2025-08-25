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
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.JsonSchemas;

namespace SmartHopper.Providers.OpenAI
{
    internal sealed class OpenAIJsonSchemaAdapter : IJsonSchemaAdapter
    {
        public string ProviderName => "OpenAI";

        public (JObject wrapped, SchemaWrapperInfo info) Wrap(JObject schema)
        {
            if (schema is null) throw new ArgumentNullException(nameof(schema));
            var schemaType = schema["type"]?.ToString();

            // OpenAI requires object-root schemas for structured outputs
            if (string.Equals(schemaType, "object", StringComparison.OrdinalIgnoreCase))
            {
                return (schema, new SchemaWrapperInfo { IsWrapped = false, ProviderName = this.ProviderName });
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
                return (wrapped, new SchemaWrapperInfo { IsWrapped = true, WrapperType = "array", PropertyName = "items", ProviderName = this.ProviderName });
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
                return (wrapped, new SchemaWrapperInfo { IsWrapped = true, WrapperType = schemaType ?? "primitive", PropertyName = "value", ProviderName = this.ProviderName });
            }

            // Unknown type: wrap under data
            var generic = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject { ["data"] = schema },
                ["required"] = new JArray { "data" },
                ["additionalProperties"] = false,
            };
            return (generic, new SchemaWrapperInfo { IsWrapped = true, WrapperType = "unknown", PropertyName = "data", ProviderName = this.ProviderName });
        }

        public string Unwrap(string content, SchemaWrapperInfo info)
        {
            // Default service logic is sufficient for OpenAI
            return content;
        }
    }
}
