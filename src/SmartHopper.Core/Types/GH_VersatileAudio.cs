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
using Grasshopper.Kernel.Types;

namespace SmartHopper.Core.Types
{
    /// <summary>
    /// Grasshopper goo wrapper for VersatileAudio.
    /// </summary>
    public class GH_VersatileAudio : GH_Goo<VersatileAudio>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GH_VersatileAudio"/> class.
        /// </summary>
        public GH_VersatileAudio()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GH_VersatileAudio"/> class.
        /// </summary>
        /// <param name="audio">The VersatileAudio instance to wrap.</param>
        public GH_VersatileAudio(VersatileAudio audio)
            : base(audio)
        {
        }

        /// <summary>
        /// Gets a value indicating whether the audio is valid.
        /// </summary>
        public override bool IsValid => this.Value != null;

        /// <summary>
        /// Gets the type name of the goo.
        /// </summary>
        public override string TypeName => "VersatileAudio";

        /// <summary>
        /// Gets the type description.
        /// </summary>
        public override string TypeDescription => "A versatile audio type accepting file paths, URLs, base64, data-URIs, and extracted document audio with metadata.";

        /// <summary>
        /// Gets a string representation of the audio.
        /// </summary>
        public override string ToString()
        {
            if (this.Value == null)
            {
                return "null";
            }

            return $"VersatileAudio ({this.Value.Kind}): {this.Value.RawValue}";
        }

        /// <summary>
        /// Duplicates this goo.
        /// </summary>
        /// <returns>A duplicate of this goo.</returns>
        public override IGH_Goo Duplicate()
        {
            return new GH_VersatileAudio(this.Value);
        }

        /// <summary>
        /// Attempts to cast the given data to VersatileAudio.
        /// </summary>
        /// <param name="source">The source data.</param>
        /// <returns>True if cast was successful; otherwise false.</returns>
        public override bool CastFrom(object source)
        {
            if (source == null)
            {
                return false;
            }

            if (source is GH_VersatileAudio audio)
            {
                this.Value = audio.Value;
                return true;
            }

            if (source is VersatileAudio versatileAudio)
            {
                this.Value = versatileAudio;
                return true;
            }

            if (source is string str)
            {
                try
                {
                    this.Value = VersatileAudio.FromString(str);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to cast this goo to the given type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="target">The target goo (output).</param>
        /// <returns>True if cast succeeded; otherwise false.</returns>
        public override bool CastTo<T>(ref T target)
        {
            if (typeof(T) == typeof(GH_VersatileAudio))
            {
                target = (T)(object)new GH_VersatileAudio(this.Value);
                return true;
            }

            if (typeof(T) == typeof(VersatileAudio))
            {
                target = (T)(object)this.Value;
                return true;
            }

            if (typeof(T) == typeof(GH_String))
            {
                var str = this.ToString();
                target = (T)(object)new GH_String(str);
                return true;
            }

            if (typeof(T) == typeof(string))
            {
                target = (T)(object)(this.Value?.RawValue ?? string.Empty);
                return true;
            }

            return false;
        }
    }
}
