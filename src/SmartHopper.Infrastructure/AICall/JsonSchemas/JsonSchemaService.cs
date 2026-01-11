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
using System.Threading;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.AICall.JsonSchemas
{
    /// <summary>
    /// Default implementation of IJsonSchemaService.
    /// </summary>
    public sealed class JsonSchemaService : IJsonSchemaService
    {
        private static readonly Lazy<JsonSchemaService> _instance = new Lazy<JsonSchemaService>(() => new JsonSchemaService());
        private static readonly AsyncLocal<SchemaWrapperInfo> _currentWrapper = new AsyncLocal<SchemaWrapperInfo>();

        public static JsonSchemaService Instance => _instance.Value;

        private JsonSchemaService()
        {
        }

        public bool TryParseSchema(string schemaJson, out JObject schema, out string error)
        {
            schema = new JObject();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(schemaJson))
            {
                error = "Schema is empty";
                return false;
            }

            try
            {
                schema = JObject.Parse(schemaJson);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Invalid JSON schema: {ex.Message}";
                return false;
            }
        }

        public (JObject wrapped, SchemaWrapperInfo info) WrapForProvider(JObject schema, string provider)
        {
            if (schema is null) throw new ArgumentNullException(nameof(schema));
            var adapter = JsonSchemaAdapterRegistry.Get(provider);
            var (wrapped, info) = adapter.Wrap(schema);

            // Ensure provider name is captured
            info.ProviderName = string.IsNullOrWhiteSpace(info.ProviderName) ? (provider ?? string.Empty) : info.ProviderName;
            return (wrapped, info);
        }

        public string Unwrap(string content, SchemaWrapperInfo info)
        {
            if (info == null || !info.IsWrapped || string.IsNullOrWhiteSpace(content))
            {
                return content;
            }

            try
            {
                // Allow provider adapter to pre-process content (e.g., cleanup) before default extraction
                var adapter = JsonSchemaAdapterRegistry.Get(info.ProviderName);
                var preProcessed = adapter.Unwrap(content, info) ?? content;

                var obj = JObject.Parse(preProcessed);
                var value = obj[info.PropertyName];
                if (value == null) return preProcessed;
                if (value.Type == JTokenType.Array || value.Type == JTokenType.Object)
                {
                    return value.ToString(Newtonsoft.Json.Formatting.None);
                }

                return value.ToString();
            }
            catch
            {
                // On failure, return original content
                return content;
            }
        }

        public bool Validate(string schemaJson, string json, out string error)
        {
            error = string.Empty;

            // Minimal validation: ensure both are valid JSON. Full JSON Schema validation can be added later.
            try { _ = JObject.Parse(schemaJson); }
            catch (Exception ex)
            {
                error = $"Invalid schema JSON: {ex.Message}";
                return false;
            }

            try { _ = JToken.Parse(json); }
            catch (Exception ex)
            {
                error = $"Invalid instance JSON: {ex.Message}";
                return false;
            }

            return true;
        }

        public void SetCurrentWrapperInfo(SchemaWrapperInfo info)
        {
            _currentWrapper.Value = info;
        }

        public SchemaWrapperInfo GetCurrentWrapperInfo()
        {
            return _currentWrapper.Value;
        }
    }
}
