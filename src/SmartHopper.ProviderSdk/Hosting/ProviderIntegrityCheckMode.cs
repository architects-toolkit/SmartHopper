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

namespace SmartHopper.ProviderSdk.Hosting
{
    /// <summary>
    /// Provider integrity check mode defining how verification failures are handled by
    /// the host trust pipeline. Lives in the SDK so that DTOs in SDK code paths can
    /// reason about it without depending on the SmartHopper host.
    /// </summary>
    public enum ProviderIntegrityCheckMode
    {
        /// <summary>
        /// Strict mode: blocks providers on hash mismatch, unavailable hash repository,
        /// and unknown providers (not present in the official manifest).
        /// </summary>
        Strict,

        /// <summary>
        /// Hard mode: blocks providers on hash mismatch and unknown providers, but
        /// allows loading when the hash repository is unreachable.
        /// </summary>
        Hard,

        /// <summary>
        /// Soft mode: surfaces warnings rather than blocking. Best suited for
        /// development and third-party providers.
        /// </summary>
        Soft,
    }
}
