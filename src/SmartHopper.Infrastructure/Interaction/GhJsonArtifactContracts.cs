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
    /// One validation problem reported for a <see cref="GhJsonArtifact"/>: a JSON-path-ish
    /// location and a human-readable message, already formatted for the chat card.
    /// </summary>
    public sealed class GhJsonArtifactIssue
    {
        /// <summary>Gets or sets the issue location (for example <c>$.components[2]</c>, or empty for the document root).</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Gets or sets the issue description.</summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Describes one GhJSON artifact card: the document text to display, its schema
    /// validity and the validation issues to surface when it is not valid.
    /// </summary>
    public sealed class GhJsonArtifact
    {
        /// <summary>Gets or sets the unique artifact identifier used by place-on-canvas replay.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the optional user-facing card title.</summary>
        public string? Title { get; set; }

        /// <summary>Gets or sets the GhJSON document text rendered in the card's collapsed preview.</summary>
        public string GhJson { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the document passed GhJSON schema validation.</summary>
        public bool Valid { get; set; }

        /// <summary>Gets or sets the number of declared components (valid documents only).</summary>
        public int Components { get; set; }

        /// <summary>Gets or sets the number of declared connections (valid documents only).</summary>
        public int Connections { get; set; }

        /// <summary>Gets or sets the validation issues to list when <see cref="Valid"/> is false.</summary>
        public IReadOnlyList<GhJsonArtifactIssue> Errors { get; set; } = new List<GhJsonArtifactIssue>();
    }

    /// <summary>
    /// Renders the chat-side card of a <see cref="GhJsonArtifact"/> without owning the
    /// canvas placement itself. The card carries the artifact id so a later
    /// place-on-canvas click can resolve the stored document.
    /// </summary>
    public interface IGhJsonArtifactPresenter
    {
        /// <summary>Presents the GhJSON artifact card to the user.</summary>
        /// <param name="artifact">The artifact to render.</param>
        /// <param name="cancellationToken">Cancellation for the presentation request.</param>
        Task ShowAsync(GhJsonArtifact artifact, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Outcome of a place-on-canvas request resolved through <see cref="GhJsonCanvasBridge"/>.
    /// </summary>
    public sealed class GhJsonPlaceOutcome
    {
        /// <summary>Gets or sets a value indicating whether the document was placed.</summary>
        public bool Success { get; set; }

        /// <summary>Gets or sets the number of objects that landed on the canvas.</summary>
        public int PlacedCount { get; set; }

        /// <summary>Gets or sets the user-facing failure reason when <see cref="Success"/> is false.</summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Process-wide bridge that lets chat surfaces place a GhJSON document on the active
    /// Grasshopper canvas without holding a reference to the Grasshopper layer.
    /// The Grasshopper layer assigns <see cref="PlaceHandler"/> at plugin load;
    /// callers pass the document text only.
    /// </summary>
    public static class GhJsonCanvasBridge
    {
        /// <summary>
        /// Gets or sets the handler that places a GhJSON document on the active canvas.
        /// Signature: (ghjsonText, cancellationToken) => the placement outcome.
        /// Null when no Grasshopper placer is active.
        /// </summary>
        public static Func<string, CancellationToken, Task<GhJsonPlaceOutcome>>? PlaceHandler { get; set; }
    }
}
