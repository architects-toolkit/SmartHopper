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
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Grasshopper.Utils.Parsing;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Components.Output
{
    /// <summary>
    /// Generates a list of strings from AI input payloads. Each branch produces one
    /// AI call and the resulting JSON array is expanded into list items in the same branch.
    /// </summary>
    public class AI2TextListComponent : AIOutputAdapterBase
    {
        // Fallback list captured from the optional Fallback input. Emitted when the AI
        // response cannot be parsed into a non-empty list.
        private List<string> _fallback;

        public AI2TextListComponent()
            : base("AI to Text List", "AI→TextList", "Generate a list of strings from AI input. The model is asked to return a JSON array of strings; each item is expanded into the output branch.", GH_Exposure.primary)
        {
        }

        public override Guid ComponentGuid => new Guid("C4286A7D-3BCB-4785-84E9-2FB2164519C0");

        protected override Bitmap Icon => null;

        protected override IReadOnlyList<string> UsingAiTools => new[] { "text2text" };

        protected override string GetInternalSystemPrompt()
        {
            return "You are a list generation assistant. Always respond with a single JSON array of strings and nothing else. Do not wrap the array in any object or markdown code block.";
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Fallback", "F", "OPTIONAL fallback list emitted when the AI response cannot be parsed as a JSON array of strings. If not provided, an empty list is emitted on parse failure.", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <inheritdoc/>
        protected override void GatherAdditionalInputs(IGH_DataAccess DA, Dictionary<string, object> additionalInputs)
        {
            var items = new List<string>();
            this._fallback = DA.GetDataList("Fallback", items) && items.Count > 0 ? items : null;
        }

        /// <summary>
        /// Resolves the list of strings from the AI response, applying the fallback when the
        /// response cannot be parsed into a non-empty list.
        /// </summary>
        private (List<string> Items, bool UsedFallback) ResolveList(AIReturn aiReturn)
        {
            var text = aiReturn?.Body?.GetLastAssistantText();
            var (parsed, _) = StringListResultResolver.Resolve(text);
            if (parsed != null && parsed.Count > 0)
            {
                return (parsed, false);
            }

            return (this._fallback?.ToList() ?? new List<string>(), true);
        }

        protected override IReadOnlyList<OutputMapping> GetOutputMappings()
        {
            return new[]
            {
                new OutputMapping
                {
                    ParamName = "Text List",
                    NickName = "T",
                    Description = "Parsed list of strings (one branch entry per JSON array element). Falls back to the Fallback input when the response cannot be parsed.",
                    ParamType = typeof(Param_String),
                    Access = GH_ParamAccess.list,
                    Extractor = aiReturn =>
                    {
                        var (items, _) = this.ResolveList(aiReturn);
                        return items.Select(s => (IGH_Goo)new GH_String(s));
                    },
                },
                new OutputMapping
                {
                    ParamName = "Used Fallback",
                    NickName = "UF",
                    Description = "True when the AI response could not be parsed as a list and the Fallback value was used (or an empty list was emitted because no fallback was provided).",
                    ParamType = typeof(Param_Boolean),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(aiReturn =>
                    {
                        var (_, usedFallback) = this.ResolveList(aiReturn);
                        return new GH_Boolean(usedFallback);
                    }),
                },
            };
        }
    }
}
