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
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Infrastructure.Interaction
{
    /// <summary>
    /// Describes one canvas pointing request: what to highlight on the Grasshopper canvas
    /// and which message to show next to it in chat.
    /// </summary>
    public sealed class CanvasPointerRequest
    {
        /// <summary>Gets or sets the unique pointer identifier used for replay.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the user-facing message rendered in the chat card.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Gets or sets the document object instance GUIDs to highlight.</summary>
        public IReadOnlyList<Guid> Guids { get; set; } = new List<Guid>();

        /// <summary>Gets or sets the optional canvas region origin X (world coordinates).</summary>
        public float? X { get; set; }

        /// <summary>Gets or sets the optional canvas region origin Y (world coordinates).</summary>
        public float? Y { get; set; }

        /// <summary>Gets or sets the optional canvas region width.</summary>
        public float? Width { get; set; }

        /// <summary>Gets or sets the optional canvas region height.</summary>
        public float? Height { get; set; }

        /// <summary>Gets or sets the highlight duration in seconds.</summary>
        public int DurationSeconds { get; set; } = 8;

        /// <summary>Gets a value indicating whether a canvas region target was provided.</summary>
        public bool HasRegion =>
            this.X.HasValue && this.Y.HasValue && this.Width.HasValue && this.Height.HasValue;
    }

    /// <summary>
    /// Renders the chat-side card of a <see cref="CanvasPointerRequest"/> without owning
    /// the canvas highlight itself. The card carries the pointer id so the user can
    /// re-trigger the pan/zoom/highlight later via <see cref="CanvasPointerBridge"/>.
    /// </summary>
    public interface ICanvasPointerPresenter
    {
        /// <summary>Presents the pointer card to the user.</summary>
        /// <param name="request">The pointer request to render.</param>
        /// <param name="cancellationToken">Cancellation for the presentation request.</param>
        Task ShowAsync(CanvasPointerRequest request, CancellationToken cancellationToken);
    }
}
