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
using System.Drawing;
using Grasshopper.Kernel.Types;

namespace SmartHopper.Core.Types
{
    /// <summary>
    /// Grasshopper goo wrapper for VersatileImage.
    /// Accepts Bitmap, file paths, URLs, base64, data-URIs, and extracted document images.
    /// </summary>
    public class GH_VersatileImage : GH_Goo<VersatileImage>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GH_VersatileImage"/> class.
        /// </summary>
        public GH_VersatileImage()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GH_VersatileImage"/> class with an image source.
        /// </summary>
        /// <param name="imageSource">The VersatileImage to wrap.</param>
        public GH_VersatileImage(VersatileImage imageSource)
            : base(imageSource)
        {
        }

        /// <summary>
        /// Gets a value indicating whether the image source is valid.
        /// </summary>
        public override bool IsValid => this.Value != null;

        /// <summary>
        /// Gets the type name for display.
        /// </summary>
        public override string TypeName => "VersatileImage";

        /// <summary>
        /// Gets the type description.
        /// </summary>
        public override string TypeDescription => "A versatile image type accepting Bitmap, file paths, URLs, base64, data-URIs, and extracted document images with metadata";

        /// <summary>
        /// Duplicates this goo.
        /// </summary>
        /// <returns>A duplicate of this goo.</returns>
        public override IGH_Goo Duplicate()
        {
            if (this.Value == null)
            {
                return new GH_VersatileImage();
            }

            // Create a new VersatileImage with the same kind and values
            VersatileImage newSource;
            if (this.Value.Kind == VersatileImageKind.Bitmap && this.Value.Bitmap != null)
            {
                newSource = VersatileImage.FromBitmap(new Bitmap(this.Value.Bitmap));
            }
            else
            {
                newSource = VersatileImage.FromString(this.Value.RawValue);
            }

            return new GH_VersatileImage(newSource);
        }

        /// <summary>
        /// Gets a string representation of this goo.
        /// </summary>
        /// <returns>A string representation.</returns>
        public override string ToString()
        {
            if (this.Value == null)
            {
                return "Null VersatileImage";
            }

            var baseDesc = this.Value.Kind switch
            {
                VersatileImageKind.Bitmap => "VersatileImage (Bitmap)",
                VersatileImageKind.LocalFile => $"VersatileImage (File: {this.Value.RawValue})",
                VersatileImageKind.Url => $"VersatileImage (URL: {this.Value.RawValue})",
                VersatileImageKind.Base64 => "VersatileImage (Base64)",
                VersatileImageKind.DataUri => "VersatileImage (Data-URI)",
                _ => "VersatileImage (Unknown)",
            };

            // Append document metadata if present
            if (!string.IsNullOrEmpty(this.Value.Id))
            {
                baseDesc += $" [{this.Value.Id}";
                if (!string.IsNullOrEmpty(this.Value.Context))
                {
                    baseDesc += $", {this.Value.Context}";
                }

                if (this.Value.PageOrSlide > 0)
                {
                    baseDesc += $", Page {this.Value.PageOrSlide}";
                }

                baseDesc += "]";
            }

            return baseDesc;
        }

        /// <summary>
        /// Attempts to cast from another goo type.
        /// </summary>
        /// <param name="source">The source goo or object.</param>
        /// <returns>True if cast succeeded; otherwise false.</returns>
        public override bool CastFrom(object source)
        {
            if (source is GH_VersatileImage imageGoo)
            {
                this.Value = imageGoo.Value;
                return true;
            }

            if (source is VersatileImage imageSource)
            {
                this.Value = imageSource;
                return true;
            }

            if (source is Bitmap bitmap)
            {
                this.Value = VersatileImage.FromBitmap(bitmap);
                return true;
            }

            if (source is GH_ObjectWrapper wrapper && wrapper.Value is Bitmap wrappedBitmap)
            {
                this.Value = VersatileImage.FromBitmap(wrappedBitmap);
                return true;
            }

            if (source is GH_String ghString && ghString.IsValid)
            {
                try
                {
                    this.Value = VersatileImage.FromString(ghString.Value);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            if (source is string stringValue)
            {
                try
                {
                    this.Value = VersatileImage.FromString(stringValue);
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
        /// Attempts to cast to another goo type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="target">The target goo (output).</param>
        /// <returns>True if cast succeeded; otherwise false.</returns>
        public override bool CastTo<T>(ref T target)
        {
            if (typeof(T) == typeof(GH_VersatileImage))
            {
                target = (T)(object)new GH_VersatileImage(this.Value);
                return true;
            }

            if (typeof(T) == typeof(VersatileImage))
            {
                target = (T)(object)this.Value;
                return true;
            }

            if (typeof(T) == typeof(Bitmap))
            {
                try
                {
                    target = (T)(object)this.Value.ToBitmap();
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            if (typeof(T) == typeof(GH_String))
            {
                var str = this.ToString();
                target = (T)(object)new GH_String(str);
                return true;
            }

            return false;
        }
    }
}
