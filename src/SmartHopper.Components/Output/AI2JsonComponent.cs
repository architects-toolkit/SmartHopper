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
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Grasshopper.Converters;
using SmartHopper.Core.Grasshopper.Utils.Parsing;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Components.Output
{
    /// <summary>
    /// Generates structured JSON data from AI input payloads.
    /// </summary>
    public class AI2JsonComponent : AIOutputAdapterBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AI2JsonComponent"/> class.
        /// </summary>
        // Fallback JSON token captured from the optional Fallback input. Used by the
        // OutputMapping extractors when the AI response cannot be parsed as JSON.
        private JToken _fallback;

        public AI2JsonComponent()
            : base("AI to JSON", "AI→JSON", "Generate structured JSON data from AI input", GH_Exposure.secondary)
        {
        }

        /// <summary>
        /// Gets the component GUID.
        /// </summary>
        public override Guid ComponentGuid => new Guid("FE980BBB-83CF-46CA-B8D2-36B5EAD7F6B4");

        /// <summary>
        /// Gets the component icon.
        /// </summary>
        protected override Bitmap Icon => Resources.aitojson;

        /// <summary>
        /// Gets the AI tools used by this component.
        /// </summary>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "text2json" };

        /// <summary>
        /// The text2json tool advertises JsonOutput because the real tool emits structured JSON.
        /// This adapter can parse JSON from free-form text, so JsonOutput is only required when
        /// the caller supplies a Schema (applied to the body in <see cref="PrepareInputs"/>).
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
            return "You are a JSON generation assistant. Generate valid JSON output based on the user input. Return only valid JSON, no markdown formatting or additional text.";
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
                    ParamName = "JSON",
                    NickName = "J",
                    Description = "Generated JSON data (minified). Falls back to the Fallback input when the AI response cannot be parsed as JSON.",
                    ParamType = typeof(Param_String),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
                        var text = aiReturn?.Body?.GetLastAssistantText();
                        var outcome = JsonResultResolver.Resolve(text, this._fallback);
                        return outcome.Value != null
                            ? new GH_String(outcome.Value.ToString(Newtonsoft.Json.Formatting.None))
                            : null;
                    })
                },
                new OutputMapping
                {
                    ParamName = "Used Fallback",
                    NickName = "UF",
                    Description = "True when the AI response could not be parsed as JSON and the Fallback value was used (or null was emitted because no fallback was provided).",
                    ParamType = typeof(Param_Boolean),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
                        var text = aiReturn?.Body?.GetLastAssistantText();
                        var outcome = JsonResultResolver.Resolve(text, this._fallback);
                        return new GH_Boolean(!outcome.Success);
                    })
                }
            };
        }

        /// <summary>
        /// Registers additional input parameters.
        /// </summary>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Schema", "S", "Optional JSON schema for structured output", GH_ParamAccess.tree);
            pManager.AddTextParameter("Fallback", "F", "OPTIONAL fallback JSON (object or array) to emit when the AI response cannot be parsed. If not provided, the output will be null for unparsable responses.", GH_ParamAccess.item);
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <summary>
        /// Gathers additional input parameters (Schema tree).
        /// </summary>
        protected override void GatherAdditionalInputs(IGH_DataAccess DA, Dictionary<string, object> additionalInputs)
        {
            base.GatherAdditionalInputs(DA, additionalInputs);

            try
            {
                // The Schema parameter is a Param_Text, so its underlying tree is GH_Structure<GH_String>.
                // Read it with the concrete type and then convert to GH_Structure<IGH_Goo> so the
                // output-adapter pipeline can slice and broadcast it like any other additional input.
                var schemaTree = new GH_Structure<GH_String>();
                if (DA.GetDataTree(1, out schemaTree) && schemaTree != null && schemaTree.DataCount > 0)
                {
                    additionalInputs["Schema"] = GHStructureConverter.ConvertToGooTree(schemaTree);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI2JsonComponent] Error gathering Schema input: {ex.Message}");
            }

            // Capture optional Fallback JSON for use by the extractors when parsing fails.
            this._fallback = null;
            var fallbackItem = new GH_String();
            if (DA.GetData("Fallback", ref fallbackItem) && fallbackItem != null && !string.IsNullOrWhiteSpace(fallbackItem.Value))
            {
                try
                {
                    this._fallback = JToken.Parse(fallbackItem.Value);
                }
                catch (Newtonsoft.Json.JsonException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AI2JsonComponent] Invalid Fallback JSON ignored: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Overrides PrepareInputs to inject Schema into the body if provided.
        /// </summary>
        protected override void PrepareInputs(Dictionary<string, object> inputs, ProcessingUnitContext context)
        {
            base.PrepareInputs(inputs, context);

            // Read Schema from sliced inputs (per-unit)
            if (inputs.TryGetValue("Schema", out var schemaObj) && schemaObj is GH_String schemaStr && !string.IsNullOrWhiteSpace(schemaStr.Value))
            {
                var schema = schemaStr.Value;

                // Apply JSON schema to the merged body if present
                if (inputs.TryGetValue("_MergedBody", out var bodyObj) && bodyObj is AIBody body)
                {
                    try
                    {
                        // Rebuild the body with the schema using the builder
                        var builder = AIBodyBuilder.FromImmutable(body);
                        builder.WithJsonOutputSchema(schema);
                        inputs["_MergedBody"] = builder.Build();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AI2JsonComponent] Error applying schema: {ex.Message}");
                    }
                }
            }
        }
    }
}
