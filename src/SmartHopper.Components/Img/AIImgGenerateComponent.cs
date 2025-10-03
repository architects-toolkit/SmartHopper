/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Components.Img
{
    /// <summary>
    /// Grasshopper component for generating images using AI.
    /// </summary>
    public class AIImgGenerateComponent : AIStatefulAsyncComponentBase
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("B4E69EAD-2EEB-413C-8E47-19D5079882BE");

        /// <summary>
        /// Gets the icon for this component.
        /// </summary>
        protected override Bitmap Icon => Resources.imggenerate;

        /// <summary>
        /// Gets the exposure level of this component in the ribbon.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override AICapability RequiredCapability => AICapability.Text2Image;

        /// <summary>
        /// Initializes a new instance of the AIImgGenerateComponent class.
        /// </summary>
        public AIImgGenerateComponent()
            : base(
                  "AI Image Generate",
                  "AIImgGen",
                  "Generate images using AI based on text prompts.",
                  "SmartHopper", "Img")
        {
        }

        /// <summary>
        /// Registers additional input parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register inputs with.</param>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Prompt", "P", "Text prompt describing the desired image", GH_ParamAccess.tree);
            pManager.AddTextParameter("Size", "S", "Image size (e.g., '1024x1024', '1792x1024', '1024x1792')", GH_ParamAccess.tree, "1024x1024");
            pManager.AddTextParameter("Quality", "Q", "Image quality ('standard' or 'hd')", GH_ParamAccess.tree, "standard");
            pManager.AddTextParameter("Style", "St", "Image style ('vivid' or 'natural')", GH_ParamAccess.tree, "vivid");
        }

        /// <summary>
        /// Registers additional output parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager to register outputs with.</param>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Image", "I", "Generated image as bitmap", GH_ParamAccess.tree);
            pManager.AddTextParameter("Revised Prompt", "RP", "AI-revised prompt used for generation", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Creates the async worker for this component.
        /// </summary>
        /// <param name="progressReporter">Progress reporter callback.</param>
        /// <returns>The async worker instance.</returns>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIImgGenerateWorker(this, this.AddRuntimeMessage, progressReporter);
        }

        /// <summary>
        /// Async worker for the AI Image Generate component.
        /// </summary>
        private sealed class AIImgGenerateWorker : AsyncWorkerBase
        {
            private readonly AIImgGenerateComponent _parent;
            private readonly Action<string> _progressReporter;
            private readonly Dictionary<string, object> _result = new Dictionary<string, object>();
            private GH_Structure<GH_String> _prompts;
            private GH_Structure<GH_String> _sizes;
            private GH_Structure<GH_String> _qualities;
            private GH_Structure<GH_String> _styles;

            public AIImgGenerateWorker(
                AIImgGenerateComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                Action<string> progressReporter)
                : base(parent, addRuntimeMessage)
            {
                this._parent = parent;
                this._progressReporter = progressReporter;
            }

            /// <summary>
            /// Gathers input from the component.
            /// </summary>
            /// <param name="DA">Data access object.</param>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                this._prompts = new GH_Structure<GH_String>();
                this._sizes = new GH_Structure<GH_String>();
                this._qualities = new GH_Structure<GH_String>();
                this._styles = new GH_Structure<GH_String>();

                // Parameter indices:
                // 0: Prompt (tree access)
                // 1: Size (tree access)
                // 2: Quality (tree access)
                // 3: Style (tree access)

                // Prompt parameter index
                if (!DA.GetDataTree(0, out this._prompts))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to get Prompt input");
                    dataCount = 0;
                    return;
                }

                // Size parameter index
                if (!DA.GetDataTree(1, out this._sizes))
                {
                    // Use default if not provided
                    this._sizes.Append(new GH_String("1024x1024"), new GH_Path(0));
                }

                // Quality parameter index
                if (!DA.GetDataTree(2, out this._qualities))
                {
                    // Use default if not provided
                    this._qualities.Append(new GH_String("standard"), new GH_Path(0));
                }

                // Style parameter index
                if (!DA.GetDataTree(3, out this._styles))
                {
                    // Use default if not provided
                    this._styles.Append(new GH_String("vivid"), new GH_Path(0));
                }

                dataCount = 1;
            }

            /// <summary>
            /// Performs the async work to generate images.
            /// </summary>
            /// <param name="token">Cancellation token.</param>
            /// <returns>Async task.</returns>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                // Data tree structures to store the results
                var imageResults = new GH_Structure<IGH_Goo>();
                var revisedPromptResults = new GH_Structure<GH_String>();

                try
                {
                    // Get the current AI provider settings
                    var providerName = this._parent.GetActualAIProviderName();
                    var model = this._parent.GetModel();

                    if (string.IsNullOrEmpty(providerName))
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No AI provider selected");
                        return;
                    }

                    var totalPaths = this._prompts.Paths.Count;
                    var processedPaths = 0;

                    // Process each path in the data tree
                    foreach (var path in this._prompts.Paths)
                    {
                        token.ThrowIfCancellationRequested();

                        // Update progress
                        processedPaths++;
                        this._progressReporter?.Invoke($"Process {processedPaths}/{totalPaths}");

                        var promptBranch = this._prompts.get_Branch(path);
                        var sizeBranch = this._sizes.PathExists(path) ? this._sizes.get_Branch(path) : this._sizes.get_Branch(new GH_Path(0));
                        var qualityBranch = this._qualities.PathExists(path) ? this._qualities.get_Branch(path) : this._qualities.get_Branch(new GH_Path(0));
                        var styleBranch = this._styles.PathExists(path) ? this._styles.get_Branch(path) : this._styles.get_Branch(new GH_Path(0));

                        var branchResults = new List<IGH_Goo>();
                        var branchRevisedPrompts = new List<GH_String>();

                        // Process each item in the branch
                        for (int i = 0; i < promptBranch.Count; i++)
                        {
                            token.ThrowIfCancellationRequested();

                            var prompt = (promptBranch[i] as GH_String)?.Value ?? string.Empty;
                            var size = (i < sizeBranch.Count ? (sizeBranch[i] as GH_String)?.Value : (sizeBranch.Count > 0 ? (sizeBranch[sizeBranch.Count - 1] as GH_String)?.Value : null)) ?? "1024x1024";
                            var quality = (i < qualityBranch.Count ? (qualityBranch[i] as GH_String)?.Value : (qualityBranch.Count > 0 ? (qualityBranch[qualityBranch.Count - 1] as GH_String)?.Value : null)) ?? "standard";
                            var style = (i < styleBranch.Count ? (styleBranch[i] as GH_String)?.Value : (styleBranch.Count > 0 ? (styleBranch[styleBranch.Count - 1] as GH_String)?.Value : null)) ?? "vivid";

                            if (string.IsNullOrEmpty(prompt))
                            {
                                branchResults.Add(new GH_String(""));
                                branchRevisedPrompts.Add(new GH_String(""));
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
                                };

                                var toolResult = await this._parent.CallAiToolAsync(
                                    "img_generate", parameters)
                                    .ConfigureAwait(false);

                                // Treat missing 'success' as true (CallAiToolAsync returns direct result without 'success' on success)
                                // and ensure there's no 'error' key to consider it a success.
                                if (toolResult != null && ((toolResult["success"]?.Value<bool?>() ?? true) && string.IsNullOrEmpty(toolResult["error"]?.ToString())))
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
                                            Bitmap bitmap;

                                            // Check if it's a URL or base64 data
                                            if (imageResult.StartsWith("http://") || imageResult.StartsWith("https://"))
                                            {
                                                // Download from URL
                                                using var httpClient = new HttpClient();
                                                var imageData = await httpClient.GetByteArrayAsync(imageResult).ConfigureAwait(false);
                                                using var stream = new MemoryStream(imageData);
                                                bitmap = new Bitmap(stream);
                                            }
                                            else
                                            {
                                                // Convert from base64
                                                var base64Data = imageResult.StartsWith("data:image/")
                                                    ? imageResult.Substring(imageResult.IndexOf(",") + 1)
                                                    : imageResult;
                                                var imageBytes = Convert.FromBase64String(base64Data);
                                                using var stream = new MemoryStream(imageBytes);
                                                bitmap = new Bitmap(stream);
                                            }

                                            // Wrap the bitmap in a Grasshopper-compatible image object
                                            branchResults.Add(new GH_ObjectWrapper(bitmap));
                                            branchRevisedPrompts.Add(new GH_String(revisedPrompt));
                                        }
                                        catch (Exception processEx)
                                        {
                                            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Failed to process image: {processEx.Message}");
                                            branchResults.Add(new GH_ObjectWrapper(null));
                                            branchRevisedPrompts.Add(new GH_String(revisedPrompt));
                                        }
                                    }
                                    else
                                    {
                                        branchResults.Add(new GH_ObjectWrapper(null));
                                        branchRevisedPrompts.Add(new GH_String(revisedPrompt));
                                    }
                                }
                                else
                                {
                                    var errorMessage = toolResult?["error"]?.ToString() ?? "Unknown error occurred";
                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Image generation failed: {errorMessage}");
                                    branchResults.Add(new GH_ObjectWrapper(null));
                                    branchRevisedPrompts.Add(new GH_String(""));
                                }
                            }
                            catch (Exception ex)
                            {
                                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Error processing item: {ex.Message}");
                                branchResults.Add(new GH_ObjectWrapper(null));
                                branchRevisedPrompts.Add(new GH_String(""));
                            }
                        }

                        // Add results to output structures
                        imageResults.AppendRange(branchResults, path);
                        revisedPromptResults.AppendRange(branchRevisedPrompts, path);
                    }

                    // Store results for SetOutput
                    this._result["Images"] = imageResults;
                    this._result["RevisedPrompts"] = revisedPromptResults;
                    this._result["Success"] = true;
                }
                catch (OperationCanceledException)
                {
                    this._result["Success"] = false;
                    this._result["Error"] = "Operation was cancelled";
                }
                catch (Exception ex)
                {
                    this._result["Success"] = false;
                    this._result["Error"] = ex.Message;
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
                if (this._result.TryGetValue("Success", out var success) && (bool)success)
                {
                    if (this._result.TryGetValue("Images", out var images) && images is GH_Structure<IGH_Goo> imageTree)
                    {
                        this._parent.SetPersistentOutput("Image", imageTree, DA);
                    }

                    if (this._result.TryGetValue("RevisedPrompts", out var revisedPrompts) && revisedPrompts is GH_Structure<GH_String> promptTree)
                    {
                        this._parent.SetPersistentOutput("Revised Prompt", promptTree, DA);
                    }

                    message = "Image generation completed successfully";
                }
                else
                {
                    this._result.TryGetValue("Error", out var error);
                    message = $"Error: {error ?? "Unknown error"}";
                }
            }
        }
    }
}
