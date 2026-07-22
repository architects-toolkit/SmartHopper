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

using Newtonsoft.Json;

namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// Pinned provider/model override for a specific modality fallback conversion.
    /// Null values mean automatic selection.
    /// </summary>
    public sealed class FallbackProviderPin
    {
        /// <summary>Pinned provider name, or null for automatic selection.</summary>
        [JsonProperty]
        public string Provider { get; set; }

        /// <summary>Pinned model name, or null for provider default for the required capability.</summary>
        [JsonProperty]
        public string Model { get; set; }
    }
}
