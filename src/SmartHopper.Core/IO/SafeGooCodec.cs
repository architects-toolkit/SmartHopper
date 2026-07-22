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

// Purpose: Encode/decode IGH_Goo instances to a canonical string payload to allow safe, versioned persistence
// without invoking GH internal type cache on read. Supports common primitives and safely falls back to GH_String.
using System;
using System.Globalization;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.IO.Codecs;

namespace SmartHopper.Core.IO
{
    /// <summary>
    /// Provides safe encoding/decoding between IGH_Goo and string payloads.
    /// Delegates to <see cref="GooCodecRegistry"/> for extensible type resolution.
    /// </summary>
    public static class SafeGooCodec
    {
        /// <summary>
        /// Encodes a goo into a canonical payload string with a type hint prefix.
        /// </summary>
        public static string Encode(IGH_Goo goo)
        {
            return GooCodecRegistry.Encode(goo);
        }

        /// <summary>
        /// Attempts to decode a payload into an IGH_Goo. For unknown types returns a GH_String.
        /// Never throws.
        /// </summary>
        public static bool TryDecode(string payload, out IGH_Goo goo, out string warning)
        {
            return GooCodecRegistry.TryDecode(payload, out goo, out warning);
        }
    }
}
