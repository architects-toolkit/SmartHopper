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
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Utilities;

namespace SmartHopper.Components.Output
{
    /// <summary>
    /// Generates Grasshopper component definitions in GhJSON format.
    /// </summary>
    public class AI2GhJsonComponent : AIOutputAdapterBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AI2GhJsonComponent"/> class.
        /// </summary>
        public AI2GhJsonComponent()
            : base("AI to GhJSON", "AI→GhJSON", "Generate Grasshopper components in GhJSON format", GH_Exposure.tertiary)
        {
        }

        /// <summary>
        /// Gets the component GUID.
        /// </summary>
        public override Guid ComponentGuid => new Guid("E83D8A20-12A4-42DE-AA73-D26E74976ADA");

        /// <summary>
        /// Gets the component icon.
        /// </summary>
        protected override Bitmap Icon => null;

        /// <summary>
        /// Gets the AI tools used by this component.
        /// </summary>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "gh_generate" };

        /// <summary>
        /// The gh_generate tool carries JsonOutput because the real tool emits JSON.
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
            return "You are a Grasshopper definition generator. Generate valid GhJSON format component definitions based on user requirements.";
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
                    ParamName = "GhJSON",
                    NickName = "G",
                    Description = "Generated GhJSON definition",
                    ParamType = typeof(Param_String),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
                        if (aiReturn?.Body?.GetLastAssistantJson() is JObject json)
                        {
                            // Use JsonFormatHelper to minify JSON
                            var minifiedJson = JsonFormatHelper.JsonToString(json);
                            return !string.IsNullOrEmpty(minifiedJson) ? new GH_String(minifiedJson) : null;
                        }
                        else if (aiReturn?.Body?.GetLastAssistantText() is string text && !string.IsNullOrWhiteSpace(text))
                        {
                            // Use JsonFormatHelper to validate and minify JSON
                            var minifiedJson = JsonFormatHelper.JsonToString(text, out var error);
                            if (string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(minifiedJson))
                            {
                                return new GH_String(minifiedJson);
                            }
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
            pManager.AddBooleanParameter("Place on Canvas", "P", "Automatically place generated components on canvas", GH_ParamAccess.item, false);
        }
    }
}
