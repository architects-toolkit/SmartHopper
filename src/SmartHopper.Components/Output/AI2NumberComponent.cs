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
    /// Extracts numerical values from AI input payloads.
    /// </summary>
    public class AI2NumberComponent : AIOutputAdapterBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AI2NumberComponent"/> class.
        /// </summary>
        // Fallback value captured from the optional Fallback input. Used by the
        // OutputMapping extractors when the AI response cannot be parsed.
        private double? _fallback;

        public AI2NumberComponent()
            : base("AI to Number", "AI→Num", "Extract numerical values from AI input", GH_Exposure.primary)
        {
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Fallback", "F", "OPTIONAL fallback value to use when the AI response cannot be parsed as a number. If not provided, the output will be null for unparsable responses.", GH_ParamAccess.item);
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <inheritdoc/>
        protected override void GatherAdditionalInputs(IGH_DataAccess DA, Dictionary<string, object> additionalInputs)
        {
            var fallbackItem = new GH_Number();
            this._fallback = DA.GetData("Fallback", ref fallbackItem) && fallbackItem != null
                ? fallbackItem.Value
                : (double?)null;
        }

        /// <summary>
        /// Gets the component GUID.
        /// </summary>
        public override Guid ComponentGuid => new Guid("894A032F-550E-45EC-BF70-655FE27A642C");

        /// <summary>
        /// Gets the component icon.
        /// </summary>
        protected override Bitmap Icon => Resources.aitonumeric;

        /// <summary>
        /// Gets the AI tools used by this component.
        /// </summary>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "text2text" };

        /// <summary>
        /// Gets the internal system prompt.
        /// </summary>
        protected override string GetInternalSystemPrompt()
        {
            return "You are a numerical extraction assistant. Extract and return only a single numerical value from the user input. Return only the number, no units or text.";
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
                    ParamName = "Number",
                    NickName = "N",
                    Description = "Extracted numerical value. Falls back to the Fallback input when the AI response cannot be parsed.",
                    ParamType = typeof(Param_Number),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
                        var text = aiReturn?.Body?.GetLastAssistantText();
                        var (value, _) = NumberResultResolver.Resolve(text, this._fallback);
                        return value.HasValue ? new GH_Number(value.Value) : null;
                    })
                },
                new OutputMapping
                {
                    ParamName = "Used Fallback",
                    NickName = "UF",
                    Description = "True when the AI response could not be parsed and the Fallback value was used (or null was emitted because no fallback was provided).",
                    ParamType = typeof(Param_Boolean),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
                        var text = aiReturn?.Body?.GetLastAssistantText();
                        var (_, usedFallback) = NumberResultResolver.Resolve(text, this._fallback);
                        return new GH_Boolean(usedFallback);
                    })
                }
            };
        }
    }
}
