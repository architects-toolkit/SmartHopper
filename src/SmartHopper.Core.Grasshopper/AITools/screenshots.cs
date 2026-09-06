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
                }
            }
        }";
        private const string CanvasOutputSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""imageBase64"": { ""type"": ""string"" },
                ""mimeType"": { ""type"": ""string"", ""const"": ""image/png"" },
                ""width"": { ""type"": ""integer"" },
                ""height"": { ""type"": ""integer"" }
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
                }
            }
        }";
        private const string ViewportOutputSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""imageBase64"": { ""type"": ""string"" },
                ""mimeType"": { ""type"": ""string"", ""const"": ""image/png"" },
                ""width"": { ""type"": ""integer"" },
                ""height"": { ""type"": ""integer"" },
                ""viewName"": { ""type"": ""string"" }
            },
            ""required"": [""imageBase64"", ""mimeType"", ""width"", ""height"", ""viewName""]
        }";
        private const string ViewportToolName = "viewport_screenshot";
        private readonly ICanvasCaptureService canvasCaptureService;
        private readonly IViewportCaptureService viewportCaptureService;

        /// <summary>
        /// Initializes a new instance of the <see cref="Screenshots"/> class with live Rhino capture services.
        /// </summary>
        public Screenshots()
            : this(new CanvasCaptureService(), new ViewportCaptureService())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Screenshots"/> class with injectable capture services.
        /// </summary>
        public Screenshots(ICanvasCaptureService canvasCaptureService, IViewportCaptureService viewportCaptureService)
        {
            this.canvasCaptureService = canvasCaptureService ?? throw new ArgumentNullException(nameof(canvasCaptureService));
            this.viewportCaptureService = viewportCaptureService ?? throw new ArgumentNullException(nameof(viewportCaptureService));
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
        }

        private static int ReadDimension(JObject args, string propertyName, int defaultValue)
        {
            int value = args[propertyName]?.ToObject<int>() ?? defaultValue;
            return ImageCaptureUtilities.ValidateDimension(value, propertyName);
        }

        private static AIBody BuildToolResultBody(
            AIInteractionToolCall toolInfo,
            ImageCaptureResult capture,
            bool includeViewName)
        {
            var result = new JObject
            {
                ["imageBase64"] = capture.ImageBase64,
                ["mimeType"] = "image/png",
                ["width"] = capture.Width,
                ["height"] = capture.Height,
            };

            if (includeViewName)
            {
                result["viewName"] = capture.ViewName ?? string.Empty;
            }

            return AIBodyBuilder.Create()
                .AddToolResult(result, toolInfo.Id, toolInfo.Name)
                .Build();
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
                var capture = await this.canvasCaptureService
                    .CaptureCanvasAsync(maxWidth, maxHeight)
                    .ConfigureAwait(false);

                output.CreateSuccess(BuildToolResultBody(toolInfo, capture, includeViewName: false), toolCall);
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
                var capture = await this.viewportCaptureService
                    .CaptureViewportAsync(viewName, width, height)
                    .ConfigureAwait(false);

                output.CreateSuccess(BuildToolResultBody(toolInfo, capture, includeViewName: true), toolCall);
            }
            catch (Exception ex)
            {
                output.CreateError($"Viewport screenshot failed: {ex.Message}");
            }

            return output;
        }
    }
}
