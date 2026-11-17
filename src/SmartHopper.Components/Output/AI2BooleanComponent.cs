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
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Grasshopper.Utils.Parsing;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;

namespace SmartHopper.Components.Output
{
    /// <summary>
    /// Classifies input as true or false using AI.
    /// </summary>
    public class AI2BooleanComponent : AIOutputAdapterBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AI2BooleanComponent"/> class.
        /// </summary>
        // Fallback value captured from the optional Fallback input. Used by the
        // OutputMapping extractors when the AI response cannot be parsed.
        private bool? _fallback;

        public AI2BooleanComponent()
            : base("AI to Boolean", "AI2Bool", "Call the AI provider on the connected input(s) and return a True/False boolean.", GH_Exposure.primary)
        {
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Fallback", "F", "Optional fallback boolean returned when the AI response cannot be parsed as True or False (leave disconnected to output null on parse failure).", GH_ParamAccess.item);
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <inheritdoc/>
        protected override void GatherAdditionalInputs(IGH_DataAccess DA, Dictionary<string, object> additionalInputs)
        {
            base.GatherAdditionalInputs(DA, additionalInputs);

            var fallbackItem = new GH_Boolean();
            this._fallback = DA.GetData("Fallback", ref fallbackItem) && fallbackItem != null
                ? fallbackItem.Value
                : (bool?)null;
        }

        /// <summary>
        /// Gets the component GUID.
        /// </summary>
        public override Guid ComponentGuid => new Guid("2742C0E6-0195-4A6E-9F89-1B9CBA4A3D87");

        /// <summary>
        /// Gets the component icon.
        /// </summary>
        protected override Bitmap Icon => Resources.AI2Boolean;

        /// <summary>
        /// Gets the AI tools used by this component.
        /// </summary>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "text2text" };

        /// <summary>
        /// Gets the internal system prompt.
        /// </summary>
        protected override string GetInternalSystemPrompt()
        {
            return "You are a classification assistant. Respond with only 'true' or 'false' based on the user input. No other text.";
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
                    ParamName = "Boolean",
                    NickName = "B",
                    Description = "Extracted boolean value (or the Fallback value when unparsable).",
                    ParamType = typeof(Param_Boolean),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
                        var text = aiReturn?.Body?.GetLastAssistantText();
                        var (value, _) = BooleanResultResolver.Resolve(text, this._fallback);
                        return value.HasValue ? new GH_Boolean(value.Value) : null;
                    })
                },
                new OutputMapping
                {
                    ParamName = "Used Fallback",
                    NickName = "UF",
                    Description = "True when the AI response could not be parsed as True/False and the fallback behavior was used.",
                    ParamType = typeof(Param_Boolean),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
                        var text = aiReturn?.Body?.GetLastAssistantText();
                        var (_, usedFallback) = BooleanResultResolver.Resolve(text, this._fallback);
                        return new GH_Boolean(usedFallback);
                    })
                }
            };
        }
    }
}
