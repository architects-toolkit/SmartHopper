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
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
namespace SmartHopper.ProviderSdk.AICall.JsonSchemas
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

        public static IJsonSchemaAdapter Default { get; } = new OpenAICompatibleJsonSchemaAdapter("__default__");

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


}
