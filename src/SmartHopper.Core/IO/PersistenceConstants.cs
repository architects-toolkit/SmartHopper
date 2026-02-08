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

// Purpose: Central constants and keys for component persistence (versioned schema)
// This file defines keys and version numbers used by the persistence service to
// read and write component outputs in a robust, forward-compatible way.

using System;

namespace SmartHopper.Core.IO
{
    /// <summary>
    /// Constants for versioned persistence of component data.
    /// </summary>
    public static class PersistenceConstants
    {
        /// <summary>
        /// Global version key stored at component scope.
        /// </summary>
        public const string VersionKey = "PO.Version";

        /// <summary>
        /// Current persistence format version.
        /// </summary>
        public const int CurrentVersion = 2;

        /// <summary>
        /// Prefix for per-output stored payloads for version 2.
        /// Keys are PO2_{paramGuidN}.
        /// </summary>
        public const string OutKeyPrefixV2 = "PO2_";

        /// <summary>
        /// Build the writer/reader key for an output parameter GUID.
        /// </summary>
        public static string KeyForOutputV2(Guid paramGuid) => OutKeyPrefixV2 + paramGuid.ToString("N");

        /// <summary>
        /// Feature flag: allow attempting legacy (v1) restore using GH internals.
        /// Disabled by default to avoid crashes from malformed legacy chunks. Set to true only for migration.
        /// </summary>
        public const bool EnableLegacyRestore = false;
    }
}
