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
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Types;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.Components.Output
{
    /// <summary>
    /// Generates images from AI input payloads.
    /// </summary>
    public class AI2ImgComponent : AIOutputAdapterBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AI2ImgComponent"/> class.
        /// </summary>
        public AI2ImgComponent()
            : base("AI to Image", "AI→Img", "Generate images from AI input payloads", GH_Exposure.secondary)
        {
        }

        /// <summary>
        /// Gets the component GUID.
        /// </summary>
        public override Guid ComponentGuid => new Guid("23171E58-D212-4251-8331-788E82A376BC");

        /// <summary>
        /// Gets the component icon.
        /// </summary>
        protected override Bitmap Icon => null;

        /// <summary>
        /// Gets the AI tools used by this component.
        /// </summary>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "text2img" };

        /// <summary>
        /// Gets the internal system prompt.
        /// </summary>
        protected override string GetInternalSystemPrompt()
        {
            return "You are an image generation assistant. Generate detailed image descriptions based on user input.";
        }

        /// <summary>
        /// Gets the output mappings.
        /// </summary>
        protected override IReadOnlyList<OutputMapping> GetOutputMappings()
        {
            return new[]
            {
                new OutputMapping
                {
                    ParamName = "Image",
                    NickName = "I",
                    Description = "Generated image",
                    ParamType = typeof(Param_GenericObject),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
                        if (aiReturn?.Body?.GetLastAssistantImage() is var imgInteraction && imgInteraction != null)
                        {
                            var versatile = VersatileImage.FromString(imgInteraction.ImageUrl.ToString());
                            return new GH_VersatileImage(versatile);
                        }

                        return null;
                    }),
                },
            };
        }

        /// <summary>
        /// Registers additional input parameters.
        /// </summary>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Size", "Sz", "Image size (e.g., 1024x1024)", GH_ParamAccess.tree, "1024x1024");
            pManager.AddTextParameter("Quality", "Q", "Image quality (standard or hd)", GH_ParamAccess.tree, "standard");
            pManager.AddTextParameter("Style", "St", "Image style (natural or vivid)", GH_ParamAccess.tree, "natural");
        }

        /// <summary>
        /// Gathers additional input parameters (Size, Quality, Style trees).
        /// </summary>
        protected override void GatherAdditionalInputs(IGH_DataAccess DA, Dictionary<string, object> additionalInputs)
        {
            base.GatherAdditionalInputs(DA, additionalInputs);

            try
            {
                var sizeTree = new GH_Structure<IGH_Goo>();
                if (DA.GetDataTree(2, out sizeTree) && sizeTree != null && sizeTree.DataCount > 0)
                {
                    additionalInputs["Size"] = sizeTree;
                }

                var qualityTree = new GH_Structure<IGH_Goo>();
                if (DA.GetDataTree(3, out qualityTree) && qualityTree != null && qualityTree.DataCount > 0)
                {
                    additionalInputs["Quality"] = qualityTree;
                }

                var styleTree = new GH_Structure<IGH_Goo>();
                if (DA.GetDataTree(4, out styleTree) && styleTree != null && styleTree.DataCount > 0)
                {
                    additionalInputs["Style"] = styleTree;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI2ImgComponent] Error gathering Size/Quality/Style inputs: {ex.Message}");
            }
        }

        /// <summary>
        /// Overrides PrepareInputs to inject Size, Quality, and Style into tool parameters.
        /// </summary>
        protected override void PrepareInputs(Dictionary<string, object> inputs, ProcessingUnitContext context)
        {
            base.PrepareInputs(inputs, context);

            // Read Size, Quality, and Style from sliced inputs (per-unit)
            var size = "1024x1024";
            var quality = "standard";
            var style = "natural";

            if (inputs.TryGetValue("Size", out var sizeObj) && sizeObj is GH_String sizeStr && !string.IsNullOrWhiteSpace(sizeStr.Value))
            {
                size = sizeStr.Value;
            }

            if (inputs.TryGetValue("Quality", out var qualityObj) && qualityObj is GH_String qualityStr && !string.IsNullOrWhiteSpace(qualityStr.Value))
            {
                quality = qualityStr.Value;
            }

            if (inputs.TryGetValue("Style", out var styleObj) && styleObj is GH_String styleStr && !string.IsNullOrWhiteSpace(styleStr.Value))
            {
                style = styleStr.Value;
            }

            // Store in inputs for use by CallAIAsync
            inputs["_Size"] = size;
            inputs["_Quality"] = quality;
            inputs["_Style"] = style;
        }
    }
}
