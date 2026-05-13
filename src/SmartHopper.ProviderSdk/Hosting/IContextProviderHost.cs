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
    /// Surface that the SDK consumes to retrieve system-prompt context contributions
    /// from the host's context manager (Grasshopper canvas state, user preferences,
    /// document-scoped data, etc.).
    /// </summary>
    public interface IContextProviderHost
    {
        /// <summary>
        /// Resolve the active context bag for the given provider filter expression.
        /// Keys are provider/contributor identifiers and values are arbitrary text or
        /// JSON fragments to be embedded in the system prompt.
        /// </summary>
        /// <param name="providerFilter">Optional space-separated list of contributor ids
        /// to include. Pass null/empty to request the entire context bag.</param>
        IDictionary<string, string> GetCurrentContext(string providerFilter = null);
    }

    /// <summary>
    /// No-op context provider that returns an empty dictionary. Used when SDK code runs
    /// outside the SmartHopper host (e.g. unit tests).
    /// </summary>
    public sealed class NullContextProviderHost : IContextProviderHost
    {
        /// <inheritdoc />
        public IDictionary<string, string> GetCurrentContext(string providerFilter = null)
            => new Dictionary<string, string>();
    }
}
