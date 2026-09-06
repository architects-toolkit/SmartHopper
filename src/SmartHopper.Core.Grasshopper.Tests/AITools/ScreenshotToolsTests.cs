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

namespace SmartHopper.Core.Grasshopper.Tests.AITools
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Core.Grasshopper.AITools;
    using SmartHopper.Core.Grasshopper.Utils.Internal;
    using SmartHopper.Core.Types;
    using SmartHopper.Infrastructure.AICall.Tools;
    using SmartHopper.Infrastructure.AITools;
    using SmartHopper.Infrastructure.Mcp;
    using SmartHopper.ProviderSdk.AICall.Core.Returns;
    using Xunit;

    /// <summary>
    /// Tests screenshot encoding and projection through the existing MCP tool adapter without Rhino runtime access.
    /// </summary>
    public class ScreenshotToolsTests
    {
        [Fact]
        public void EncodeToBase64Png_ResizesWithinBoundsAndPreservesAspectRatio()
        {
            using var bitmap = new Bitmap(200, 100);

            var result = ImageCaptureUtilities.EncodeToBase64Png(bitmap, 100, 100);

            Assert.Equal(100, result.Width);
            Assert.Equal(50, result.Height);
            byte[] bytes = Convert.FromBase64String(result.ImageBase64);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8).ToArray());
            using var stream = new MemoryStream(bytes);
            using var decoded = new Bitmap(stream);
            Assert.Equal(100, decoded.Width);
            Assert.Equal(50, decoded.Height);
        }

        [Fact]
        public void VersatileImage_FromBase64PreservesPngPayloadAndMimeType()
        {
            var image = VersatileImage.FromBase64("iVBORw0KGgo=", "image/png");

            Assert.Equal(VersatileImageKind.Base64, image.Kind);
            Assert.Equal("iVBORw0KGgo=", image.RawValue);
            Assert.Equal("image/png", image.MimeType);
        }

        [Fact]
        public void BuildDescriptors_ExposesScreenshotToolsAsReadOnlyThroughMcp()
        {
            var tools = BuildTools(new FakeCanvasCaptureService(), new FakeViewportCaptureService());
            var adapter = BuildAdapter(tools);

            var descriptors = adapter.BuildDescriptors();
            var canvas = descriptors.Single(tool => tool.Name == "canvas_screenshot");
            var viewport = descriptors.Single(tool => tool.Name == "viewport_screenshot");

            Assert.True(canvas.Annotations.ReadOnlyHint);
            Assert.False(canvas.Annotations.DestructiveHint);
            Assert.Contains("vision", canvas.Tags);
            Assert.Equal(1920, (int?)canvas.InputSchema["properties"]?["maxWidth"]?["default"]);
            Assert.Equal("string", (string?)canvas.OutputSchema["properties"]?["imageBase64"]?["type"]);
            Assert.True(viewport.Annotations.ReadOnlyHint);
            Assert.False(viewport.Annotations.DestructiveHint);
            Assert.Equal(1024, (int?)viewport.InputSchema["properties"]?["width"]?["default"]);
            Assert.Equal("string", (string?)viewport.InputSchema["properties"]?["viewName"]?["type"]);
        }

        [Fact]
        public async Task ExecuteAsync_CanvasScreenshotReturnsPngMetadataThroughMcp()
        {
            var canvasService = new FakeCanvasCaptureService();
            var tools = BuildTools(canvasService, new FakeViewportCaptureService());
            var adapter = BuildAdapter(tools);

            var result = await adapter.ExecuteAsync(
                "canvas_screenshot",
                new JObject { ["maxWidth"] = 800, ["maxHeight"] = 600 }).ConfigureAwait(false);

            Assert.False(result.IsError);
            Assert.Equal(800, canvasService.MaxWidth);
            Assert.Equal(600, canvasService.MaxHeight);
            Assert.Equal("canvas-png", (string?)result.Payload["imageBase64"]);
            Assert.Equal("image/png", (string?)result.Payload["mimeType"]);
            Assert.Equal(640, (int?)result.Payload["width"]);
            Assert.Equal(360, (int?)result.Payload["height"]);
        }

        [Fact]
        public async Task ExecuteAsync_ViewportScreenshotReturnsResolvedViewThroughMcp()
        {
            var viewportService = new FakeViewportCaptureService();
            var tools = BuildTools(new FakeCanvasCaptureService(), viewportService);
            var adapter = BuildAdapter(tools);

            var result = await adapter.ExecuteAsync(
                "viewport_screenshot",
                new JObject { ["viewName"] = "Perspective", ["width"] = 900, ["height"] = 700 }).ConfigureAwait(false);

            Assert.False(result.IsError);
            Assert.Equal("Perspective", viewportService.ViewName);
            Assert.Equal(900, viewportService.Width);
            Assert.Equal(700, viewportService.Height);
            Assert.Equal("viewport-png", (string?)result.Payload["imageBase64"]);
            Assert.Equal("Perspective", (string?)result.Payload["viewName"]);
        }

        [Fact]
        public async Task ExecuteAsync_CaptureFailureReturnsMcpToolError()
        {
            var tools = BuildTools(new FailingCanvasCaptureService(), new FakeViewportCaptureService());
            var adapter = BuildAdapter(tools);

            var result = await adapter.ExecuteAsync("canvas_screenshot", new JObject()).ConfigureAwait(false);

            Assert.True(result.IsError);
            Assert.Contains("No active Grasshopper canvas", result.ErrorMessage, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ExecuteAsync_RejectsOversizedDimensionsBeforeCapture()
        {
            var canvasService = new FakeCanvasCaptureService();
            var tools = BuildTools(canvasService, new FakeViewportCaptureService());
            var adapter = BuildAdapter(tools);

            var result = await adapter.ExecuteAsync(
                "canvas_screenshot",
                new JObject { ["maxWidth"] = ImageCaptureUtilities.MaximumDimension + 1 }).ConfigureAwait(false);

            Assert.True(result.IsError);
            Assert.Null(canvasService.MaxWidth);
            Assert.Contains("between 1 and 4096", result.ErrorMessage, StringComparison.Ordinal);
        }

        private static IReadOnlyDictionary<string, AITool> BuildTools(
            ICanvasCaptureService canvasService,
            IViewportCaptureService viewportService)
        {
            return new Screenshots(canvasService, viewportService)
                .GetTools()
                .ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        }

        private static AIToolMcpAdapter BuildAdapter(IReadOnlyDictionary<string, AITool> tools)
        {
            Task<AIReturn> Execute(AIToolCall call)
            {
                return tools[call.GetToolCall().Name].Execute(call);
            }

            return new AIToolMcpAdapter(new McpServerOptions(), () => tools, Execute);
        }

        private sealed class FakeCanvasCaptureService : ICanvasCaptureService
        {
            public int? MaxWidth { get; private set; }

            public int? MaxHeight { get; private set; }

            public Task<ImageCaptureResult> CaptureCanvasAsync(int? maxWidth, int? maxHeight)
            {
                this.MaxWidth = maxWidth;
                this.MaxHeight = maxHeight;
                return Task.FromResult(new ImageCaptureResult("canvas-png", 640, 360));
            }
        }

        private sealed class FakeViewportCaptureService : IViewportCaptureService
        {
            public string? ViewName { get; private set; }

            public int? Width { get; private set; }

            public int? Height { get; private set; }

            public Task<ImageCaptureResult> CaptureViewportAsync(string? viewName, int? width, int? height)
            {
                this.ViewName = viewName;
                this.Width = width;
                this.Height = height;
                return Task.FromResult(new ImageCaptureResult("viewport-png", 900, 600, viewName ?? "Perspective"));
            }
        }

        private sealed class FailingCanvasCaptureService : ICanvasCaptureService
        {
            public Task<ImageCaptureResult> CaptureCanvasAsync(int? maxWidth, int? maxHeight)
            {
                throw new InvalidOperationException("No active Grasshopper canvas is available.");
            }
        }
    }
}
