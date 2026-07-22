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
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.ComponentBase.Batch;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.Models;
using SmartHopper.Core.Types;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Diagnostics;

namespace SmartHopper.Components.Img
{
    /// <summary>
    /// Grasshopper component for describing or analyzing images using a vision AI model.
    /// Accepts image file paths or URLs and returns AI-generated text descriptions.
    /// </summary>
    public class AIImg2TextComponent : AIStatefulAsyncComponentBase
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("6498B5BA-D781-42B6-8D74-25DA73E32004");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.imgtotext;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "AIImg2Txt",
            "AIImgToText",
            "Image to Text",
            "img2text",
            "Vision AI",
            "Image Analysis",
            "Image Description",
            "Describe Image",
            "Analyze Image",
            "Image Caption",
            "Caption Image",
            "Image Reader",
            "AI Vision",
            "Computer Vision",
            "Image Understanding",
            "OCR AI",
            "Image to Words",
            "Picture Description",
        };

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "AIImg2Txt",
            "AIImgToText",
            "Image to Text",
            "img2text",
            "Vision AI",
            "Image Analysis",
            "Image Description",
            "Describe Image",
            "Analyze Image",
            "Image Caption",
            "Caption Image",
            "Image Reader",
            "AI Vision",
            "Computer Vision",
            "Image Understanding",
            "OCR AI",
            "Image to Words",
            "Picture Description",
        };

        /// <inheritdoc/>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "img2text" };

        /// <inheritdoc/>
        protected override ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.ItemGraft,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AIImg2TextComponent"/> class.
        /// </summary>
        public AIImg2TextComponent()
            : base(
                  "AI Image To Text",
                  "AIImg2Text",
                  "Describe or analyze images using a vision AI model. Accepts image file paths or URLs and returns AI-generated text descriptions.",
                  "SmartHopper",
                  "Img")
        {
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Image", "I", "Image to describe. Accepts: (1) VersatileImage format from file extraction components, (2) absolute file path to an image file (.png, .jpg, .gif, .bmp, .webp, .tiff), (3) public HTTP/HTTPS URL, or (4) raw base64-encoded image data.", GH_ParamAccess.tree);
            pManager.AddTextParameter("Prompt", "P", "Custom description prompt for the AI. Leave empty to use the built-in prompt: 'Describe this image in detail.'", GH_ParamAccess.item);
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Description", "D", "AI-generated text description of the image.", GH_ParamAccess.tree);
        }

        /// <inheritdoc/>
        protected override void OnBatchCompleted(IReadOnlyDictionary<string, JObject> results, IReadOnlyList<SHRuntimeMessage> messages = null)
        {
            var sentinel = this.GetSentinelTree("Description");
            if (results == null || sentinel == null) return;

            // ProcessBatchResults automatically persists outputs and sets metrics
            this.ProcessBatchResults<GH_String>(
                "Description",
                sentinel,
                results,
                (customId, resultBody) =>
                {
                    var provider = ProviderManager.Instance.GetProvider(this.GetActualAIProviderName());
                    if (provider == null) return new GH_String(string.Empty);

                    var interactions = provider.Decode(resultBody);
                    var lastText = interactions
                        ?.OfType<AIInteractionText>()
                        .LastOrDefault(i => i.Agent == AIAgent.Assistant);

                    return new GH_String(lastText?.Content ?? string.Empty);
                },
                messages);
        }

        /// <summary>
        /// Creates the async worker for this component.
        /// </summary>
        /// <param name="progressReporter">Progress reporter callback.</param>
        /// <returns>The async worker instance.</returns>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIImg2TextWorker(this, this.AddRuntimeMessage, this.ComponentProcessingOptions);
        }

        /// <summary>
        /// Returns the MIME type string for the given image file extension.
        /// </summary>
        /// <param name="filePath">Path to the image file.</param>
        /// <returns>MIME type string.</returns>
        private static string GetMimeType(string filePath)
        {
            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".tiff" or ".tif" => "image/tiff",
                _ => "image/png",
            };
        }

        /// <summary>
        /// Async worker for the AI Image To Text component.
        /// </summary>
        private sealed class AIImg2TextWorker : AsyncWorkerBase
        {
            private readonly AIImg2TextComponent parent;
            private readonly ProcessingOptions processingOptions;
            private bool hasWork;

            private string prompt;

            private Dictionary<string, GH_Structure<IGH_Goo>> inputTrees;
            private GH_Structure<GH_String> resultDescriptions;

            public AIImg2TextWorker(
                AIImg2TextComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                ProcessingOptions processingOptions)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
                this.processingOptions = processingOptions;
            }

            /// <summary>
            /// Gathers inputs from the component.
            /// </summary>
            /// <param name="DA">Data access object.</param>
            /// <param name="dataCount">Number of data items to process.</param>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                var imageTree = new GH_Structure<IGH_Goo>();
                DA.GetDataTree("Image", out imageTree);

                var promptParam = new GH_String();
                DA.GetData("Prompt", ref promptParam);
                this.prompt = promptParam?.Value;

                this.inputTrees = new Dictionary<string, GH_Structure<IGH_Goo>>
                {
                    { "Image", imageTree ?? new GH_Structure<IGH_Goo>() },
                };

                this.hasWork = imageTree != null && imageTree.DataCount > 0;
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Image input is required.");
                }

                dataCount = 0;
                this.resultDescriptions = new GH_Structure<GH_String>();
            }

            /// <summary>
            /// Performs the async work to describe images.
            /// </summary>
            /// <param name="token">Cancellation token.</param>
            /// <returns>Async task.</returns>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                this.resultDescriptions = new GH_Structure<GH_String>();

                if (!this.hasWork)
                {
                    return;
                }

                try
                {
                    var resultTrees = await this.parent.RunProcessingAsync<IGH_Goo, GH_String>(
                        this.inputTrees,
                        async branchInputs =>
                        {
                            var outputs = new Dictionary<string, List<GH_String>>
                            {
                                { "Description", new List<GH_String>() },
                            };

                            if (!branchInputs.TryGetValue("Image", out var imageBranch) || imageBranch == null)
                            {
                                return outputs;
                            }

                            foreach (var item in imageBranch)
                            {
                                token.ThrowIfCancellationRequested();

                                var imgParams = new JObject();
                                bool hasImage = false;

                                if (item is GH_VersatileImage versatileImage && versatileImage.Value != null)
                                {
                                    var source = versatileImage.Value;
                                    if (source.Kind == VersatileImageKind.Base64 && !string.IsNullOrWhiteSpace(source.RawValue))
                                    {
                                        imgParams["imageBase64"] = source.RawValue;
                                        imgParams["mimeType"] = "image/png";
                                        hasImage = true;
                                    }
                                    else if (source.Kind == VersatileImageKind.Url && !string.IsNullOrWhiteSpace(source.RawValue))
                                    {
                                        imgParams["imageUrl"] = source.RawValue;
                                        hasImage = true;
                                    }
                                    else if (source.Kind == VersatileImageKind.LocalFile && !string.IsNullOrWhiteSpace(source.RawValue))
                                    {
                                        try
                                        {
                                            if (!File.Exists(source.RawValue))
                                            {
                                                this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Image file not found: {source.RawValue}");
                                            }
                                            else
                                            {
                                                var bytes = File.ReadAllBytes(source.RawValue);
                                                imgParams["imageBase64"] = Convert.ToBase64String(bytes);
                                                imgParams["mimeType"] = GetMimeType(source.RawValue);
                                                hasImage = true;
                                            }
                                        }
                                        catch (UnauthorizedAccessException ex)
                                        {
                                            this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Unable to access image file '{source.RawValue}': {ex.Message}");
                                        }
                                        catch (IOException ex)
                                        {
                                            this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"Failed to read image file '{source.RawValue}': {ex.Message}");
                                        }
                                    }
                                    else if (source.Kind == VersatileImageKind.Bitmap && source.Bitmap != null)
                                    {
                                        using var ms = new MemoryStream();
                                        source.Bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                        imgParams["imageBase64"] = Convert.ToBase64String(ms.ToArray());
                                        imgParams["mimeType"] = "image/png";
                                        hasImage = true;
                                    }
                                }
                                else
                                {
                                    string imageValue = null;
                                    if (item is GH_String ghStr)
                                    {
                                        imageValue = ghStr.Value;
                                    }
                                    else if (item != null)
                                    {
                                        var castStr = new GH_String();
                                        if (item.CastTo(out castStr))
                                        {
                                            imageValue = castStr?.Value;
                                        }
                                    }

                                    if (!string.IsNullOrWhiteSpace(imageValue))
                                    {
                                        if (imageValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                            imageValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                        {
                                            imgParams["imageUrl"] = imageValue;
                                            hasImage = true;
                                        }
                                        else if (File.Exists(imageValue))
                                        {
                                            var bytes = File.ReadAllBytes(imageValue);
                                            imgParams["imageBase64"] = Convert.ToBase64String(bytes);
                                            imgParams["mimeType"] = GetMimeType(imageValue);
                                            hasImage = true;
                                        }
                                        else
                                        {
                                            imgParams["imageBase64"] = imageValue;
                                            hasImage = true;
                                        }
                                    }
                                }

                                if (!hasImage)
                                {
                                    outputs["Description"].Add(new GH_String(string.Empty));
                                    continue;
                                }

                                if (!string.IsNullOrWhiteSpace(this.prompt))
                                {
                                    imgParams["prompt"] = this.prompt;
                                }

                                try
                                {
                                    var toolResult = await this.parent.CallAIToolAsync("img2text", imgParams, token).ConfigureAwait(false);

                                    if (toolResult == null)
                                    {
                                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"img2text returned no result.");
                                        outputs["Description"].Add(new GH_String(string.Empty));
                                        continue;
                                    }

                                    // In batch mode, CallAIToolAsync returns a sentinel placeholder under "result".
                                    // Forward it so ReconstructOutputTree can replace it after the batch completes.
                                    var sentinel = toolResult["result"]?.ToString();
                                    if (BatchSentinel.Is(sentinel))
                                    {
                                        outputs["Description"].Add(new GH_String(sentinel));
                                        continue;
                                    }

                                    string description = toolResult["description"]?.ToString() ?? string.Empty;
                                    outputs["Description"].Add(new GH_String(description));
                                }
                                catch (Exception ex)
                                {
                                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Failed to describe image: {ex.Message}");
                                    outputs["Description"].Add(new GH_String(string.Empty));
                                }
                            }

                            return outputs;
                        },
                        this.processingOptions,
                        token).ConfigureAwait(false);

                    if (resultTrees.TryGetValue("Description", out var descTree))
                    {
                        this.resultDescriptions = descTree;
                    }

                    var batchSubmitted = await this.parent.TrySubmitBatchAsync("Description", resultTrees, token).ConfigureAwait(false);
                    if (batchSubmitted)
                    {
                        this.resultDescriptions = null;
                    }
                    else
                    {
                        // Non-batch: persist output and emit metrics via FinishResults
                        this.parent.FinishResults("Description", this.resultDescriptions ?? new GH_Structure<GH_String>());
                    }
                }
                catch (OperationCanceledException)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Operation was cancelled.");
                }
                catch (Exception ex)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error in image description: {ex.Message}");
                }
            }

            /// <summary>
            /// Sets the output from the async work.
            /// </summary>
            /// <param name="DA">Data access object.</param>
            /// <param name="errorMessage">Output error message, or null on success.</param>
            public override void SetOutput(IGH_DataAccess DA, out string errorMessage)
            {
                // Outputs and metrics are handled by FinishResults (non-batch) or
                // ProcessBatchResults → FinishResults (batch). RestorePersistentOutputs
                // replays them to the canvas on the next solve.
                errorMessage = null;
            }
        }
    }
}
