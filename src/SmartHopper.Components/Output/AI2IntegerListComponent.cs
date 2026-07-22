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
    /// Generates a list of integers from AI input payloads.
    /// </summary>
    public class AI2IntegerListComponent : AIOutputAdapterBase
    {
        // Fallback list captured from the optional Fallback input.
        private List<int> _fallback;

        public AI2IntegerListComponent()
            : base("AI to Integer List", "AI→IntList", "Generate a list of integers from AI input. The model is asked to return a JSON array of integers; each item is expanded into the output branch.", GH_Exposure.primary)
        {
        }

        public override Guid ComponentGuid => new Guid("2B31E711-CE62-4EBF-8D67-A56569BBE9E8");

        protected override Bitmap Icon => null;

        protected override IReadOnlyList<string> UsingAiTools => new[] { "text2text" };

        protected override string GetInternalSystemPrompt()
        {
            return "You are a list generation assistant. Always respond with a single JSON array of integers and nothing else. Do not wrap the array in any object or markdown code block.";
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Fallback", "F", "OPTIONAL fallback list emitted when the AI response cannot be parsed as a JSON array of integers. If not provided, an empty list is emitted on parse failure.", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <inheritdoc/>
        protected override void GatherAdditionalInputs(IGH_DataAccess DA, Dictionary<string, object> additionalInputs)
        {
            var items = new List<int>();
            this._fallback = DA.GetDataList("Fallback", items) && items.Count > 0 ? items : null;
        }

        private (List<int> Items, bool UsedFallback) ResolveList(AIReturn aiReturn)
        {
            var text = aiReturn?.Body?.GetLastAssistantText();
            var (strings, _) = StringListResultResolver.Resolve(text);
            var parsed = strings
                .Select(s => IntegerResultResolver.Resolve(s, null).Value)
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .ToList();

            if (parsed.Count > 0)
            {
                return (parsed, false);
            }

            return (this._fallback?.ToList() ?? new List<int>(), true);
        }

        protected override IReadOnlyList<OutputMapping> GetOutputMappings()
        {
            return new[]
            {
                new OutputMapping
                {
                    ParamName = "Integer List",
                    NickName = "I",
                    Description = "Parsed list of integers (one branch entry per JSON array element). Items that fail to parse are skipped. Falls back to the Fallback input when no items are parseable.",
                    ParamType = typeof(Param_Integer),
                    Access = GH_ParamAccess.list,
                    Extractor = aiReturn =>
                    {
                        var (items, _) = this.ResolveList(aiReturn);
                        return items.Select(v => (IGH_Goo)new GH_Integer(v));
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
