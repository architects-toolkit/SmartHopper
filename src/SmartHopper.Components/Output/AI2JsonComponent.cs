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
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.Utilities;

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
        protected override Bitmap Icon => null;

        /// <summary>
        /// Gets the AI tools used by this component.
        /// </summary>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "text_generate" };

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
                    Description = "Generated JSON data",
                    ParamType = typeof(Param_String),
                    Access = GH_ParamAccess.tree,
                    Extractor = (aiReturn) =>
                    {
                        if (aiReturn?.Body?.GetLastAssistantText() is string text && !string.IsNullOrWhiteSpace(text))
                        {
                            // Use JsonFormatHelper to validate and minify JSON
                            var minifiedJson = JsonFormatHelper.JsonToString(text, out var error);
                            if (string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(minifiedJson))
                            {
                                return new GH_String(minifiedJson);
                            }
                        }

                        return null;
                    }
                }
            };
        }

        /// <summary>
        /// Registers additional input parameters.
        /// </summary>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Schema", "S", "Optional JSON schema for structured output", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Gathers additional input parameters (Schema tree).
        /// </summary>
        protected override void GatherAdditionalInputs(IGH_DataAccess DA, Dictionary<string, object> additionalInputs)
        {
            base.GatherAdditionalInputs(DA, additionalInputs);

            try
            {
                var schemaTree = new GH_Structure<IGH_Goo>();
                if (DA.GetDataTree(2, out schemaTree) && schemaTree != null && schemaTree.DataCount > 0)
                {
                    additionalInputs["Schema"] = schemaTree;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AI2JsonComponent] Error gathering Schema input: {ex.Message}");
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
