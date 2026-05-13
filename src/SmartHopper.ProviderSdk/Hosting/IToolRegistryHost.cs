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

using System.Collections.Generic;

namespace SmartHopper.ProviderSdk.Hosting
{
    /// <summary>
    /// Minimal projection of a tool exposed by the host's tool manager, scoped to the
    /// fields a provider needs when formatting tools for an LLM request.
    /// </summary>
    public sealed class ProviderToolDefinition
    {
        /// <summary>Tool identifier (matches host-side <c>AITool.Name</c>).</summary>
        public string Name { get; set; }

        /// <summary>Human-readable description supplied to the model.</summary>
        public string Description { get; set; }

        /// <summary>Tool category (e.g. <c>knowledge</c>, <c>script</c>); free-form.</summary>
        public string Category { get; set; }

        /// <summary>JSON schema fragment describing the tool's argument shape.</summary>
        public string ParametersSchema { get; set; }

        /// <summary>Optional model-side hint. Free-form (e.g. <c>required</c>).</summary>
        public string RequiredUse { get; set; }
    }

    /// <summary>
    /// Surface that the SDK consumes to enumerate tools registered with the host's tool
    /// manager. The host registers an implementation with
    /// <see cref="ProviderSdkHost.ToolRegistry"/>; SDK code calls
    /// <see cref="DiscoverTools"/> to trigger lazy discovery and <see cref="GetTools"/>
    /// to read the current catalog.
    /// </summary>
    public interface IToolRegistryHost
    {
        /// <summary>
        /// Trigger lazy discovery of tools. Idempotent; may be called more than once.
        /// </summary>
        void DiscoverTools();

        /// <summary>
        /// Return the current snapshot of registered tools keyed by tool name.
        /// </summary>
        IReadOnlyDictionary<string, ProviderToolDefinition> GetTools();
    }

    /// <summary>
    /// Empty tool registry used when SDK code runs outside the SmartHopper host.
    /// </summary>
    public sealed class NullToolRegistryHost : IToolRegistryHost
    {
        private static readonly Dictionary<string, ProviderToolDefinition> Empty
            = new Dictionary<string, ProviderToolDefinition>();

        /// <inheritdoc />
        public void DiscoverTools()
        {
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, ProviderToolDefinition> GetTools() => Empty;
    }
}
