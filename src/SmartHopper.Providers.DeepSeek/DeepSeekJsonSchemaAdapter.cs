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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.JsonSchemas;

namespace SmartHopper.Providers.DeepSeek
{
    internal sealed partial class DeepSeekJsonSchemaAdapter : IJsonSchemaAdapter
    {
        #region Compiled Regex Patterns

        /// <summary>
        /// Regex pattern for extracting enum arrays from malformed JSON.
        /// </summary>
        [GeneratedRegex(@"enum[""']?:\s*\[([^\]]+)\]")]
        private static partial Regex EnumArrayRegex();

        #endregion

        public string ProviderName => "DeepSeek";

        public (JObject wrapped, SchemaWrapperInfo info) Wrap(JObject schema)
        {
            if (schema is null) throw new ArgumentNullException(nameof(schema));
            var schemaType = schema["type"]?.ToString();

            // DeepSeek also behaves better with object-root schemas
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
            if (string.IsNullOrWhiteSpace(content)) return content;

            // DeepSeek sometimes returns malformed JSON where array is put under an "enum" property
            try
            {
                var trimmed = content.TrimStart();
                if (trimmed.StartsWith("{"))
                {
                    var obj = JObject.Parse(content);
                    if (obj["enum"] is JArray enumArray)
                    {
                        var cleanedArray = enumArray.ToString(Newtonsoft.Json.Formatting.None);
                        Debug.WriteLine($"[DeepSeekAdapter] Cleaned enum array: {cleanedArray}");
                        return cleanedArray;
                    }
                }
            }
            catch
            {
                // Fall through to regex attempt
            }

            try
            {
                // Fallback: try regex extraction if JSON parsing fails
                var match = EnumArrayRegex().Match(content);
                if (match.Success)
                {
                    var inner = match.Groups[1].Value;
                    var cleaned = $"[{inner}]";
                    Debug.WriteLine($"[DeepSeekAdapter] Regex extracted enum array: {cleaned}");
                    return cleaned;
                }
            }
            catch
            {
                // ignore
            }

            return content;
        }
    }
}
