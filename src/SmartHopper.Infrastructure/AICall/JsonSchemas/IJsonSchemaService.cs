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

using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.AICall.JsonSchemas
{
    /// <summary>
    /// Centralized service for handling JSON schema wrapping, unwrapping and validation.
    /// Thread-safe and provider-aware. Keeps ephemeral wrapper info in thread-local context.
    /// </summary>
    public interface IJsonSchemaService
    {
        /// <summary>
        /// Attempts to parse the schema text into a JObject.
        /// </summary>
        /// <param name="schemaJson">Raw schema JSON text.</param>
        /// <param name="schema">Parsed schema JObject on success.</param>
        /// <param name="error">Error message on failure.</param>
        /// <returns>True if parsed; otherwise false.</returns>
        bool TryParseSchema(string schemaJson, out JObject schema, out string error);

        /// <summary>
        /// Provider-aware schema transformation. For providers that require object-root schemas
        /// (e.g., OpenAI), non-object schemas are wrapped into an object with a single property.
        /// </summary>
        /// <param name="schema">Original schema (parsed).</param>
        /// <param name="provider">Provider name (e.g., "OpenAI").</param>
        /// <returns>Wrapped schema and wrapper info metadata.</returns>
        (JObject wrapped, SchemaWrapperInfo info) WrapForProvider(JObject schema, string provider);

        /// <summary>
        /// Unwraps a JSON content string using the provided wrapper info.
        /// </summary>
        /// <param name="content">JSON content string.</param>
        /// <param name="info">Wrapper metadata previously returned by WrapForProvider.</param>
        /// <returns>Unwrapped JSON string (or original if not wrapped).</returns>
        string Unwrap(string content, SchemaWrapperInfo info);

        /// <summary>
        /// Validates a JSON instance string against a schema string.
        /// Lightweight: currently performs parse checks only (no full JSON Schema validation).
        /// </summary>
        /// <param name="schemaJson">JSON schema string.</param>
        /// <param name="json">JSON instance string.</param>
        /// <param name="error">Error message on failure.</param>
        /// <returns>True if structurally valid; otherwise false.</returns>
        bool Validate(string schemaJson, string json, out string error);

        /// <summary>
        /// Sets the current thread's wrapper info so response mappers can unwrap consistently.
        /// </summary>
        /// <param name="info">Wrapper info.</param>
        void SetCurrentWrapperInfo(SchemaWrapperInfo info);

        /// <summary>
        /// Gets the current thread's wrapper info, if any.
        /// </summary>
        /// <returns>Wrapper info or null.</returns>
        SchemaWrapperInfo GetCurrentWrapperInfo();
    }

    /// <summary>
    /// Information about schema wrapping for response unwrapping.
    /// </summary>
    public sealed class SchemaWrapperInfo
    {
        /// <summary>
        /// Gets or sets a value indicating whether the schema was wrapped.
        /// </summary>
        public bool IsWrapped { get; set; }

        /// <summary>
        /// Gets or sets the type of the wrapper.
        /// </summary>
        public string WrapperType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the property that contains the wrapped content.
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the provider whose adapter produced this wrapper info.
        /// Used to retrieve the same adapter during unwrapping.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;
    }
}
