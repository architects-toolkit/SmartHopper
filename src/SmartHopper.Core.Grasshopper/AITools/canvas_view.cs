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
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Read-only tool that repositions the Grasshopper canvas viewport (pan/zoom).
    /// It never mutates the document, so it requires no consent review.
    /// </summary>
    public class canvas_view : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private const string ToolName = "canvas_view";

        private const float MinZoom = 0.01f;
        private const float MaxZoom = 32f;

        /// <summary>
        /// Returns AI tools for canvas viewport control.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: ToolName,
                description: "Adjusts the Grasshopper canvas viewport without changing the document. Actions: 'zoomExtents' frames every object; 'frameGuids' frames the given component GUIDs; 'setZoom' sets the zoom factor (1 = 100%); 'setCenter' centres the view on canvas coordinates x/y.",
                category: "Canvas.ViewControl",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""action"": {
                            ""type"": ""string"",
                            ""enum"": [""zoomExtents"", ""frameGuids"", ""setZoom"", ""setCenter""],
                            ""description"": ""Viewport action to perform.""
                        },
                        ""guids"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Instance GUIDs to frame. Required for frameGuids.""
                        },
                        ""zoom"": {
                            ""type"": ""number"",
                            ""minimum"": 0.01,
                            ""description"": ""Zoom factor for setZoom (1 = 100%).""
                        },
                        ""x"": {
                            ""type"": ""number"",
                            ""description"": ""Canvas X coordinate for setCenter.""
                        },
                        ""y"": {
                            ""type"": ""number"",
                            ""description"": ""Canvas Y coordinate for setCenter.""
                        }
                    },
                    ""required"": [ ""action"" ]
                }",
                execute: this.CanvasViewAsync,
                mutatesCanvas: false,
                tags: new[] { "canvas", "view", "viewport", "zoom", "read-only" },
                outputSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""action"": { ""type"": ""string"" },
                        ""zoom"": { ""type"": ""number"" },
                        ""center"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""x"": { ""type"": ""number"" },
                                ""y"": { ""type"": ""number"" }
                            }
                        },
                        ""visibleRegion"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""x"": { ""type"": ""number"" },
                                ""y"": { ""type"": ""number"" },
                                ""width"": { ""type"": ""number"" },
                                ""height"": { ""type"": ""number"" }
                            }
                        },
                        ""framedGuids"": { ""type"": ""array"" },
                        ""missingGuids"": { ""type"": ""array"" }
                    }
                }",
                annotations: new AIToolAnnotations(
                    readOnlyHint: true,
                    destructiveHint: false,
                    idempotentHint: true,
                    openWorldHint: false,
                    title: "Grasshopper Canvas View"),
                activityLabel: "reading canvas view",
                resultLabel: "read canvas view");
        }

        private async Task<AIReturn> CanvasViewAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var action = args["action"]?.ToString();
                if (string.IsNullOrWhiteSpace(action))
                {
                    output.CreateError("Missing required 'action' parameter.");
                    return output;
                }

                // Viewport access touches WinForms objects — marshal to the UI thread.
                var result = CanvasAccess.RunOnUiThread(() =>
                {
                    var canvas = Instances.ActiveCanvas;
                    var document = canvas?.Document;
                    if (canvas == null || document == null)
                    {
                        return new JObject { ["error"] = "No active Grasshopper canvas is available." };
                    }

                    var actionResult = ApplyViewAction(canvas, document, action, args);
                    if (!actionResult.ContainsKey("error"))
                    {
                        canvas.Refresh();
                    }

                    return actionResult;
                });

                if (result.TryGetValue("error", out var errorToken))
                {
                    output.CreateError(errorToken?.ToString() ?? "Unknown canvas_view error.");
                    return output;
                }

                var builder = AIBodyBuilder.Create();
                builder.AddToolResult(result, toolInfo.Id, toolInfo.Name);
                output.CreateSuccess(builder.Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateError($"Error: {ex.Message}");
                return output;
            }
        }

        private static JObject ApplyViewAction(GH_Canvas canvas, GH_Document document, string action, JObject args)
        {
            var viewport = canvas.Viewport;
            if (viewport.Width <= 0 || viewport.Height <= 0)
            {
                return Error("The canvas viewport has no usable size.");
            }

            List<IGH_Attributes>? frameTargets = null;
            List<Guid> missing = new ();
            List<string> invalid = new ();
            float? desiredZoom = null;
            PointF? desiredCenter = null;

            switch (action)
            {
                case "zoomExtents":
                {
                    var attributes = document.Objects
                        .Where(obj => obj?.Attributes != null)
                        .Select(obj => obj.Attributes)
                        .ToList();
                    if (attributes.Count == 0)
                    {
                        return Error("The document contains no objects to frame.");
                    }

                    frameTargets = attributes;
                    break;
                }

                case "frameGuids":
                {
                    var (resolved, unresolved, malformed) = ResolveAttributes(document, args);
                    missing = unresolved;
                    invalid = malformed;
                    if (resolved.Count == 0)
                    {
                        return Error("None of the requested GUIDs could be found on the canvas.");
                    }

                    frameTargets = resolved;
                    break;
                }

                case "setZoom":
                {
                    if (args["zoom"] == null)
                    {
                        return Error("Missing required 'zoom' parameter for setZoom.");
                    }

                    var zoom = args["zoom"]!.ToObject<float>();
                    if (float.IsNaN(zoom) || float.IsInfinity(zoom) || zoom <= 0f)
                    {
                        return Error("'zoom' must be a positive finite number.");
                    }

                    // Preserve the current world-space center across the zoom change.
                    desiredCenter = viewport.MidPoint;
                    desiredZoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
                    break;
                }

                case "setCenter":
                {
                    if (args["x"] == null || args["y"] == null)
                    {
                        return Error("Missing required 'x' and 'y' parameters for setCenter.");
                    }

                    var x = args["x"]!.ToObject<float>();
                    var y = args["y"]!.ToObject<float>();
                    if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
                    {
                        return Error("'x' and 'y' must be finite numbers.");
                    }

                    desiredCenter = new PointF(x, y);
                    break;
                }

                default:
                    return Error($"Unknown action '{action}'. Expected one of: zoomExtents, frameGuids, setZoom, setCenter.");
            }

            if (frameTargets != null)
            {
                var bounds = frameTargets
                    .Select(a => a.Bounds)
                    .Aggregate(RectangleF.Union);
                desiredZoom = ViewportProjection.FitZoom(
                    viewport.Width,
                    viewport.Height,
                    bounds.Width,
                    bounds.Height,
                    ViewportProjection.FramePaddingPixels,
                    MinZoom,
                    MaxZoom);
                desiredCenter = new PointF(
                    bounds.X + bounds.Width / 2f,
                    bounds.Y + bounds.Height / 2f);
            }

            // Apply the coherent transform in one UI turn: zoom first (the target
            // recompute below preserves the requested world-space center at the new
            // zoom), then the target, then recompute the projection so the payload
            // reflects the applied state — not the previously cached region.
            if (desiredZoom.HasValue)
            {
                viewport.Zoom = desiredZoom.Value;
            }

            if (desiredCenter.HasValue)
            {
                viewport.Target = ViewportProjection.TargetForCenter(
                    viewport.Width,
                    viewport.Height,
                    desiredCenter.Value.X,
                    desiredCenter.Value.Y,
                    viewport.Zoom);
            }

            viewport.ComputeProjection();

            var payload = ViewportPayload(canvas, action);
            if (frameTargets != null && action == "frameGuids")
            {
                payload["framedGuids"] = JArray.FromObject(frameTargets
                    .Select(a => a.InstanceGuid)
                    .Distinct()
                    .Select(g => g.ToString()));
            }

            if (missing.Count > 0)
            {
                payload["missingGuids"] = JArray.FromObject(missing.Select(g => g.ToString()));
            }

            if (invalid.Count > 0)
            {
                payload["invalidGuids"] = JArray.FromObject(invalid);
            }

            return payload;
        }

        private static (List<IGH_Attributes> Resolved, List<Guid> Missing, List<string> Invalid) ResolveAttributes(GH_Document document, JObject args)
        {
            var resolved = new List<IGH_Attributes>();
            var missing = new List<Guid>();
            var invalid = new List<string>();
            var guids = args["guids"] as JArray;
            if (guids == null || guids.Count == 0)
            {
                return (resolved, missing, invalid);
            }

            var seen = new HashSet<Guid>();
            foreach (var token in guids)
            {
                if (!Guid.TryParse(token?.ToString(), out var guid))
                {
                    // Malformed tokens are reported, never fabricated into a target.
                    var raw = token?.ToString();
                    if (!string.IsNullOrEmpty(raw))
                    {
                        invalid.Add(raw!);
                    }

                    continue;
                }

                if (!seen.Add(guid))
                {
                    continue;
                }

                var obj = document.FindObject(guid, true);
                if (obj?.Attributes != null)
                {
                    resolved.Add(obj.Attributes);
                }
                else
                {
                    missing.Add(guid);
                }
            }

            return (resolved, missing, invalid);
        }

        private static JObject ViewportPayload(GH_Canvas canvas, string action)
        {
            var midpoint = canvas.Viewport.MidPoint;
            var region = canvas.Viewport.VisibleRegion;
            return new JObject
            {
                ["action"] = action,
                ["zoom"] = canvas.Viewport.Zoom,
                ["center"] = new JObject
                {
                    ["x"] = midpoint.X,
                    ["y"] = midpoint.Y,
                },
                ["visibleRegion"] = new JObject
                {
                    ["x"] = region.X,
                    ["y"] = region.Y,
                    ["width"] = region.Width,
                    ["height"] = region.Height,
                },
            };
        }

        private static JObject Error(string message)
        {
            return new JObject { ["error"] = message };
        }
    }
}
