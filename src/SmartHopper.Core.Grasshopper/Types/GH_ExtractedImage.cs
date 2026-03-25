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
using System.IO;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.Grasshopper.Converters;

namespace SmartHopper.Core.Grasshopper.Types
{
    /// <summary>
    /// Grasshopper wrapper for <see cref="ExtractedImage"/>.
    /// Compatible with <c>ImageViewerComponent</c>: <see cref="ScriptVariable"/> returns a <see cref="Bitmap"/>.
    /// Supports cast to <see cref="GH_String"/> (base64 data) and from base64 strings.
    /// </summary>
    public class GH_ExtractedImage : GH_Goo<ExtractedImage>
    {
        /// <summary>Initializes a new empty instance.</summary>
        public GH_ExtractedImage() : base((ExtractedImage)null) { }

        /// <summary>Initializes a new instance wrapping the given extracted image.</summary>
        /// <param name="value">The extracted image to wrap.</param>
        public GH_ExtractedImage(ExtractedImage value) : base(value) { }

        /// <inheritdoc/>
        public override bool IsValid => this.Value != null && !string.IsNullOrEmpty(this.Value.Base64Data);

        /// <inheritdoc/>
        public override string IsValidWhyNot => this.IsValid ? string.Empty : "No image data";

        /// <inheritdoc/>
        public override string TypeName => "Extracted Image";

        /// <inheritdoc/>
        public override string TypeDescription => "An image extracted from a document, stored as base64 data with source metadata (ID, MIME type, page/slide, context).";

        /// <inheritdoc/>
        public override IGH_Goo Duplicate() => new GH_ExtractedImage(this.Value);

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.Value == null) return "Null Extracted Image";
            return $"Image [{this.Value.Id}] {this.Value.Context} ({this.Value.MimeType})";
        }

        /// <summary>
        /// Returns the image decoded as a <see cref="Bitmap"/>, for compatibility with ImageViewerComponent.
        /// </summary>
        /// <returns>A <see cref="Bitmap"/> decoded from the base64 data, or null on failure.</returns>
        public override object ScriptVariable()
        {
            if (this.Value?.Base64Data == null) return null;
            try
            {
                var bytes = Convert.FromBase64String(this.Value.Base64Data);
                using var ms = new MemoryStream(bytes);
                return new Bitmap(ms);
            }
            catch
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q) == typeof(Bitmap))
            {
                if (this.ScriptVariable() is Bitmap bmp)
                {
                    target = (Q)(object)bmp;
                    return true;
                }
                return false;
            }

            if (typeof(Q) == typeof(GH_String))
            {
                target = (Q)(object)new GH_String(this.Value?.Base64Data ?? string.Empty);
                return true;
            }

            if (typeof(Q) == typeof(string))
            {
                target = (Q)(object)(this.Value?.Base64Data ?? string.Empty);
                return true;
            }

            return base.CastTo<Q>(ref target);
        }

        /// <inheritdoc/>
        public override bool CastFrom(object source)
        {
            if (source == null) return false;

            if (source is GH_ExtractedImage ghImg)
            {
                this.Value = ghImg.Value;
                return true;
            }

            if (source is ExtractedImage img)
            {
                this.Value = img;
                return true;
            }

            string b64 = null;
            if (source is GH_String ghStr)
            {
                b64 = ghStr.Value;
            }
            else if (source is string s)
            {
                b64 = s;
            }

            if (b64 != null)
            {
                this.Value = new ExtractedImage("img", b64, "image/png", "Unknown", 0);
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public override bool Write(GH_IWriter writer)
        {
            if (this.Value == null) return true;
            writer.SetString("Id", this.Value.Id ?? string.Empty);
            writer.SetString("Base64Data", this.Value.Base64Data ?? string.Empty);
            writer.SetString("MimeType", this.Value.MimeType ?? "image/png");
            writer.SetString("Context", this.Value.Context ?? string.Empty);
            writer.SetInt32("PageOrSlide", this.Value.PageOrSlide);
            return true;
        }

        /// <inheritdoc/>
        public override bool Read(GH_IReader reader)
        {
            try
            {
                if (!reader.ItemExists("Base64Data")) return true;
                var id = reader.ItemExists("Id") ? reader.GetString("Id") : "img";
                var b64 = reader.GetString("Base64Data");
                var mime = reader.ItemExists("MimeType") ? reader.GetString("MimeType") : "image/png";
                var ctx = reader.ItemExists("Context") ? reader.GetString("Context") : string.Empty;
                var page = reader.ItemExists("PageOrSlide") ? reader.GetInt32("PageOrSlide") : 0;
                this.Value = new ExtractedImage(id, b64, mime, ctx, page);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
