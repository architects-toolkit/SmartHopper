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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Interaction;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Read-only tool that points the user's attention at components or a region on the
    /// Grasshopper canvas: it pans and zooms the viewport to the target and draws a
    /// transient border highlight for a few seconds. In interactive chat it also renders
    /// a card with the tool's message and a replay button. It never mutates the document.
    /// </summary>
    public class canvas_point : IAIToolProvider
    {
        /// <summary>
        /// Name of the AI tool provided by this class.
        /// </summary>
        private const string ToolName = "canvas_point";

        private const int MaxMessageLength = 4000;
        private const int MaxGuids = 20;

        /// <summary>
        /// Returns AI tools for canvas pointing and highlighting.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: ToolName,
                description: "Points the user's attention at something on the Grasshopper canvas: pans and zooms the viewport to the target and draws a border highlight for a few seconds. Provide component instance GUIDs and/or a canvas region (x, y, width, height) as the target. The 'message' is shown in chat next to a button that replays the pan/zoom/highlight. Use it to refer the user to a specific component or area, e.g. 'this is the component you are struggling with'. It never changes the document.",
                category: "ViewControl",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""message"": {
                            ""type"": ""string"",
                            ""description"": ""User-facing text rendered in the chat card next to the replay button.""
                        },
                        ""guids"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""maxItems"": 20,
                            ""description"": ""Instance GUIDs of document objects to highlight.""
                        },
                        ""region"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""x"": { ""type"": ""number"" },
                                ""y"": { ""type"": ""number"" },
                                ""width"": { ""type"": ""number"" },
                                ""height"": { ""type"": ""number"" }
                            },
                            ""required"": [""x"", ""y"", ""width"", ""height""],
                            ""description"": ""Canvas region in world coordinates to highlight.""
                        }
                    },
                    ""required"": [ ""message"" ]
                }",
                execute: this.CanvasPointAsync,
                mutatesCanvas: false,
                tags: new[] { "canvas", "view", "viewport", "highlight", "pointer", "read-only" },
                outputSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""pointerId"": { ""type"": ""string"" },
                        ""displayed"": { ""type"": ""boolean"" },
                        ""framedGuids"": { ""type"": ""array"" },
                        ""missingGuids"": { ""type"": ""array"" }
                    }
                }",
                annotations: new AIToolAnnotations(
                    readOnlyHint: true,
                    destructiveHint: false,
                    idempotentHint: true,
                    openWorldHint: false,
                    title: "Point at Canvas"));
        }

        private async Task<AIReturn> CanvasPointAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();

                var request = ReadRequest(args);
                if (request == null)
                {
                    output.CreateError("Provide at least one target: 'guids' and/or 'region'.");
                    return output;
                }

                var showResult = await CanvasPointerService.ShowAsync(request, toolCall.CancellationToken).ConfigureAwait(false);
                if (!showResult.Success)
                {
                    output.CreateError(showResult.Error ?? "The pointer could not be displayed.");
                    return output;
                }

                CanvasPointerService.Register(request);

                var displayed = false;
                var presenter = toolCall.InvocationContext?.CanvasPointerPresenter;
                if (presenter != null)
                {
                    try
                    {
                        await presenter.ShowAsync(request, toolCall.CancellationToken).ConfigureAwait(false);
                        displayed = true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[canvas_point] Presenter failed: {ex.Message}");
                    }
                }

                var result = new JObject
                {
                    ["pointerId"] = request.Id,
                    ["displayed"] = displayed,
                    ["framedGuids"] = JArray.FromObject(showResult.FramedGuids.ConvertAll(g => g.ToString())),
                };
                if (showResult.MissingGuids.Count > 0)
                {
                    result["missingGuids"] = JArray.FromObject(showResult.MissingGuids.ConvertAll(g => g.ToString()));
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

        private static CanvasPointerRequest? ReadRequest(JObject args)
        {
            var message = args["message"]?.ToString() ?? string.Empty;
            if (message.Length > MaxMessageLength)
            {
                message = message.Substring(0, MaxMessageLength);
            }

            var guids = new List<Guid>();
            if (args["guids"] is JArray guidTokens)
            {
                foreach (var token in guidTokens)
                {
                    if (guids.Count >= MaxGuids)
                    {
                        break;
                    }

                    if (Guid.TryParse(token?.ToString(), out var guid))
                    {
                        guids.Add(guid);
                    }
                }
            }

            float? x = null, y = null, width = null, height = null;
            if (args["region"] is JObject region)
            {
                x = ReadFinite(region, "x");
                y = ReadFinite(region, "y");
                width = ReadFinite(region, "width");
                height = ReadFinite(region, "height");
            }

            var hasRegion = x.HasValue && y.HasValue && width.HasValue && height.HasValue &&
                width.Value > 0f && height.Value > 0f;
            if (guids.Count == 0 && !hasRegion)
            {
                return null;
            }

            return new CanvasPointerRequest
            {
                Id = $"ptr-{Guid.NewGuid():N}",
                Message = message,
                Guids = guids,
                X = hasRegion ? x : null,
                Y = hasRegion ? y : null,
                Width = hasRegion ? width : null,
                Height = hasRegion ? height : null,
                DurationSeconds = CanvasPointerService.DurationSeconds,
            };
        }

        private static float? ReadFinite(JObject obj, string name)
        {
            if (obj[name] == null)
            {
                return null;
            }

            var value = obj[name]!.ToObject<float>();
            return float.IsNaN(value) || float.IsInfinity(value) ? null : value;
        }
    }
}
