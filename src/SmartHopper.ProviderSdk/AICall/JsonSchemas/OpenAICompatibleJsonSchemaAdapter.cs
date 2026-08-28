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
using Newtonsoft.Json.Linq;

namespace SmartHopper.ProviderSdk.AICall.JsonSchemas
{
    /// <summary>
    /// Shared JSON schema adapter for OpenAI-compatible providers.
    /// Wraps non-object root schemas into an object root so providers that require
    /// object-root schemas for structured outputs can consume them.
    /// </summary>
    public class OpenAICompatibleJsonSchemaAdapter : IJsonSchemaAdapter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenAICompatibleJsonSchemaAdapter"/> class.
        /// </summary>
        /// <param name="providerName">The provider name this adapter applies to.</param>
        public OpenAICompatibleJsonSchemaAdapter(string providerName)
        {
            this.ProviderName = providerName ?? throw new ArgumentNullException(nameof(providerName));
        }

        /// <inheritdoc/>
        public string ProviderName { get; }

        /// <inheritdoc/>
        public virtual (JObject wrapped, SchemaWrapperInfo info) Wrap(JObject schema)
        {
            if (schema is null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            var schemaType = schema["type"]?.ToString();

            // Object-root schemas are sent as-is
            if (string.Equals(schemaType, "object", StringComparison.OrdinalIgnoreCase))
            {
                return (schema, new SchemaWrapperInfo
                {
                    IsWrapped = false,
                    ProviderName = this.ProviderName,
                });
            }

            // Arrays are wrapped under an "items" property
            if (string.Equals(schemaType, "array", StringComparison.OrdinalIgnoreCase))
            {
                var wrapped = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["items"] = schema },
                    ["required"] = new JArray { "items" },
                    ["additionalProperties"] = false,
                };

                return (wrapped, new SchemaWrapperInfo
                {
                    IsWrapped = true,
                    WrapperType = "array",
                    PropertyName = "items",
                    ProviderName = this.ProviderName,
                });
            }

            // Primitive types are wrapped under a "value" property
            if (schemaType == "string" || schemaType == "number" || schemaType == "integer" || schemaType == "boolean")
            {
                var wrapped = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["value"] = schema },
                    ["required"] = new JArray { "value" },
                    ["additionalProperties"] = false,
                };

                return (wrapped, new SchemaWrapperInfo
                {
                    IsWrapped = true,
                    WrapperType = schemaType,
                    PropertyName = "value",
                    ProviderName = this.ProviderName,
                });
            }

            // Unknown or missing type: wrap under a generic "data" property
            var generic = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject { ["data"] = schema },
                ["required"] = new JArray { "data" },
                ["additionalProperties"] = false,
            };

            return (generic, new SchemaWrapperInfo
            {
                IsWrapped = true,
                WrapperType = "unknown",
                PropertyName = "data",
                ProviderName = this.ProviderName,
            });
        }

        /// <inheritdoc/>
        public virtual string Unwrap(string content, SchemaWrapperInfo info)
        {
            // Default service logic is sufficient for most OpenAI-compatible providers.
            return content;
        }
    }
}
