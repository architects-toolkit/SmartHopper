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
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.Types;

namespace SmartHopper.Components.Img
{
    /// <summary>
    /// Grasshopper component for generating images using AI.
    /// </summary>
    public class AIText2ImgComponent : AIStatefulAsyncComponentBase
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("B4E69EAD-2EEB-413C-8E47-19D5079882BE");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.texttoimg;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "AI Image Generate",
            "AIImgGen",
            "AIImageGen",
            "AIImgGenerate",
            "AIImageGenerate",
            "text2img",
            "Image Generate",
            "Generate Image",
            "Create Image",
            "AI Picture",
            "Picture Generate",
            "Image AI",
            "AI Art",
            "AI Drawing",
            "DALL-E",
            "Image Synthesis",
            "Text to Image",
            "Prompt to Image",
        };

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "text2img" };

        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.ItemGraft,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        // This component uses ItemGraft topology to create separate branches for each prompt's generated images

        /// <summary>
        /// Initializes a new instance of the AIText2ImgComponent class.
        /// </summary>
        public AIText2ImgComponent()
            : base(
                "AI Text To Image",
                "AIText2Img",
                "Generate images using AI based on text prompts.",
                "SmartHopper",
                "Img")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "Text prompt describing the desired image", GH_ParamAccess.tree);
            pManager.AddTextParameter("Size", "S", "Image size (e.g., '1024x1024', '1792x1024', '1024x1792', '1K', '2K', '4K'). Not all providers accept all sizes.", GH_ParamAccess.tree, "1024x1024");
            pManager.AddTextParameter("Quality", "Q", "Image quality ('standard' or 'hd'). Used by OpenAI image generation.", GH_ParamAccess.tree, "standard");
            pManager.AddTextParameter("Style", "St", "Image style ('vivid' or 'natural'). Used by OpenAI image generation.", GH_ParamAccess.tree, "vivid");
            pManager.AddTextParameter("Aspect Ratio", "AR", "Image aspect ratio (e.g., '1:1', '16:9', '4:3'). Used by Gemini image generation.", GH_ParamAccess.tree, string.Empty);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Image", "I", "Generated image as VersatileImage format", GH_ParamAccess.tree);
            pManager.AddTextParameter("Revised Prompt", "RP", "AI-revised prompt used for generation", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIText2ImgWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        /// <summary>
        /// Async worker for the AI Text To Image component.
        /// </summary>
        private sealed class AIText2ImgWorker : AsyncWorkerBase
        {
            private readonly AIText2ImgComponent parent;
            private readonly ProcessingOptions processingOptions;
            private Dictionary<string, GH_Structure<GH_String>> inputTrees;
            private bool hasWork;

            private GH_Structure<IGH_Goo> imageResults;
            private GH_Structure<IGH_Goo> revisedPromptResults;
            private bool success;
            private string errorMessage;

            public AIText2ImgWorker(
                AIText2ImgComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                ProcessingOptions processingOptions)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
            }

            /// <summary>
            /// Gathers input from the component.
            /// </summary>
            /// <param name="DA">Data access object.</param>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                var prompts = new GH_Structure<GH_String>();
                var sizes = new GH_Structure<GH_String>();
                var qualities = new GH_Structure<GH_String>();
                var styles = new GH_Structure<GH_String>();
                GH_Structure<GH_String> aspectRatios;

                // Parameter indices:
                // 0: Prompt (tree access)
                // 1: Size (tree access)
                // 2: Quality (tree access)
                // 3: Style (tree access)
                // 4: Aspect Ratio (tree access)

                // Prompt parameter index
                if (!DA.GetDataTree(0, out prompts))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to get Prompt input");
                    dataCount = 0;
                    return;
                }

                // Size parameter index
                if (!DA.GetDataTree(1, out sizes) || sizes == null || sizes.DataCount == 0)
                {
                    // Use default if not provided
                    sizes = new GH_Structure<GH_String>();
                    sizes.Append(new GH_String("1024x1024"), new GH_Path(0));
                }

                // Quality parameter index
                if (!DA.GetDataTree(2, out qualities) || qualities == null || qualities.DataCount == 0)
                {
                    // Use default if not provided
                    qualities = new GH_Structure<GH_String>();
                    qualities.Append(new GH_String("standard"), new GH_Path(0));
                }

                // Style parameter index
                if (!DA.GetDataTree(3, out styles) || styles == null || styles.DataCount == 0)
                {
                    // Use default if not provided
                    styles = new GH_Structure<GH_String>();
                    styles.Append(new GH_String("vivid"), new GH_Path(0));
                }

                // Aspect Ratio parameter index
                if (!DA.GetDataTree(4, out aspectRatios) || aspectRatios == null || aspectRatios.DataCount == 0)
                {
                    // Use default if not provided
                    aspectRatios = new GH_Structure<GH_String>();
                    aspectRatios.Append(new GH_String(string.Empty), new GH_Path(0));
                }

                this.inputTrees = new Dictionary<string, GH_Structure<GH_String>>
                {
                    { "Prompt", prompts },
                    { "Size", sizes },
                    { "Quality", qualities },
                    { "Style", styles },
                    { "AspectRatio", aspectRatios },
                };

                this.hasWork = prompts != null && prompts.DataCount > 0;
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Prompt is required.");
                }

                // Data count is computed centrally in RunProcessingAsync for item-based metrics.
                dataCount = 0;
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                this.imageResults = new GH_Structure<IGH_Goo>();
                this.revisedPromptResults = new GH_Structure<IGH_Goo>();
                this.success = false;
                this.errorMessage = null;

                if (!this.hasWork)
                {
                    return;
                }

                try
                {
                    // Get the current AI provider settings
                    var providerName = this.parent.GetActualAIProviderName();
                    var model = this.parent.GetModel();

                    if (string.IsNullOrEmpty(providerName))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No AI provider selected");
                        this.errorMessage = "No AI provider selected";
                        return;
                    }

                    var resultTrees = await this.parent.RunProcessingAsync<GH_String, IGH_Goo>(
                        this.inputTrees,
                        async branchInputs =>
                        {
                            var outputs = new Dictionary<string, List<IGH_Goo>>
                            {
                                { "Image", new List<IGH_Goo>() },
                                { "RevisedPrompt", new List<IGH_Goo>() },
                            };

                            if (!branchInputs.TryGetValue("Prompt", out var promptBranch) || promptBranch == null || promptBranch.Count == 0)
                            {
                                return outputs;
                            }

                            branchInputs.TryGetValue("Size", out var sizeBranch);
                            branchInputs.TryGetValue("Quality", out var qualityBranch);
                            branchInputs.TryGetValue("Style", out var styleBranch);
                            branchInputs.TryGetValue("AspectRatio", out var aspectRatioBranch);

                            var normalized = DataTreeProcessor.NormalizeBranchLengths(
                                new List<List<GH_String>>
                                {
                                    promptBranch,
                                    sizeBranch ?? new List<GH_String>(),
                                    qualityBranch ?? new List<GH_String>(),
                                    styleBranch ?? new List<GH_String>(),
                                    aspectRatioBranch ?? new List<GH_String>(),
                                });

                            var prompts = normalized[0];
                            var sizes = normalized[1];
                            var qualities = normalized[2];
                            var styles = normalized[3];
                            var aspectRatios = normalized[4];

                            for (int i = 0; i < prompts.Count; i++)
                            {
                                token.ThrowIfCancellationRequested();

                                var prompt = prompts[i]?.Value ?? string.Empty;
                                var size = sizes[i]?.Value ?? "1024x1024";
                                var quality = qualities[i]?.Value ?? "standard";
                                var style = styles[i]?.Value ?? "vivid";
                                var aspectRatio = aspectRatios[i]?.Value ?? string.Empty;

                                if (string.IsNullOrEmpty(prompt))
                                {
                                    outputs["Image"].Add(new GH_VersatileImage());
                                    outputs["RevisedPrompt"].Add(new GH_String(string.Empty));
                                    continue;
                                }

                                try
                                {
                                    // Execute the AI tool
                                    var parameters = new JObject
                                    {
                                        ["prompt"] = prompt,
                                        ["size"] = size,
                                        ["quality"] = quality,
                                        ["style"] = style,
                                        ["aspect_ratio"] = aspectRatio,
                                    };

                                    var toolResult = await this.parent.CallAIToolAsync(
                                        "text2img", parameters, token)
                                        .ConfigureAwait(false);

                                    // Treat missing 'success' as true (CallAIToolAsync returns direct result without 'success' on success)
                                    // Check for errors in messages array
                                    var hasErrors = toolResult?["messages"] is JArray messages && messages.Any(m => m["severity"]?.ToString() == "Error");
                                    if (toolResult != null && ((toolResult["success"]?.Value<bool?>() ?? true) && !hasErrors))
                                    {
                                        // Get the image result (could be URL or base64 data)
                                        string imageResult = toolResult["result"]?.ToString() ?? string.Empty;

                                        // Get revised prompt - now returned directly from new schema
                                        string revisedPrompt = toolResult["revisedPrompt"]?.ToString() ?? prompt;

                                        // Process the image result (URL or base64)
                                        if (!string.IsNullOrEmpty(imageResult))
                                        {
                                            try
                                            {
                                                VersatileImage imageSource;

                                                // Check if it's a URL or base64 data
                                                if (imageResult.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || imageResult.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    // Store as URL
                                                    imageSource = VersatileImage.FromString(imageResult);
                                                }
                                                else
                                                {
                                                    // Convert from base64 to bitmap and store as Bitmap
                                                    var base64Data = imageResult.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                                                        ? imageResult.Substring(imageResult.IndexOf(",", StringComparison.Ordinal) + 1)
                                                        : imageResult;
                                                    var imageBytes = Convert.FromBase64String(base64Data);
                                                    using var stream = new MemoryStream(imageBytes);
                                                    var bitmap = new Bitmap(stream);
                                                    imageSource = VersatileImage.FromBitmap(bitmap);
                                                }

                                                // Wrap in GH_VersatileImage for output
                                                outputs["Image"].Add(new GH_VersatileImage(imageSource));
                                                outputs["RevisedPrompt"].Add(new GH_String(revisedPrompt));
                                            }
                                            catch (Exception processEx)
                                            {
                                                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Failed to process image: {processEx.Message}");
                                                outputs["Image"].Add(new GH_VersatileImage());
                                                outputs["RevisedPrompt"].Add(new GH_String(revisedPrompt));
                                            }
                                        }
                                        else
                                        {
                                            outputs["Image"].Add(new GH_VersatileImage());
                                            outputs["RevisedPrompt"].Add(new GH_String(revisedPrompt));
                                        }
                                    }
                                    else
                                    {
                                        // Surface all error messages from the messages array
                                        var hasErrorsLocal = false;
                                        if (toolResult?["messages"] is JArray msgs)
                                        {
                                            foreach (var msg in msgs.Where(m => m["severity"]?.ToString() == "Error"))
                                            {
                                                var errorText = msg["message"]?.ToString();
                                                var origin = msg["origin"]?.ToString();
                                                if (!string.IsNullOrEmpty(errorText))
                                                {
                                                    var prefix = !string.IsNullOrEmpty(origin) ? $"[{origin}] " : string.Empty;
                                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"{prefix}{errorText}");
                                                    hasErrorsLocal = true;
                                                }
                                            }
                                        }

                                        if (!hasErrorsLocal)
                                        {
                                            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Image generation failed: Unknown error occurred");
                                        }

                                        outputs["Image"].Add(new GH_VersatileImage());
                                        outputs["RevisedPrompt"].Add(new GH_String(string.Empty));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Error processing item: {ex.Message}");
                                    outputs["Image"].Add(new GH_VersatileImage());
                                    outputs["RevisedPrompt"].Add(new GH_String(string.Empty));
                                }
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    this.imageResults = new GH_Structure<IGH_Goo>();
                    this.revisedPromptResults = new GH_Structure<IGH_Goo>();

                    if (resultTrees.TryGetValue("Image", out var images))
                    {
                        this.imageResults = images;
                    }

                    if (resultTrees.TryGetValue("RevisedPrompt", out var revisedPrompts))
                    {
                        this.revisedPromptResults = revisedPrompts;
                    }

                    this.success = true;
                }
                catch (OperationCanceledException)
                {
                    this.success = false;
                    this.errorMessage = "Operation was cancelled";
                }
                catch (Exception ex)
                {
                    this.success = false;
                    this.errorMessage = ex.Message;
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error in image generation: {ex.Message}");
                }
            }

            /// <summary>
            /// Sets the output from the async work.
            /// </summary>
            /// <param name="DA">Data access object.</param>
            /// <param name="message">Output message.</param>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                if (this.success)
                {
                    var imagesTree = this.imageResults ?? new GH_Structure<IGH_Goo>();
                    var revisedTree = this.revisedPromptResults ?? new GH_Structure<IGH_Goo>();

                    this.parent.SetPersistentOutput("Image", imagesTree, DA);
                    this.parent.SetPersistentOutput("Revised Prompt", revisedTree, DA);
                    this.parent.SetMetricsOutput(DA);

                    message = "Image generation completed successfully";
                }
                else
                {
                    message = $"Error: {this.errorMessage ?? "Unknown error"}";
                }
            }
        }
    }
}
