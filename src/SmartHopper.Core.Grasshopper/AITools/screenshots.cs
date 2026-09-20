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
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Internal;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides read-only screenshot tools for the Grasshopper canvas and Rhino viewport.
    /// </summary>
    public sealed class Screenshots : IAIToolProvider
    {
        private const string CanvasParametersSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""maxWidth"": {
                    ""type"": ""integer"",
                    ""minimum"": 1,
                    ""maximum"": 4096,
                    ""default"": 1920,
                    ""description"": ""Maximum output width in pixels.""
                },
                ""maxHeight"": {
                    ""type"": ""integer"",
                    ""minimum"": 1,
                    ""maximum"": 4096,
                    ""default"": 1080,
                    ""description"": ""Maximum output height in pixels.""
                },
                ""savePath"": {
                    ""type"": ""string"",
                    ""description"": ""Optional absolute file path that also receives the PNG. Parent directories are created and existing files are overwritten.""
                }
            }
        }";
        private const string CanvasOutputSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""imageBase64"": { ""type"": ""string"" },
                ""mimeType"": { ""type"": ""string"", ""const"": ""image/png"" },
                ""imageAudience"": { ""type"": ""string"", ""const"": ""model"", ""description"": ""Declares the image is intended for model vision; the session extracts it into a model-visible image part."" },
                ""width"": { ""type"": ""integer"" },
                ""height"": { ""type"": ""integer"" },
                ""savedTo"": { ""type"": ""string"", ""description"": ""Absolute path the PNG was written to, when savePath was provided."" }
            },
            ""required"": [""imageBase64"", ""mimeType"", ""width"", ""height""]
        }";
        private const string CanvasToolName = "canvas_screenshot";
        private const string ViewportParametersSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""viewName"": {
                    ""type"": ""string"",
                    ""description"": ""Optional Rhino viewport name. Omit to capture the active view.""
                },
                ""width"": {
                    ""type"": ""integer"",
                    ""minimum"": 1,
                    ""maximum"": 4096,
                    ""default"": 1024,
                    ""description"": ""Maximum output width in pixels.""
                },
                ""height"": {
                    ""type"": ""integer"",
                    ""minimum"": 1,
                    ""maximum"": 4096,
                    ""default"": 1024,
                    ""description"": ""Maximum output height in pixels.""
                },
                ""savePath"": {
                    ""type"": ""string"",
                    ""description"": ""Optional absolute file path that also receives the PNG. Parent directories are created and existing files are overwritten.""
                }
            }
        }";
        private const string ViewportOutputSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""imageBase64"": { ""type"": ""string"" },
                ""mimeType"": { ""type"": ""string"", ""const"": ""image/png"" },
                ""imageAudience"": { ""type"": ""string"", ""const"": ""model"", ""description"": ""Declares the image is intended for model vision; the session extracts it into a model-visible image part."" },
                ""width"": { ""type"": ""integer"" },
                ""height"": { ""type"": ""integer"" },
                ""viewName"": { ""type"": ""string"" },
                ""savedTo"": { ""type"": ""string"", ""description"": ""Absolute path the PNG was written to, when savePath was provided."" }
            },
            ""required"": [""imageBase64"", ""mimeType"", ""width"", ""height"", ""viewName""]
        }";
        private const string ViewportToolName = "viewport_screenshot";
        private const string HiResParametersSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""scope"": {
                    ""type"": ""string"",
                    ""enum"": [""document"", ""selection"", ""guids"", ""bounds""],
                    ""default"": ""document"",
                    ""description"": ""Region to export: the whole document, the current selection, a set of object GUIDs, or an explicit bounds rectangle.""
                },
                ""guids"": {
                    ""type"": ""array"",
                    ""items"": { ""type"": ""string"" },
                    ""description"": ""Instance GUIDs of objects to frame. Required for scope=guids.""
                },
                ""bounds"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""x"": { ""type"": ""number"" },
                        ""y"": { ""type"": ""number"" },
                        ""width"": { ""type"": ""number"" },
                        ""height"": { ""type"": ""number"" }
                    },
                    ""description"": ""Canvas-space rectangle to export. Required for scope=bounds.""
                },
                ""padding"": {
                    ""type"": ""number"",
                    ""default"": 20,
                    ""description"": ""Padding in canvas units added around the resolved region.""
                },
                ""scale"": {
                    ""type"": ""number"",
                    ""exclusiveMinimum"": 0,
                    ""maximum"": 32,
                    ""default"": 1.0,
                    ""description"": ""Render zoom factor. 1.0 matches on-screen size; 2.0 doubles resolution for print.""
                },
                ""background"": {
                    ""type"": ""string"",
                    ""default"": ""transparent"",
                    ""description"": ""Background colour: 'transparent', 'white', 'canvas', or a '#RRGGBB'/'#AARRGGBB' hex colour.""
                },
                ""maxDimension"": {
                    ""type"": ""integer"",
                    ""minimum"": 1,
                    ""maximum"": 30000,
                    ""default"": 16384,
                    ""description"": ""Maximum allowed output width or height in pixels.""
                },
                ""savePath"": {
                    ""type"": ""string"",
                    ""description"": ""Optional absolute file path that also receives the PNG. Parent directories are created and existing files are overwritten.""
                }
            }
        }";
        private const string HiResOutputSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""imageBase64"": { ""type"": ""string"" },
                ""mimeType"": { ""type"": ""string"", ""const"": ""image/png"" },
                ""imageAudience"": { ""type"": ""string"", ""const"": ""display"", ""description"": ""Declares the image is display-only; it is rendered in WebChat but never sent to the model."" },
                ""width"": { ""type"": ""integer"" },
                ""height"": { ""type"": ""integer"" },
                ""savedTo"": { ""type"": ""string"", ""description"": ""Absolute path the PNG was written to, when savePath was provided."" }
            },
            ""required"": [""imageBase64"", ""mimeType"", ""width"", ""height""]
        }";
        private const string HiResToolName = "canvas_hi-res_screenshot";
        private readonly ICanvasCaptureService canvasCaptureService;
        private readonly IViewportCaptureService viewportCaptureService;
        private readonly ICanvasHiResCaptureService hiResCaptureService;

        /// <summary>
        /// Initializes a new instance of the <see cref="Screenshots"/> class with live Rhino capture services.
        /// </summary>
        public Screenshots()
            : this(new CanvasCaptureService(), new ViewportCaptureService(), new CanvasHiResCaptureService())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Screenshots"/> class with injectable capture services.
        /// </summary>
        public Screenshots(ICanvasCaptureService canvasCaptureService, IViewportCaptureService viewportCaptureService, ICanvasHiResCaptureService? hiResCaptureService = null)
        {
            this.canvasCaptureService = canvasCaptureService ?? throw new ArgumentNullException(nameof(canvasCaptureService));
            this.viewportCaptureService = viewportCaptureService ?? throw new ArgumentNullException(nameof(viewportCaptureService));
            this.hiResCaptureService = hiResCaptureService ?? new CanvasHiResCaptureService();
        }

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: CanvasToolName,
                description: "Captures the currently visible Grasshopper canvas viewport as a base64-encoded PNG. The image is resized to fit within maxWidth and maxHeight while preserving its aspect ratio.",
                category: "Vision",
                parametersSchema: CanvasParametersSchema,
                execute: this.CaptureCanvasAsync,
                mutatesCanvas: false,
                tags: new[] { "canvas", "vision", "image", "read-only" },
                outputSchema: CanvasOutputSchema,
                annotations: new AIToolAnnotations(
                    readOnlyHint: true,
                    destructiveHint: false,
                    idempotentHint: false,
                    openWorldHint: false,
                    title: "Grasshopper Canvas Screenshot"));

            yield return new AITool(
                name: ViewportToolName,
                description: "Captures the active or named Rhino 3D viewport as a base64-encoded PNG. The image is resized to fit within width and height while preserving its aspect ratio.",
                category: "Vision",
                parametersSchema: ViewportParametersSchema,
                execute: this.CaptureViewportAsync,
                mutatesCanvas: false,
                tags: new[] { "rhino", "viewport", "vision", "image", "read-only" },
                outputSchema: ViewportOutputSchema,
                annotations: new AIToolAnnotations(
                    readOnlyHint: true,
                    destructiveHint: false,
                    idempotentHint: false,
                    openWorldHint: false,
                    title: "Rhino Viewport Screenshot"));

            yield return new AITool(
                name: HiResToolName,
                description: "Renders a region of the Grasshopper canvas to a high-resolution PNG for publishing or printing, independent of the visible viewport. Use 'scope' to capture the whole document, the current selection, specific components by GUID, or an explicit bounds rectangle; 'scale' controls resolution (1.0 matches on-screen size); 'background' supports transparency. This tool is meant for presentable output, not for AI agent vision of the canvas: it can produce very large images that would consume too many unnecessary tokens. Use canvas_screenshot for agent vision instead.",
                category: "Vision",
                parametersSchema: HiResParametersSchema,
                execute: this.CaptureHiResCanvasAsync,
                mutatesCanvas: false,
                tags: new[] { "canvas", "vision", "image", "export", "hi-res", "read-only" },
                outputSchema: HiResOutputSchema,
                annotations: new AIToolAnnotations(
                    readOnlyHint: true,
                    destructiveHint: false,
                    idempotentHint: false,
                    openWorldHint: false,
                    title: "Grasshopper Canvas Hi-Res Screenshot"));
        }

        private static int ReadDimension(JObject args, string propertyName, int defaultValue)
        {
            int value = args[propertyName]?.ToObject<int>() ?? defaultValue;
            return ImageCaptureUtilities.ValidateDimension(value, propertyName);
        }

        private static AIBody BuildToolResultBody(
            AIInteractionToolCall toolInfo,
            ImageCaptureResult capture,
            bool includeViewName,
            string? savePath,
            string imageAudience)
        {
            var result = new JObject
            {
                ["imageBase64"] = capture.ImageBase64,
                ["mimeType"] = "image/png",
                ["imageAudience"] = imageAudience,
                ["width"] = capture.Width,
                ["height"] = capture.Height,
            };

            if (includeViewName)
            {
                result["viewName"] = capture.ViewName ?? string.Empty;
            }

            var savedTo = SavePng(capture, savePath);
            if (savedTo != null)
            {
                result["savedTo"] = savedTo;
            }

            return AIBodyBuilder.Create()
                .AddToolResult(result, toolInfo.Id, toolInfo.Name)
                .Build();
        }

        /// <summary>
        /// Writes the captured PNG to <paramref name="savePath"/> when provided, creating
        /// parent directories and overwriting existing files. Returns the normalized
        /// absolute path that was written, or <see langword="null"/> when no savePath
        /// was given.
        /// </summary>
        private static string? SavePng(ImageCaptureResult capture, string? savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                return null;
            }

            var fullPath = Path.GetFullPath(savePath.Trim());
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(fullPath, Convert.FromBase64String(capture.ImageBase64));
            return fullPath;
        }

        private async Task<AIReturn> CaptureCanvasAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                int maxWidth = ReadDimension(args, "maxWidth", CanvasCaptureService.DefaultMaxWidth);
                int maxHeight = ReadDimension(args, "maxHeight", CanvasCaptureService.DefaultMaxHeight);
                var savePath = args["savePath"]?.ToString();
                var capture = await this.canvasCaptureService
                    .CaptureCanvasAsync(maxWidth, maxHeight)
                    .ConfigureAwait(false);

                output.CreateSuccess(BuildToolResultBody(toolInfo, capture, includeViewName: false, savePath, imageAudience: "model"), toolCall);
            }
            catch (Exception ex)
            {
                output.CreateError($"Canvas screenshot failed: {ex.Message}");
            }

            return output;
        }

        private async Task<AIReturn> CaptureViewportAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                string? viewName = args["viewName"]?.ToString();
                int width = ReadDimension(args, "width", ViewportCaptureService.DefaultWidth);
                int height = ReadDimension(args, "height", ViewportCaptureService.DefaultHeight);
                var savePath = args["savePath"]?.ToString();
                var capture = await this.viewportCaptureService
                    .CaptureViewportAsync(viewName, width, height)
                    .ConfigureAwait(false);

                output.CreateSuccess(BuildToolResultBody(toolInfo, capture, includeViewName: true, savePath, imageAudience: "model"), toolCall);
            }
            catch (Exception ex)
            {
                output.CreateError($"Viewport screenshot failed: {ex.Message}");
            }

            return output;
        }

        private async Task<AIReturn> CaptureHiResCanvasAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                var toolInfo = toolCall.GetToolCall();
                var args = toolInfo.GetArgumentsOrEmpty();
                var request = BuildHiResRequest(args);
                var savePath = args["savePath"]?.ToString();
                var capture = await this.hiResCaptureService
                    .CaptureHiResAsync(request)
                    .ConfigureAwait(false);

                output.CreateSuccess(BuildToolResultBody(toolInfo, capture, includeViewName: false, savePath, imageAudience: "display"), toolCall);
            }
            catch (Exception ex)
            {
                output.CreateError($"Hi-res canvas screenshot failed: {ex.Message}");
            }

            return output;
        }

        private static CanvasHiResCaptureRequest BuildHiResRequest(JObject args)
        {
            var request = new CanvasHiResCaptureRequest();
            var scope = args["scope"]?.ToString()?.Trim().ToLowerInvariant() ?? "document";
            switch (scope)
            {
                case "document":
                    request.Scope = CanvasHiResScope.Document;
                    break;
                case "selection":
                    request.Scope = CanvasHiResScope.Selection;
                    break;
                case "guids":
                {
                    request.Scope = CanvasHiResScope.Guids;
                    var guids = new List<Guid>();
                    foreach (var token in args["guids"] as JArray ?? new JArray())
                    {
                        if (!Guid.TryParse(token?.ToString(), out var guid))
                        {
                            throw new ArgumentException($"Invalid guid '{token}' in 'guids'.");
                        }

                        guids.Add(guid);
                    }

                    if (guids.Count == 0)
                    {
                        throw new ArgumentException("'guids' is required and must be non-empty for scope=guids.");
                    }

                    request.Guids = guids;
                    break;
                }

                case "bounds":
                {
                    request.Scope = CanvasHiResScope.Bounds;
                    var bounds = args["bounds"] as JObject
                        ?? throw new ArgumentException("'bounds' is required for scope=bounds.");
                    float x = bounds["x"]?.ToObject<float>() ?? 0f;
                    float y = bounds["y"]?.ToObject<float>() ?? 0f;
                    float w = bounds["width"]?.ToObject<float>() ?? 0f;
                    float h = bounds["height"]?.ToObject<float>() ?? 0f;
                    if (w <= 0f || h <= 0f)
                    {
                        throw new ArgumentException("'bounds.width' and 'bounds.height' must be positive.");
                    }

                    request.Bounds = new RectangleF(x, y, w, h);
                    break;
                }

                default:
                    throw new ArgumentException($"Unknown scope '{scope}'. Use one of: document, selection, guids, bounds.");
            }

            request.Padding = args["padding"]?.ToObject<float>() ?? 20f;
            request.Scale = args["scale"]?.ToObject<float>() ?? 1f;
            request.Background = ParseBackground(args["background"]?.ToString());
            int maxDimension = args["maxDimension"]?.ToObject<int>() ?? CanvasHiResCaptureService.DefaultMaxDimension;
            if (maxDimension < 1 || maxDimension > CanvasHiResCaptureService.AbsoluteMaxDimension)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDimension),
                    maxDimension,
                    $"maxDimension must be between 1 and {CanvasHiResCaptureService.AbsoluteMaxDimension} pixels.");
            }

            request.MaxDimension = maxDimension;
            return request;
        }

        private static Color ParseBackground(string? value)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "transparent":
                    return Color.Transparent;
                case "white":
                    return Color.White;
                case "canvas":
                    return Color.Empty; // sentinel: resolved to the live canvas BackColor inside the service
                default:
                    if (value.StartsWith('#'))
                    {
                        try
                        {
                            return ColorTranslator.FromHtml(value);
                        }
                        catch (Exception ex)
                        {
                            throw new ArgumentException($"Invalid background colour '{value}': {ex.Message}");
                        }
                    }

                    throw new ArgumentException($"Invalid background '{value}'. Use 'transparent', 'white', 'canvas', or a '#RRGGBB'/'#AARRGGBB' hex colour.");
            }
        }
    }
}
