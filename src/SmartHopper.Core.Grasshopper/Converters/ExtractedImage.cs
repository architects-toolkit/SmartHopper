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

namespace SmartHopper.Core.Grasshopper.Converters
{
    /// <summary>
    /// Represents an image extracted from a document during file conversion.
    /// </summary>
    public sealed class ExtractedImage
    {
        /// <summary>
        /// Gets the unique identifier for this image within the document (e.g., "img-1", "img-2").
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the base64-encoded image data.
        /// </summary>
        public string Base64Data { get; }

        /// <summary>
        /// Gets the MIME type of the image (e.g., "image/png", "image/jpeg").
        /// </summary>
        public string MimeType { get; }

        /// <summary>
        /// Gets a contextual description of where the image was found
        /// (e.g., "Page 3", "Slide 5", "Document body").
        /// </summary>
        public string Context { get; }

        /// <summary>
        /// Gets the page or slide number where the image was found (1-based).
        /// A value of 0 means the location is unknown or not applicable.
        /// </summary>
        public int PageOrSlide { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExtractedImage"/> class.
        /// </summary>
        /// <param name="id">Unique image identifier within the document.</param>
        /// <param name="base64Data">Base64-encoded image data.</param>
        /// <param name="mimeType">MIME type of the image.</param>
        /// <param name="context">Contextual description of image location.</param>
        /// <param name="pageOrSlide">Page or slide number (1-based), or 0 if unknown.</param>
        public ExtractedImage(string id, string base64Data, string mimeType, string context, int pageOrSlide)
        {
            this.Id = id;
            this.Base64Data = base64Data;
            this.MimeType = mimeType;
            this.Context = context;
            this.PageOrSlide = pageOrSlide;
        }
    }
}
