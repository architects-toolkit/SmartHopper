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
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.Types;

namespace SmartHopper.Components.Test.OutputAdapters
{
    /// <summary>
    /// Regression test for AIOutputAdapterBase additional-input gathering. Verifies that a typed
    /// text tree (GH_Structure&lt;GH_String&gt;) is collected through VolatileData without the
    /// GetDataTree&lt;IGH_Goo&gt; type-mismatch exception, that aligned per-branch values reach
    /// PrepareInputs as GH_String, and that non-tree additional inputs are injected verbatim.
    /// No provider request is issued: PrepareInputs intentionally leaves "_MergedBody" unset so
    /// the built-in worker skips CallAIAsync. Wire "Input &gt;" with payloads and "Option" with a
    /// matching text tree, then toggle Run. Results are reported as a persistent runtime message.
    /// </summary>
    public class OutputAdapterTreeGatheringTestComponent : AIOutputAdapterBase
    {
        private const string ExpectedConstant = "verbatim-constant";
        private readonly List<string> _failures = new List<string>();
        private int _unitsPrepared;
        private string _capturedOption;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputAdapterTreeGatheringTestComponent"/> class.
        /// </summary>
        public OutputAdapterTreeGatheringTestComponent()
            : base(
                "TEST Output Adapter Gathering",
                "TEST-ADAPT-GATHER",
                "Validates that AIOutputAdapterBase gathers typed text trees without type mismatch, aligns them per branch, and injects non-tree values verbatim.",
                GH_Exposure.quarternary)
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("7C2E4A6B-1D8F-4C3A-9E5B-0F2D7A4C6B8E");

        /// <inheritdoc/>
        protected override Bitmap Icon => null;

        /// <inheritdoc/>
        protected override string GetInternalSystemPrompt()
        {
            return "Test system prompt.";
        }

        /// <inheritdoc/>
        protected override IReadOnlyList<OutputMapping> GetOutputMappings()
        {
            return new[]
            {
                new OutputMapping
                {
                    ParamName = "Result",
                    NickName = "R",
                    Description = "Unused; the worker never reaches a provider call.",
                    ParamType = typeof(Param_String),
                    Access = GH_ParamAccess.tree,
                    Extractor = OutputMapping.Single(_ => null),
                },
            };
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Option", "O", "Text option input aligned per branch. Wire a multi-branch text tree to verify alignment.", GH_ParamAccess.tree, "default-opt");
        }

        /// <inheritdoc/>
        protected override void GatherAdditionalInputs(IGH_DataAccess DA, Dictionary<string, object> additionalInputs)
        {
            this._failures.Clear();
            this._unitsPrepared = 0;
            this._capturedOption = null;

            base.GatherAdditionalInputs(DA, additionalInputs);

            // A non-tree value must reach PrepareInputs verbatim.
            additionalInputs["Constant"] = new GH_String(ExpectedConstant);

            // The typed text param must be gathered as a heterogeneous goo tree with paths preserved.
            var paramIndex = this.Params.IndexOfInputParam("Option");
            if (paramIndex < 0)
            {
                this._failures.Add("Input parameter 'Option' was not registered.");
                return;
            }

            var volatileData = this.Params.Input[paramIndex].VolatileData;
            if (!(additionalInputs.TryGetValue("Option", out var optionObj) && optionObj is GH_Structure<IGH_Goo> optionTree))
            {
                if (volatileData != null && volatileData.DataCount > 0)
                {
                    this._failures.Add("'Option' tree was not gathered as GH_Structure<IGH_Goo>.");
                }

                return;
            }

            if (volatileData == null || !optionTree.Paths.SequenceEqual(volatileData.Paths))
            {
                this._failures.Add("'Option' tree paths were not preserved during goo conversion.");
                return;
            }

            var gatheredValues = optionTree.AllData(true).OfType<GH_String>().Select(item => item.Value).ToList();
            var expectedValues = volatileData.AllData(true).OfType<GH_String>().Select(item => item.Value).ToList();
            if (!gatheredValues.SequenceEqual(expectedValues))
            {
                this._failures.Add("'Option' tree items were not preserved during goo conversion.");
            }
        }

        /// <inheritdoc/>
        protected override void PrepareInputs(Dictionary<string, object> inputs, ProcessingUnitContext context)
        {
            // Intentionally does NOT call base.PrepareInputs: leaving "_MergedBody" unset keeps the
            // built-in worker from issuing a provider request, so only the gather → align → inject
            // pipeline is exercised.
            this._unitsPrepared++;

            if (!(inputs.TryGetValue("Input >", out var payloadObj) && payloadObj is GH_Structure<GH_AIInputPayload>))
            {
                this._failures.Add("'Input >' was not injected as GH_Structure<GH_AIInputPayload>.");
            }

            if (!(inputs.TryGetValue("Constant", out var constantObj) && constantObj is GH_String constantStr && constantStr.Value == ExpectedConstant))
            {
                this._failures.Add("Non-tree additional input 'Constant' was not injected verbatim.");
            }

            this._capturedOption = inputs.TryGetValue("Option", out var optionObj) && optionObj is GH_String optionStr
                ? optionStr.Value
                : null;

            if (this._capturedOption == null)
            {
                this._failures.Add("Tree input 'Option' was not aligned into the per-unit inputs.");
            }
        }

        /// <inheritdoc/>
        protected override void OnWorkerCompleted()
        {
            base.OnWorkerCompleted();

            if (this._failures.Count > 0)
            {
                this.SetPersistentRuntimeMessage(
                    "adapter_gather_test",
                    GH_RuntimeMessageLevel.Error,
                    $"Gathering test failed: {string.Join("; ", this._failures.Distinct())}",
                    false);
                return;
            }

            if (this._unitsPrepared == 0)
            {
                this.SetPersistentRuntimeMessage(
                    "adapter_gather_test",
                    GH_RuntimeMessageLevel.Remark,
                    "Gather succeeded; no units ran. Wire 'Input >' with payloads to exercise per-unit assertions.",
                    false);
                return;
            }

            this.SetPersistentRuntimeMessage(
                "adapter_gather_test",
                GH_RuntimeMessageLevel.Remark,
                $"Gathering test passed: {this._unitsPrepared} unit(s) processed; Option='{this._capturedOption}'.",
                false);
        }
    }
}
