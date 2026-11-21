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
using SmartHopper.Infrastructure.FileSystem;
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
                    ""description"": ""Optional file path that also receives the PNG. Relative names are written inside the SmartHopper Screenshots folder and never overwrite. Absolute paths require user approval and must end in .png.""
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
                    ""description"": ""Optional file path that also receives the PNG. Relative names are written inside the SmartHopper Screenshots folder and never overwrite. Absolute paths require user approval and must end in .png.""
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
                    ""default"": 2.0,
                    ""description"": ""Render zoom factor. 1.0 matches on-screen size; 2.0 doubles resolution for print.""
                },
                ""background"": {
                    ""type"": ""string"",
                    ""default"": ""transparent"",
                    ""description"": ""Background colour: 'transparent', 'white', 'canvas', or a '#RRGGBB'/'#AARRGGBB' hex colour.""
                },
                ""savePath"": {
                    ""type"": ""string"",
                    ""description"": ""Optional file path that also receives the PNG. Relative names are written inside the SmartHopper Screenshots folder and never overwrite. Absolute paths require user approval and must end in .png.""
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
                category: "Canvas.Vision",
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
                    title: "Grasshopper Canvas Screenshot"),
                activityLabel: "capturing canvas screenshot",
                resultLabel: "captured canvas screenshot");

            yield return new AITool(
                name: ViewportToolName,
                description: "Captures the active or named Rhino 3D viewport as a base64-encoded PNG. The image is resized to fit within width and height while preserving its aspect ratio.",
                category: "Canvas.Vision",
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
                    title: "Rhino Viewport Screenshot"),
                activityLabel: "capturing viewport screenshot",
                resultLabel: "captured viewport screenshot");

            yield return new AITool(
                name: HiResToolName,
                description: "Renders a region of the Grasshopper canvas to a high-resolution PNG for publishing or printing, independent of the visible viewport. Use 'scope' to capture the whole document, the current selection, specific components by GUID, or an explicit bounds rectangle; 'scale' controls resolution (default 2.0; 1.0 matches on-screen size); 'background' supports transparency. This tool is meant for presentable output, not for AI agent vision of the canvas: it can produce very large images that would consume too many unnecessary tokens. Use canvas_screenshot for agent vision instead.",
                category: "Canvas.Vision",
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
                    title: "Grasshopper Canvas Hi-Res Screenshot"),
                activityLabel: "capturing hi-res screenshot",
                resultLabel: "captured hi-res screenshot");
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
        /// Writes the captured PNG to <paramref name="savePath"/> when provided. Relative
        /// names land in the screenshot export directory under SmartHopper's app data and
        /// never overwrite; absolute paths need a .png extension and explicit approval
        /// through the local-file consent gate, which warns the user before overwriting.
        /// Returns the path that was written, or <see langword="null"/> when no savePath
        /// was given.
        /// </summary>
        private static string? SavePng(ImageCaptureResult capture, string? savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                return null;
            }

            var trimmed = savePath.Trim();
            var mode = FileMode.CreateNew;
            string fullPath;

            // Re-validates the still-open destination before any bytes are written:
            // sandbox exports must still resolve inside Screenshots, and a consented
            // absolute path must still resolve to exactly the approved file.
            Func<string, bool> destinationCheck;
            if (Path.IsPathRooted(trimmed))
            {
                if (!LocalPath.TryCanonicalize(trimmed, out var canonical, out var error))
                {
                    throw new InvalidOperationException(error);
                }

                if (!string.Equals(Path.GetExtension(canonical), ".png", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("savePath must point to a .png file.");
                }

                // Blocking wait is safe here except on the Rhino UI thread, where an
                // unanswered prompt would deadlock — RequireBlocking refuses instead.
                LocalFileAccess.RequireBlocking(canonical, LocalFileCapability.Write, "screenshots");
                mode = FileMode.OpenOrCreate;
                fullPath = canonical;
                var approved = canonical;
                destinationCheck = path =>
                    LocalPath.TryCanonicalize(path, out var check, out _)
                    && LocalPath.Comparer.Equals(check, approved);
            }
            else
            {
                // Relative names stay inside the export sandbox. Both sides are
                // canonicalized so a linked subfolder inside the sandbox resolves to
                // its real target before the containment check.
                var root = LocalPath.ScreenshotsDirectory;
                if (!LocalPath.TryCanonicalize(Path.GetFullPath(Path.Combine(root, trimmed)), out var resolved, out var error)
                    || !LocalPath.TryCanonicalize(root, out var resolvedRoot, out _))
                {
                    throw new InvalidOperationException(error);
                }

                if (!LocalPath.IsUnder(resolvedRoot, resolved) || string.Equals(resolved, resolvedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Relative savePath values must stay inside the Screenshots folder.");
                }

                if (!string.Equals(Path.GetExtension(resolved), ".png", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("savePath must point to a .png file.");
                }

                var sandbox = resolvedRoot;
                destinationCheck = path =>
                    LocalPath.TryCanonicalize(path, out var check, out _)
                    && LocalPath.IsUnder(sandbox, check);
                fullPath = resolved;
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Open without truncating first: a link planted while consent was pending
            // must not be followed. The destination spelling is re-canonicalized with
            // the handle open — once verified, writes are pinned to that file.
            FileStream? stream = null;
            try
            {
                try
                {
                    stream = mode == FileMode.CreateNew
                        ? new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)
                        : new FileStream(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException) when (mode == FileMode.CreateNew)
                {
                    throw new InvalidOperationException($"'{fullPath}' already exists; exports never overwrite.");
                }

                if (!destinationCheck(fullPath))
                {
                    stream.Dispose();
                    stream = null;
                    try
                    {
                        File.Delete(fullPath);
                    }
                    catch (IOException)
                    {
                        // Best effort: the check failure itself is the error that matters.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Best effort: the check failure itself is the error that matters.
                    }

                    throw new InvalidOperationException(
                        $"The destination '{fullPath}' changed while it was being prepared; export aborted.");
                }

                stream.SetLength(0);
                var bytes = Convert.FromBase64String(capture.ImageBase64);
                stream.Write(bytes, 0, bytes.Length);
                return fullPath;
            }
            finally
            {
                stream?.Dispose();
            }
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
            request.Scale = args["scale"]?.ToObject<float>() ?? 2f;
            request.Background = ParseBackground(args["background"]?.ToString());
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
