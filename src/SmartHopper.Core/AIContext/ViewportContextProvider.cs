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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json;
using Rhino;
using SmartHopper.Infrastructure.AIContext;

namespace SmartHopper.Core.AIContext
{
    /// <summary>
    /// Context provider that supplies information about the currently visible
    /// Grasshopper canvas viewport.
    /// </summary>
    public class ViewportContextProvider : IAIContextProvider
    {
        /// <summary>
        /// Maximum number of visible objects to include in the detailed list.
        /// </summary>
        private const int MaxVisibleObjects = 50;

        /// <summary>
        /// Gets the provider identifier.
        /// </summary>
        public string ProviderId => "viewport";

        /// <summary>
        /// Gets the current viewport context for AI queries.
        /// </summary>
        /// <returns>A dictionary containing the current viewport context values.</returns>
        [SuppressMessage("Design", "CA1031", Justification = "Context providers must be resilient and never crash the AI request pipeline.")]
        public Dictionary<string, string> GetContext()
        {
            try
            {
                string boundsJson = "{}";
                string centerJson = "{}";
                float zoom = 1f;
                int visibleCount = 0;
                string visibleObjectsJson = "[]";

                using (var uiThreadComplete = new ManualResetEventSlim(false))
                {
                    RhinoApp.InvokeOnUiThread(
                        (Action)(() =>
                        {
                            try
                            {
                                var canvas = Instances.ActiveCanvas;
                                var doc = canvas?.Document;
                                var viewport = canvas?.Viewport;

                                if (viewport != null)
                                {
                                    var bounds = viewport.VisibleRegion;
                                    boundsJson = JsonConvert.SerializeObject(new
                                    {
                                        x = bounds.X,
                                        y = bounds.Y,
                                        width = bounds.Width,
                                        height = bounds.Height,
                                    });

                                    centerJson = JsonConvert.SerializeObject(new
                                    {
                                        x = viewport.MidPoint.X,
                                        y = viewport.MidPoint.Y,
                                    });

                                    zoom = viewport.Zoom;
                                }

                                if (doc != null && viewport != null)
                                {
                                    var visibleRegion = viewport.VisibleRegion;
                                    var allObjects = doc.Objects?.OfType<IGH_DocumentObject>()?.ToList();
                                    var visibleObjects = new List<object>();

                                    if (allObjects != null)
                                    {
                                        foreach (var obj in allObjects)
                                        {
                                            if (obj?.Attributes?.Bounds != null
                                                && obj.Attributes.Bounds.IntersectsWith(visibleRegion))
                                            {
                                                visibleObjects.Add(new
                                                {
                                                    instanceGuid = obj.InstanceGuid,
                                                    name = obj.Name,
                                                    nickName = string.IsNullOrWhiteSpace(obj.NickName)
                                                        ? obj.Name
                                                        : obj.NickName,
                                                    type = obj.GetType().Name,
                                                    pivot = new
                                                    {
                                                        x = obj.Attributes.Pivot.X,
                                                        y = obj.Attributes.Pivot.Y,
                                                    },
                                                });

                                                if (visibleObjects.Count >= MaxVisibleObjects)
                                                {
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                    visibleCount = visibleObjects.Count;
                                    visibleObjectsJson = JsonConvert.SerializeObject(visibleObjects);
                                }
                            }
                            finally
                            {
                                uiThreadComplete.Set();
                            }
                        }));

                    uiThreadComplete.Wait(TimeSpan.FromSeconds(5));
                }

                return new Dictionary<string, string>
                {
                    { "viewport-bounds", boundsJson },
                    { "viewport-center", centerJson },
                    { "viewport-zoom", zoom.ToString(CultureInfo.InvariantCulture) },
                    { "viewport-visible-count", visibleCount.ToString(CultureInfo.InvariantCulture) },
                    { "viewport-visible-objects", visibleObjectsJson },
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ViewportContextProvider] Error: {ex.Message}");
                return new Dictionary<string, string>
                {
                    { "viewport-bounds", "{}" },
                    { "viewport-center", "{}" },
                    { "viewport-zoom", "1" },
                    { "viewport-visible-count", "0" },
                    { "viewport-visible-objects", "[]" },
                };
            }
        }
    }
}
