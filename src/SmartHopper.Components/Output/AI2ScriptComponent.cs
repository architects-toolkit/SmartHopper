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
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.Components.Output
{
    /// <summary>
    /// Generates Grasshopper Python/C# script code from AI input payloads.
    /// </summary>
    public class AI2ScriptComponent : AIOutputAdapterBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AI2ScriptComponent"/> class.
        /// </summary>
        public AI2ScriptComponent()
            : base("AI to Script", "AI→Script", "Generate GH script code from AI input", GH_Exposure.tertiary)
        {
        }

        /// <summary>
        /// Gets the component GUID.
        /// </summary>
        public override Guid ComponentGuid => new Guid("EB5FA729-E165-4701-BF42-3BFFFFCB7C33");

        /// <summary>
        /// Gets the component icon.
        /// </summary>
        protected override Bitmap Icon => Resources.aitoscript;

        /// <summary>
        /// Gets the AI tools used by this component.
        /// </summary>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "script_generate" };

        /// <summary>
        /// The script_generate tool carries JsonOutput because the real tool emits JSON.
        /// This adapter parses free-form text, so it only needs text-in/text-out capability.
        /// </summary>
        protected override AICapability RequiredCapability
        {
            get
            {
                var capability = base.RequiredCapability;
                return capability & ~AICapability.JsonOutput;
            }

            set => base.RequiredCapability = value;
        }

        /// <summary>
        /// Gets the internal system prompt.
        /// </summary>
        protected override string GetInternalSystemPrompt()
        {
            return "You are a Grasshopper script generation assistant. Generate valid Python or C# script code for Grasshopper based on user requirements.";
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
                    ParamName = "Script",
                    NickName = "S",
                    Description = "Generated script code",
                    ParamType = typeof(Param_String),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
                        if (aiReturn?.Body?.GetLastAssistantText() is string text)
                        {
                            return new GH_String(text);
                        }

                        return null;
                    })
                }
            };
        }

        /// <summary>
        /// Registers additional input parameters.
        /// </summary>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Language", "L", "Script language (python or csharp)", GH_ParamAccess.tree, "python");
        }

        /// <summary>
        /// Gathers additional input parameters (Language tree).
        /// </summary>
        protected override void GatherAdditionalInputs(IGH_DataAccess DA, Dictionary<string, object> additionalInputs)
        {
            base.GatherAdditionalInputs(DA, additionalInputs);

            try
            {
                var languageTree = new GH_Structure<IGH_Goo>();
                if (DA.GetDataTree(1, out languageTree) && languageTree != null && languageTree.DataCount > 0)
                {
                    additionalInputs["Language"] = languageTree;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI2ScriptComponent] Error gathering Language input: {ex.Message}");
            }
        }

        /// <summary>
        /// Overrides PrepareInputs to inject Language into the system prompt.
        /// </summary>
        protected override void PrepareInputs(Dictionary<string, object> inputs, ProcessingUnitContext context)
        {
            base.PrepareInputs(inputs, context);

            // Read Language from sliced inputs (per-unit)
            var language = "python";

            if (inputs.TryGetValue("Language", out var languageObj) && languageObj is GH_String languageStr && !string.IsNullOrWhiteSpace(languageStr.Value))
            {
                language = languageStr.Value;
            }

            // Store language in inputs for use by CallAIAsync
            inputs["_Language"] = language;
        }
    }
}
