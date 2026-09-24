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

namespace SmartHopper.ProviderSdk.AICall.Core.Interactions
{
    /// <summary>
    /// Image payload produced by a tool call. Carried by <see cref="AIInteractionToolResult.Images"/>
    /// so the image stays part of the tool result instead of being embedded as base64 text in the
    /// result JSON. Rendered inside the tool-result bubble in WebChat; emitted to providers only
    /// when <see cref="SendToModel"/> is true and the provider supports image input.
    /// </summary>
    public sealed record ToolResultImage
    {
        /// <summary>
        /// Gets the base64-encoded image data (no data-URI prefix).
        /// </summary>
        public string ImageData { get; init; }

        /// <summary>
        /// Gets the MIME type of the image (e.g. "image/png").
        /// </summary>
        public string MimeType { get; init; } = "image/png";

        /// <summary>
        /// Gets a value indicating whether provider codecs may emit this image as model-visible
        /// input. When false the image is display-only (WebChat render, MCP image block) and is
        /// never sent to the model.
        /// </summary>
        public bool SendToModel { get; init; }

        /// <summary>
        /// Gets the image width in pixels, when known.
        /// </summary>
        public int? Width { get; init; }

        /// <summary>
        /// Gets the image height in pixels, when known.
        /// </summary>
        public int? Height { get; init; }
    }
}
