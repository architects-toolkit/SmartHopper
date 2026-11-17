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
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.JsonSchemas;

namespace SmartHopper.Providers.Anthropic
{
    /// <summary>
    /// Anthropic JSON schema adapter. Inherits the standard wrapping and normalizes
    /// the schema for Anthropic's constrained decoding (<c>output_config.format.schema</c>),
    /// which rejects validation keywords such as <c>maxItems</c> or <c>minimum</c> with
    /// HTTP 400. Stripped constraints are preserved as natural-language hints in the
    /// node's <c>description</c>, mirroring the official Anthropic SDK behavior.
    /// </summary>
    public sealed class AnthropicJsonSchemaAdapter : OpenAICompatibleJsonSchemaAdapter
    {
        /// <summary>
        /// JSON Schema keywords rejected by Anthropic structured outputs.
        /// </summary>
        private static readonly HashSet<string> UnsupportedKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "minimum",
            "maximum",
            "exclusiveMinimum",
            "exclusiveMaximum",
            "multipleOf",
            "minLength",
            "maxLength",
            "pattern",
            "minItems",
            "maxItems",
            "uniqueItems",
            "minProperties",
            "maxProperties",
            "not",
        };

        /// <summary>
        /// String <c>format</c> values Anthropic structured outputs accept; any other
        /// <c>format</c> value is stripped as an unsupported keyword.
        /// </summary>
        private static readonly HashSet<string> SupportedFormats = new HashSet<string>(StringComparer.Ordinal)
        {
            "date-time",
            "time",
            "date",
            "duration",
            "email",
            "hostname",
            "uri",
            "ipv4",
            "ipv6",
            "uuid",
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AnthropicJsonSchemaAdapter"/> class.
        /// </summary>
        public AnthropicJsonSchemaAdapter()
            : base("Anthropic")
        {
        }

        /// <inheritdoc/>
        public override (JObject wrapped, SchemaWrapperInfo info) Wrap(JObject schema)
        {
            if (schema != null)
            {
                NormalizeSchema(schema);
            }

            return base.Wrap(schema);
        }

        /// <summary>
        /// Recursively normalizes a JSON schema for Anthropic structured outputs:
        /// adds <c>additionalProperties: false</c> to every object-type schema
        /// (required by Anthropic) and removes unsupported validation keywords,
        /// appending them as hints to the node's <c>description</c>.
        /// </summary>
        /// <param name="token">The schema token to normalize in place.</param>
        public static void NormalizeSchema(JToken token)
        {
            if (token is JObject obj)
            {
                var type = obj["type"]?.ToString();
                if (type == "object" && !obj.ContainsKey("additionalProperties"))
                {
                    obj["additionalProperties"] = false;
                }

                var stripped = new List<string>();
                foreach (var property in obj.Properties().ToList())
                {
                    var name = property.Name;
                    if (name == "format")
                    {
                        if (property.Value.Type == JTokenType.String && !SupportedFormats.Contains(property.Value.ToString()))
                        {
                            stripped.Add($"format={property.Value}");
                            property.Remove();
                        }

                        continue;
                    }

                    if (!UnsupportedKeywords.Contains(name))
                    {
                        continue;
                    }

                    // Anthropic accepts minItems only when it is 0 or 1
                    if (name == "minItems" && property.Value.Type == JTokenType.Integer)
                    {
                        var minItems = property.Value.Value<int>();
                        if (minItems == 0 || minItems == 1)
                        {
                            continue;
                        }
                    }

                    stripped.Add($"{name}={property.Value.ToString(Formatting.None)}");
                    property.Remove();
                }

                if (stripped.Count > 0)
                {
                    var hint = "Constraints: " + string.Join(", ", stripped) + ".";
                    var existing = obj["description"]?.ToString();
                    obj["description"] = string.IsNullOrEmpty(existing) ? hint : existing + " " + hint;
                }

                foreach (var property in obj.Properties().ToList())
                {
                    NormalizeSchema(property.Value);
                }
            }
            else if (token is JArray arr)
            {
                foreach (var item in arr)
                {
                    NormalizeSchema(item);
                }
            }
        }
    }
}
