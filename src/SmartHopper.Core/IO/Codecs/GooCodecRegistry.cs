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
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel.Types;

namespace SmartHopper.Core.IO.Codecs
{
    /// <summary>
    /// Registry for <see cref="IGooCodec"/> implementations.
    /// Manages encode/decode resolution for IGH_Goo types in a safe,
    /// versioned persistence pipeline. Never throws during decode.
    /// </summary>
    public static class GooCodecRegistry
    {
        private static readonly List<IGooCodec> Codecs = new List<IGooCodec>();
        private static bool _initialized = false;

        /// <summary>
        /// The separator between type-hint and payload.
        /// </summary>
        internal const string Sep = "|";

        /// <summary>
        /// Ensures built-in codecs are registered.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (Codecs)
            {
                if (_initialized)
                {
                    return;
                }

                RegisterBuiltInCodecs();
                _initialized = true;
            }
        }

        /// <summary>
        /// Registers a codec. Higher-priority codecs are evaluated first during encoding.
        /// </summary>
        public static void Register(IGooCodec codec)
        {
            lock (Codecs)
            {
                Codecs.Add(codec);
                Codecs.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }
        }

        /// <summary>
        /// Unregisters a codec by type-hint.
        /// </summary>
        public static void Unregister(string typeHint)
        {
            lock (Codecs)
            {
                Codecs.RemoveAll(c => c.TypeHint == typeHint);
            }
        }

        /// <summary>
        /// Encodes a goo into a canonical payload string with a type-hint prefix.
        /// </summary>
        public static string Encode(IGH_Goo goo)
        {
            if (goo == null)
            {
                return "null";
            }

            EnsureInitialized();

            lock (Codecs)
            {
                foreach (var codec in Codecs)
                {
                    if (codec.CanEncode(goo))
                    {
                        var payload = codec.Encode(goo);
                        return $"{codec.TypeHint}{Sep}{payload}";
                    }
                }
            }

            // Fallback: best-effort string representation
            return $"GH_String{Sep}{goo.ToString() ?? string.Empty}";
        }

        /// <summary>
        /// Attempts to decode a payload into an IGH_Goo.
        /// For unknown types returns a GH_String. Never throws.
        /// </summary>
        public static bool TryDecode(string payload, out IGH_Goo goo, out string warning)
        {
            goo = null;
            warning = null;

            if (string.IsNullOrEmpty(payload))
            {
                goo = new GH_String(string.Empty);
                return true;
            }

            var idx = payload.IndexOf(Sep, StringComparison.Ordinal);
            if (idx <= 0)
            {
                goo = new GH_String(payload);
                return true;
            }

            var typeHint = payload.Substring(0, idx);
            var data = payload.Substring(idx + Sep.Length);

            EnsureInitialized();

            lock (Codecs)
            {
                foreach (var codec in Codecs)
                {
                    if (codec.TypeHint == typeHint)
                    {
                        return codec.TryDecode(data, out goo, out warning);
                    }
                }
            }

            goo = new GH_String(data);
            warning = $"Unknown type hint '{typeHint}'; restored as GH_String.";
            return true;
        }

        /// <summary>
        /// Gets all registered codecs in priority order.
        /// </summary>
        public static IEnumerable<IGooCodec> GetAll()
        {
            EnsureInitialized();
            lock (Codecs)
            {
                return Codecs.ToList();
            }
        }

        private static void RegisterBuiltInCodecs()
        {
            // Register in priority order (lower first)
            Register(new StringCodec());
            Register(new NumberCodec());
            Register(new IntegerCodec());
            Register(new BooleanCodec());
            Register(new VersatileImageCodec());
            Register(new VersatileAudioCodec());
            Register(new AIInputPayloadCodec());
        }
    }
}
