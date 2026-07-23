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
using Newtonsoft.Json;

namespace SmartHopper.Infrastructure.Settings
{
    /// <summary>
    /// Persisted record describing a per-provider trust decision. Replaces the legacy
    /// <c>Dictionary&lt;string, bool&gt;</c> format keyed by assembly file name.
    /// </summary>
    /// <remarks>
    /// Legacy boolean entries continue to be read for backward compatibility: they are
    /// promoted to <see cref="TrustedProviderRecord"/> with
    /// <see cref="Classification"/> set to <c>"Unknown"</c> and the original allow/deny
    /// state preserved. Classification is filled in on next discovery.
    /// </remarks>
    public sealed class TrustedProviderRecord
    {
        /// <summary>
        /// The key used to identify this provider entry. Typically the assembly simple
        /// name, optionally suffixed by SHA-256 hash for unsigned providers.
        /// </summary>
        [JsonProperty("key")]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Cryptographic classification recorded at decision time. Stored as a string
        /// (e.g. <c>"Official"</c>, <c>"Community"</c>, <c>"Unknown"</c>) so future
        /// values added in newer SmartHopper versions don't break older settings files.
        /// </summary>
        [JsonProperty("classification")]
        public string Classification { get; set; } = "Unknown";

        /// <summary>
        /// Whether the user permitted this provider to load.
        /// </summary>
        [JsonProperty("allowed")]
        public bool Allowed { get; set; }

        /// <summary>
        /// When the decision was made.
        /// </summary>
        [JsonProperty("decidedAt")]
        public DateTime DecidedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// SHA-256 hash of the assembly at decision time. For community providers, a
        /// later load with a different hash invalidates the trust.
        /// </summary>
        [JsonProperty("hashAtDecision")]
        public string? HashAtDecision { get; set; }

        /// <summary>
        /// Authenticode signer SHA-1 thumbprint at decision time (Windows only). May be
        /// <c>null</c> on macOS or for unsigned providers.
        /// </summary>
        [JsonProperty("signerThumbprintAtDecision")]
        public string? SignerThumbprintAtDecision { get; set; }

        /// <summary>
        /// Strong-name public key token at decision time. <c>null</c> for unsigned providers.
        /// </summary>
        [JsonProperty("strongNameTokenAtDecision")]
        public string? StrongNameTokenAtDecision { get; set; }
    }
}
