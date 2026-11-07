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
using GhJSON.Grasshopper.PutOperations;
using GhJSON.Grasshopper.Shared;
using Grasshopper.Kernel;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Bakes final canvas pivots into the new components of a proposed GhJSON document
    /// by running the shared GhJSON layout pipeline — the core dependency-graph pass
    /// plus the Grasshopper refinements (bounds-aware spacing, port alignment, wire
    /// corridors, collision resolution) that <see cref="GhJsonGrasshopper.ComputeLayout"/>
    /// exposes and gh_tidy_up uses. Writing the computed pivots back into the document
    /// before it is staged means the canvas review previews the exact final placement
    /// (never <c>(0,0)</c> or raw AI-supplied coordinates) and the later
    /// <see cref="GhJsonGrasshopper.Put(GhJsonDocument, PutOptions)"/> applies the
    /// positions verbatim.
    /// </summary>
    public static class CanvasNewComponentLayout
    {
        private const float AutoOffsetSpacing = 100f;
        private const float FallbackCascadeSpacing = 40f;

        /// <summary>
        /// Marshals <see cref="ApplyOnUiThread"/> onto the Rhino UI thread.
        /// </summary>
        /// <param name="document">Proposed document whose new components get laid out.</param>
        /// <param name="preservedInstanceGuids">
        /// Instance GUIDs of document components that update live objects in place;
        /// those keep their position and are excluded from the layout. Pass
        /// <c>null</c> or empty when every component places as a new object.
        /// </param>
        /// <param name="offsetBelowExisting">
        /// When <c>true</c>, the laid-out block shifts below existing canvas content
        /// (same convention as <see cref="PutOptions.AutoOffset"/>).
        /// </param>
        /// <param name="cancellationToken">Cancellation for the marshalled call.</param>
        /// <returns>The same document with final pivots baked into new components.</returns>
        public static Task<GhJsonDocument> ApplyAsync(
            GhJsonDocument document,
            IReadOnlySet<Guid>? preservedInstanceGuids,
            bool offsetBelowExisting,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<GhJsonDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            global::Rhino.RhinoApp.InvokeOnUiThread(() =>
            {
                try
                {
                    completion.TrySetResult(ApplyOnUiThread(document, preservedInstanceGuids, offsetBelowExisting));
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            });
            return completion.Task;
        }

        /// <summary>
        /// Computes tidy-up positions for every component not in
        /// <paramref name="preservedInstanceGuids"/> and writes them back as pivots.
        /// MUST run on the Rhino UI thread — it measures freshly instantiated objects.
        /// </summary>
        /// <param name="document">Proposed document whose new components get laid out.</param>
        /// <param name="preservedInstanceGuids">
        /// Instance GUIDs of document components that update live objects in place.
        /// </param>
        /// <param name="offsetBelowExisting">
        /// When <c>true</c>, shifts the laid-out block below existing canvas content.
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
                .Where(component => !(component.InstanceGuid.HasValue &&
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
        /// Runs the shared layout over the new components of <paramref name="document"/>,
        /// converts the returned bounds-center positions to real pivots, and shifts the
        /// whole block below existing canvas content when requested.
        /// </summary>
        private static void AssignLayoutPivots(
            GhJsonDocument document,
            List<GhJsonComponent> newComponents,
            GH_Document ghDoc,
            bool offsetBelowExisting)
        {
            // Measure real object bounds off-canvas so refinements and the
            // center-to-pivot conversion use true geometry. GUIDs are regenerated so a
            // measure-only instance can never collide with a live object.
            var measureDoc = new GhJsonDocument(document.Schema, document.Metadata, newComponents, null, null);
            var measured = GhJsonGrasshopper.Deserialize(measureDoc, new DeserializationOptions
            {
                RegenerateInstanceGuids = true,
                SkipInvalidComponents = true,
            });

            var objectsByKey = new Dictionary<Guid, IGH_DocumentObject>(newComponents.Count);
            foreach (var component in newComponents)
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

            // The layout graph contains only the new components and their internal
            // wiring; preserved (update-in-place) objects keep their live positions.
            var newIds = new HashSet<int>(newComponents
                .Where(component => component.Id.HasValue)
                .Select(component => component.Id!.Value));
            var internalConnections = document.Connections?
                .Where(conn => newIds.Contains(conn.From.Id) && newIds.Contains(conn.To.Id))
                .ToList();
            var layoutDoc = new GhJsonDocument(
                document.Schema,
                document.Metadata,
                newComponents,
                internalConnections,
                null);

            var centers = GhJsonGrasshopper.ComputeLayout(layoutDoc, new CanvasLayoutOptions
            {
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

            // Same convention as CanvasPlacer.OffsetPositionsBelowExistingContent: the
            // laid-out block shifts down so its content starts below existing objects.
            var startY = offsetBelowExisting
                ? CanvasBoundsCalculator.CalculateBottomStartY(ghDoc.ActiveObjects(), AutoOffsetSpacing)
                : 0f;

            // Components with no layout key and no pivot still get a deterministic
            // cascading position below the block rather than defaulting to (0,0).
            var fallbackX = pivots.Count > 0 ? pivots.Values.Min(p => p.X) : 0f;
            var fallbackCount = 0;
            foreach (var component in newComponents)
            {
                var pivot = pivots.TryGetValue(component, out var position)
                    ? new PointF(position.X, position.Y + startY)
                    : new PointF(fallbackX, startY + (fallbackCount++ * FallbackCascadeSpacing));
                component.Pivot = new GhJsonPivot((int)Math.Round(pivot.X), (int)Math.Round(pivot.Y));
            }
        }
    }
}
