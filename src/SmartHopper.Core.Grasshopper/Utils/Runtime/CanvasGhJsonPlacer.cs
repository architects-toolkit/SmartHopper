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
using System.Threading;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Core.SchemaModels;
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.PutOperations;
using SmartHopper.Infrastructure.Interaction;

namespace SmartHopper.Core.Grasshopper.Utils.Runtime
{
    /// <summary>
    /// Executes WebChat place-on-canvas actions: puts a validated GhJSON document onto
    /// the user's ACTIVE Grasshopper document on the Rhino UI thread. Placement goes
    /// through <see cref="GhJsonGrasshopper.Put(GhJsonDocument, PutOptions)"/>, whose
    /// canvas placer records undo events internally, so Ctrl+Z reverts the placed objects.
    /// </summary>
    public static class CanvasGhJsonPlacer
    {
        private const int MaxGhJsonLength = 200_000;

        /// <summary>
        /// Registers this placer as the <see cref="GhJsonCanvasBridge"/> handler.
        /// Called once at plugin load (SmartHopperAssemblyPriority).
        /// </summary>
        public static void EnsureInitialized()
        {
            GhJsonCanvasBridge.PlaceHandler = PlaceOnUiThreadAsync;
        }

        /// <summary>Bridge path for the WebChat card's place action — chat-shaped outcome.</summary>
        private static Task<GhJsonPlaceOutcome> PlaceOnUiThreadAsync(string ghjson, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<GhJsonPlaceOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            global::Rhino.RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    tcs.TrySetResult(Place(ghjson));
                }
                catch (Exception ex)
                {
                    tcs.TrySetResult(new GhJsonPlaceOutcome { Success = false, Error = ex.Message });
                }
            }));
            return tcs.Task;
        }

        /// <summary>
        /// Validates and places the document. MUST run on the Rhino UI thread —
        /// callers marshal via <see cref="global::Rhino.RhinoApp.InvokeOnUiThread"/>.
        /// </summary>
        private static GhJsonPlaceOutcome Place(string ghjson)
        {
            if (string.IsNullOrWhiteSpace(ghjson))
            {
                return new GhJsonPlaceOutcome { Success = false, Error = "empty GhJSON payload" };
            }

            if (ghjson.Length > MaxGhJsonLength)
            {
                return new GhJsonPlaceOutcome { Success = false, Error = $"GhJSON exceeds the {MaxGhJsonLength} character limit" };
            }

            // Placement is a trusted-surface action (the user already approved showing
            // the artifact), but the payload still gets re-validated — replayed cards
            // can carry stale or crafted documents.
            var validation = GhJson.Validate(ghjson);
            if (!validation.IsValid)
            {
                var first = validation.Errors.Count > 0 ? validation.Errors[0].ToString() : "schema validation failed";
                return new GhJsonPlaceOutcome { Success = false, Error = $"invalid GhJSON: {first}" };
            }

            GhJsonDocument document;
            try
            {
                document = GhJson.FromJson(ghjson);
            }
            catch (Exception ex)
            {
                return new GhJsonPlaceOutcome { Success = false, Error = $"invalid GhJSON: {ex.Message}" };
            }

            var ghDoc = GhJsonGrasshopper.GetActiveDocument();
            if (ghDoc == null)
            {
                return new GhJsonPlaceOutcome { Success = false, Error = "no active Grasshopper document" };
            }

            var options = new PutOptions
            {
                // Card payloads always place NEW objects: regenerating guids keeps
                // a repeated click from colliding with earlier placements and prevents a
                // crafted payload from updating objects it does not own.
                RegenerateInstanceGuids = true,
                CreateConnections = true,
                CreateGroups = true,
                SelectPlacedObjects = true,
                SkipInvalidComponents = true,
                AutoOffset = false,
            };

            var result = GhJsonGrasshopper.Put(document, options);
            if (!result.Success)
            {
                return new GhJsonPlaceOutcome { Success = false, Error = result.ErrorMessage ?? "placement failed" };
            }

            return new GhJsonPlaceOutcome { Success = true, PlacedCount = result.PlacedObjects.Count };
        }
    }
}
