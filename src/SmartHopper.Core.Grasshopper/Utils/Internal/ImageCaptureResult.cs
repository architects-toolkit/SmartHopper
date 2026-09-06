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

namespace SmartHopper.Core.Grasshopper.Utils.Internal
{
    /// <summary>
    /// Represents an encoded PNG capture and its resolved metadata.
    /// </summary>
    public sealed class ImageCaptureResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageCaptureResult"/> class.
        /// </summary>
        public ImageCaptureResult(string imageBase64, int width, int height, string? viewName = null)
        {
            this.ImageBase64 = imageBase64;
            this.Width = width;
            this.Height = height;
            this.ViewName = viewName;
        }

        /// <summary>Gets the base64-encoded PNG bytes without a data URI prefix.</summary>
        public string ImageBase64 { get; }

        /// <summary>Gets the encoded image width in pixels.</summary>
        public int Width { get; }

        /// <summary>Gets the encoded image height in pixels.</summary>
        public int Height { get; }

        /// <summary>Gets the resolved Rhino view name, when applicable.</summary>
        public string? ViewName { get; }
    }
}
