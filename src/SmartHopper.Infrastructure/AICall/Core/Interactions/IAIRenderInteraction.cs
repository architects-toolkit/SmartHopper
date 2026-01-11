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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Defines how an interaction should be rendered into the WebChat HTML.
    /// Implement this on interaction types to eliminate type switches in renderers.
    /// </summary>
    public interface IAIRenderInteraction
    {
        /// <summary>
        /// Gets the CSS role class to use for the message container (e.g., "assistant", "user", "tool", "error").
        /// </summary>
        /// <returns>Role CSS class name.</returns>
        string GetRoleClassForRender();

        /// <summary>
        /// Gets the display name (header label) for the message, e.g., "Assistant", "Tool Call: name".
        /// </summary>
        /// <returns>Display label.</returns>
        string GetDisplayNameForRender();

        /// <summary>
        /// Gets the raw markdown content to render in the message body.
        /// This will be converted to HTML by the ChatResourceManager.
        /// </summary>
        /// <returns>Markdown content string.</returns>
        string GetRawContentForRender();

        /// <summary>
        /// Gets the raw reasoning content (optionally wrapped in &lt;think&gt; tags) to render
        /// in a collapsible reasoning panel above the answer. Return empty string to omit.
        /// </summary>
        /// <returns>Reasoning content string or empty.</returns>
        string GetRawReasoningForRender();
    }
}
