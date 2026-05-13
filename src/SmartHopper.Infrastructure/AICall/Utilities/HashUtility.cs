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
using System.Security.Cryptography;
using System.Text;

namespace SmartHopper.Infrastructure.AICall.Utilities
{
    /// <summary>
    /// Shared hashing utilities for generating consistent identifiers.
    /// </summary>
    public static class HashUtility
    {
        /// <summary>
        /// Computes a short (16 hex character) SHA256-based hash for stable key generation.
        /// Used for deduplication keys and stream identifiers across interactions.
        /// </summary>
        /// <param name="value">Input string to hash.</param>
        /// <returns>Lowercase 16-character hex substring of the SHA256 hash.</returns>
        public static string ComputeShortHash(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            var hash = SHA256.HashData(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant().Substring(0, 16);
        }
    }
}
