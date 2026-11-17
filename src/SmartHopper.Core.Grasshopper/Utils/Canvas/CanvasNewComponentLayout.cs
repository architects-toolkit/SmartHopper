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
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Core.SchemaModels;
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.Deserialization;
using GhJSON.Grasshopper.LayoutRefinements;
using GhJSON.Grasshopper.PutOperations;
using GhJSON.Grasshopper.Shared;
using Grasshopper.Kernel;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Fills missing canvas pivots on new components of a proposed GhJSON document
    /// by running the shared GhJSON layout pipeline — the core dependency-graph pass
    /// plus the Grasshopper refinements (bounds-aware spacing, port alignment, wire
    /// corridors, collision resolution) that <see cref="GhJsonGrasshopper.ComputeLayout"/>
    /// exposes and gh_tidy_up uses. Writing the computed pivots back into the document
    /// before it is staged means the canvas review previews the exact final placement
    /// while preserving all supplied pivots, and the later
    /// <see cref="GhJsonGrasshopper.Put(GhJsonDocument, PutOptions)"/> applies the
    /// positions verbatim.
    /// </summary>
    public static class CanvasNewComponentLayout
    {
        private const float AutoOffsetSpacing = 100f;
        private const float FallbackCascadeSpacing = 40f;

        /// <summary>Maximum time the layout post may sit unclaimed in the UI queue.</summary>
        private static readonly TimeSpan QueuedStartBudget = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Marshals <see cref="ApplyOnUiThread"/> onto the Rhino UI thread through the
        /// shared bounded-post contract: the queued-start budget bounds admission
        /// only, an expired callback never runs, a started callback owns its outcome,
        /// and cancellation reaches the post while still queued.
        /// </summary>
        /// <param name="document">Proposed document whose new components get laid out.</param>
        /// <param name="preservedInstanceGuids">
        /// Instance GUIDs of document components that update live objects in place;
        /// those keep their position and are excluded from the layout. Pass
        /// <c>null</c> or empty when every component places as a new object.
        /// </param>
        /// <param name="offsetBelowExisting">
        /// When <c>true</c>, disconnected parts without fixed anchors are placed below
        /// existing canvas content (same convention as <see cref="PutOptions.AutoOffset"/>).
        /// </param>
        /// <param name="cancellationToken">Cancellation for the marshalled call.</param>
        /// <returns>The same document with final pivots baked into new components.</returns>
        public static async Task<GhJsonDocument> ApplyAsync(
            GhJsonDocument document,
            IReadOnlySet<Guid>? preservedInstanceGuids,
            bool offsetBelowExisting,
            CancellationToken cancellationToken)
        {
            var post = await GhJsonGrasshopper.PostToUiThreadAsync(
                () => ApplyOnUiThread(document, preservedInstanceGuids, offsetBelowExisting),
                QueuedStartBudget,
                cancellationToken).ConfigureAwait(false);

            switch (post.State)
            {
                case GhJSON.Core.Hosting.UiPostState.Completed:
                    return post.Value!;
                case GhJSON.Core.Hosting.UiPostState.CancelledNotStarted:
                    throw new OperationCanceledException(cancellationToken);
                case GhJSON.Core.Hosting.UiPostState.BusyNotStarted:
                    throw new TimeoutException(
                        $"Grasshopper UI thread did not admit the layout call within {QueuedStartBudget.TotalSeconds:n0} s.");
                case GhJSON.Core.Hosting.UiPostState.NotPosted:
                    throw new InvalidOperationException(
                        "The Grasshopper UI thread is unavailable; the layout call was not queued.", post.Error);
                default:
                    throw new InvalidOperationException(
                        "Layout computation on the Grasshopper UI thread failed.", post.Error);
            }
        }

        /// <summary>
        /// Computes compact positions only for pivotless components not in
        /// <paramref name="preservedInstanceGuids"/> and writes them back as pivots.
        /// MUST run on the Rhino UI thread — it measures freshly instantiated objects.
        /// </summary>
        /// <param name="document">Proposed document whose new components get laid out.</param>
        /// <param name="preservedInstanceGuids">
        /// Instance GUIDs of document components that update live objects in place.
        /// </param>
        /// <param name="offsetBelowExisting">
        /// When <c>true</c>, places only unanchored parts below existing canvas content.
        /// </param>
        /// <returns>The same document with final pivots baked into new components.</returns>
        internal static GhJsonDocument ApplyOnUiThread(
            GhJsonDocument document,
            IReadOnlySet<Guid>? preservedInstanceGuids,
            bool offsetBelowExisting)
        {
            var ghDoc = GhJsonGrasshopper.GetActiveDocument();
            if (document?.Components == null || document.Components.Count == 0 || ghDoc == null)
            {
                return document;
            }

            var newComponents = document.Components
                .Where(component => component.Pivot == null &&
                                     !(component.InstanceGuid.HasValue &&
                                      (preservedInstanceGuids?.Contains(component.InstanceGuid.Value) ?? false)))
                .ToList();
            if (newComponents.Count == 0)
            {
                return document;
            }

            try
            {
                AssignLayoutPivots(document, newComponents, ghDoc, offsetBelowExisting);
            }
            catch (Exception ex)
            {
                // Layout is best-effort: a failure must never block placement, so
                // components keep whatever pivots they already carry.
                Logger.Exception(typeof(CanvasNewComponentLayout).FullName, ex, "[CanvasNewComponentLayout] Layout computation failed; keeping document pivots.");
            }

            return document;
        }

        /// <summary>
        /// Fits the shared compact layout to the document's fixed component centers,
        /// then converts only missing new-component centers to pivots. Disconnected
        /// unanchored parts may be offset below existing canvas content.
        /// </summary>
        private static void AssignLayoutPivots(
            GhJsonDocument document,
            List<GhJsonComponent> newComponents,
            GH_Document ghDoc,
            bool offsetBelowExisting)
        {
            // Measure real object bounds off-canvas so refinements and the
            // center-to-pivot conversion use true geometry. GUIDs are regenerated so a
            // measure-only instance can never collide with a live object. The declared
            // wiring is mirrored and attributes laid out: components keep stock
            // bounds without a layout pass, and wired floating parameters render
            // taller than unwired ones.
            var measureDoc = new GhJsonDocument(document.Schema, document.Metadata, document.Components, document.Connections, null);
            var measured = GhJsonGrasshopper.Deserialize(measureDoc, new DeserializationOptions
            {
                RegenerateInstanceGuids = true,
                SkipInvalidComponents = true,
                WireConnections = true,
                PerformLayout = true,
            });

            var objectsByKey = new Dictionary<Guid, IGH_DocumentObject>(document.Components.Count);
            foreach (var component in document.Components)
            {
                var key = GhJson.GetLayoutKey(component);
                if (key != Guid.Empty &&
                    component.Id.HasValue &&
                    measured.IdToObjectMapping.TryGetValue(component.Id.Value, out var obj) &&
                    obj != null)
                {
                    objectsByKey[key] = obj;
                }
            }

            var fixedPositions = new Dictionary<Guid, PointF>();
            foreach (var component in document.Components.Where(c => !newComponents.Contains(c)))
            {
                var key = GhJson.GetLayoutKey(component);
                var proposed = objectsByKey.GetValueOrDefault(key);
                var existing = component.Pivot == null && component.InstanceGuid.HasValue
                    ? ghDoc.FindObject(component.InstanceGuid.Value, false)
                    : null;
                if (existing?.Attributes != null && proposed != null)
                {
                    fixedPositions[key] = PivotSemantics.PivotToCenter(proposed, existing.Attributes.Pivot);
                }
                else if (proposed != null || existing != null)
                {
                    var obj = proposed ?? existing!;
                    objectsByKey[key] = obj;
                    fixedPositions[key] = PivotSemantics.BoundsCenter(obj);
                }
            }

            var unanchoredTarget = offsetBelowExisting
                ? CanvasBoundsCalculator.UnanchoredTargetTopLeft(ghDoc.ActiveObjects(), AutoOffsetSpacing)
                : (PointF?)null;
            var centers = GhJsonGrasshopper.ComputeLayout(document, new CanvasLayoutOptions
            {
                Refinements = new LayoutRefinementOptions
                {
                    FixedPositions = fixedPositions,
                    UnanchoredTargetTopLeft = unanchoredTarget,
                },
                NodeSizeProvider = key =>
                    objectsByKey.TryGetValue(key, out var obj) &&
                    obj.Attributes?.Bounds is RectangleF bounds &&
                    bounds.Width > 0f && bounds.Height > 0f
                        ? bounds.Size
                        : (SizeF?)null,
                ObjectProvider = key => objectsByKey.TryGetValue(key, out var obj) ? obj : null,
            });

            // ComputeLayout returns bounds centers; sliders, panels and floating
            // parameters do not pivot around their center, so each object's own
            // pivot-to-bounds offset performs the conversion.
            var pivots = new Dictionary<GhJsonComponent, PointF>(newComponents.Count);
            foreach (var component in newComponents)
            {
                var key = GhJson.GetLayoutKey(component);
                if (key != Guid.Empty && centers.TryGetValue(key, out var center))
                {
                    pivots[component] = objectsByKey.TryGetValue(key, out var obj)
                        ? PivotSemantics.CenterToPivot(obj, center)
                        : center;
                }
                else if (component.Pivot != null)
                {
                    pivots[component] = new PointF(component.Pivot.X, component.Pivot.Y);
                }
            }

            // Components with no layout key and no pivot still get a deterministic
            // cascading position below the block rather than defaulting to (0,0).
            var fallbackStartY = unanchoredTarget?.Y ?? 0f;
            var fallbackX = pivots.Count > 0 ? pivots.Values.Min(p => p.X) : 0f;
            var fallbackCount = 0;
            foreach (var component in newComponents)
            {
                var pivot = pivots.TryGetValue(component, out var position)
                    ? position
                    : new PointF(fallbackX, fallbackStartY + (fallbackCount++ * FallbackCascadeSpacing));
                component.Pivot = new GhJsonPivot((int)Math.Round(pivot.X), (int)Math.Round(pivot.Y));
            }
        }
    }
}
