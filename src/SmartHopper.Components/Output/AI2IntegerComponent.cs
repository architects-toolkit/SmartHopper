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
    /// Extracts integer values from AI input payloads.
    /// </summary>
    public class AI2IntegerComponent : AIOutputAdapterBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AI2IntegerComponent"/> class.
        /// </summary>
        // Fallback value captured from the optional Fallback input. Used by the
        // OutputMapping extractors when the AI response cannot be parsed.
        private int? _fallback;

        public AI2IntegerComponent()
            : base("AI to Integer", "AI→Int", "Extract integer values from AI input", GH_Exposure.primary)
        {
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Fallback", "F", "OPTIONAL fallback value to use when the AI response cannot be parsed as an integer. If not provided, the output will be null for unparsable responses.", GH_ParamAccess.item);
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <inheritdoc/>
        protected override void GatherAdditionalInputs(IGH_DataAccess DA, Dictionary<string, object> additionalInputs)
        {
            var fallbackItem = new GH_Integer();
            this._fallback = DA.GetData("Fallback", ref fallbackItem) && fallbackItem != null
                ? fallbackItem.Value
                : (int?)null;
        }

        /// <summary>
        /// Gets the component GUID.
        /// </summary>
        public override Guid ComponentGuid => new Guid("39CA90BF-9B4B-4FD3-B9FB-CD7DDB64069B");

        /// <summary>
        /// Gets the component icon.
        /// </summary>
        protected override Bitmap Icon => Resources.aitointeger;

        /// <summary>
        /// Gets the AI tools used by this component.
        /// </summary>
        protected override IReadOnlyList<string> UsingAiTools => new[] { "text2text" };

        /// <summary>
        /// Gets the internal system prompt.
        /// </summary>
        protected override string GetInternalSystemPrompt()
        {
            return "You are an integer extraction assistant. Extract and return only a single integer value from the user input. Round to the nearest integer if necessary. Return only the number, no units or text.";
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
                    ParamName = "Integer",
                    NickName = "I",
                    Description = "Extracted integer value. Falls back to the Fallback input when the AI response cannot be parsed.",
                    ParamType = typeof(Param_Integer),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
                        var text = aiReturn?.Body?.GetLastAssistantText();
                        var (value, _) = IntegerResultResolver.Resolve(text, this._fallback);
                        return value.HasValue ? new GH_Integer(value.Value) : null;
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
                        var (_, usedFallback) = IntegerResultResolver.Resolve(text, this._fallback);
                        return new GH_Boolean(usedFallback);
                    })
                }
            };
        }
    }
}
