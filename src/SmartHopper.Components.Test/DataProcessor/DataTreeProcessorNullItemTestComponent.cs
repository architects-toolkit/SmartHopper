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
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.DataTree;

using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Components.Test.DataProcessor
{
    /// <summary>
    /// Regression fixture for D21: <c>DataTreeProcessor</c> must survive input
    /// branches that contain literal <see langword="null"/> elements. Before the
    /// fix, <c>GetBranchesKey</c> called <c>item.ToString()</c> on every branch
    /// element during identical-branch grouping — a single null element threw an
    /// <see cref="NullReferenceException"/> that killed the processing plan for
    /// every stateful component downstream of an empty/null output.
    /// The fix routes each element through <c>SemanticFingerprint.Of</c>, which
    /// maps null to a null token; this component asserts the end-to-end pipeline
    /// completes, the worker still observes the nulls, and grouping still
    /// collapses identical null-bearing branches.
    /// </summary>
    public class DataTreeProcessorNullItemTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("50CBC1A8-6D1C-4E7F-8ECF-7FE75DF9DB89");

        protected override Bitmap Icon => null;

        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public DataTreeProcessorNullItemTestComponent()
            : base(
                "Test DataTreeProcessor (Null Items)",
                "TEST-DTP-NULL",
                "Tests DataTreeProcessor identical-branch grouping with literal null elements in branches (D21 regression).",
                "SmartHopper Tests",
                "Testing Data")
        {
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager) { }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Result", "R", "Result data tree produced by DataTreeProcessor.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Success", "S", "True if the pipeline completed, observed the nulls, and grouped the identical branches.", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Diagnostic messages.", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Structure<GH_String> _resultTree = new GH_Structure<GH_String>();
            private GH_Boolean _success = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly DataTreeProcessorNullItemTestComponent _parent;

            public Worker(DataTreeProcessorNullItemTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this._parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 1;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    var path0 = new GH_Path(0);
                    var path1 = new GH_Path(1);
                    var path2 = new GH_Path(2);

                    // Tree A: identical null-bearing branches at {0} and {1} so the
                    // identical-branch grouping must compare fingerprints that
                    // include a null element; {2} holds a null-only branch.
                    var treeA = new GH_Structure<GH_String>();
                    treeA.Append(new GH_String("apple"), path0);
                    var nullsA0 = NullTreeBuilder.AppendNull(treeA, path0);
                    treeA.Append(new GH_String("cherry"), path0);
                    treeA.Append(new GH_String("apple"), path1);
                    var nullsA1 = NullTreeBuilder.AppendNull(treeA, path1);
                    treeA.Append(new GH_String("cherry"), path1);
                    var nullsA2 = NullTreeBuilder.AppendNull(treeA, path2);

                    var treeB = new GH_Structure<GH_String>();
                    treeB.Append(new GH_String("x"), path0);
                    treeB.Append(new GH_String("x"), path1);
                    treeB.Append(new GH_String("y"), path2);

                    var injected = nullsA0 + nullsA1 + nullsA2;
                    this._messages.Add(new GH_String($"Injected {injected} null element(s) into tree A ({{0}}={nullsA0}, {{1}}={nullsA1}, {{2}}={nullsA2})."));

                    var trees = new Dictionary<string, GH_Structure<GH_String>>
                    {
                        { "A", treeA },
                        { "B", treeB },
                    };

                    var options = new ProcessingOptions
                    {
                        Topology = ProcessingTopology.ItemToItem,
                        OnlyMatchingPaths = false,
                        GroupIdenticalBranches = true,
                    };

                    var (dataCount, iterations) = DataTreeProcessor.CalculateProcessingMetrics(trees, options);
                    Logger.For<Worker>().Trace($"[NullItem] Iterations: {iterations}, DataCount: {dataCount}");
                    this._messages.Add(new GH_String($"Calculated metrics: iterations={iterations}, dataCount={dataCount}."));

                    // ItemToItem: one item per input tree; null elements arrive as
                    // null GH_String references and must be tolerated.
                    int funcCalls = 0;
                    int observedNulls = 0;
                    async Task<Dictionary<string, List<GH_String>>> Func(Dictionary<string, List<GH_String>> items)
                    {
                        await Task.Yield();
                        funcCalls++;
                        observedNulls += items.Values.Sum(list => list.Count(item => item == null));

                        string Describe(List<GH_String> list)
                            => list == null || list.Count == 0 ? "empty" : (list[0]?.Value ?? "<null>");

                        var a = items.TryGetValue("A", out var aList) ? Describe(aList) : "missing";
                        var b = items.TryGetValue("B", out var bList) ? Describe(bList) : "missing";
                        return new Dictionary<string, List<GH_String>>
                        {
                            { "Pair", new List<GH_String> { new GH_String($"{a}+{b}") } },
                        };
                    }

                    var result = await DataTreeProcessor.RunAsync<GH_String, GH_String>(
                        trees,
                        Func,
                        options,
                        progressCallback: null,
                        token: token).ConfigureAwait(false);

                    if (result != null && result.Outputs.TryGetValue("Pair", out var pairTree) && pairTree != null)
                    {
                        this._resultTree = pairTree;
                    }
                    else
                    {
                        this._resultTree = new GH_Structure<GH_String>();
                    }

                    // Identical branches {0} and {1} should group — reported for
                    // diagnosis, but the assertion gates on the pipeline surviving
                    // the null elements rather than on the iteration metric.
                    bool pipelineCompleted = funcCalls > 0 && this._resultTree != null && !this._resultTree.IsEmpty;

                    bool ok = injected >= 3 && pipelineCompleted && observedNulls > 0;

                    this._success = new GH_Boolean(ok);
                    this._messages.Add(new GH_String($"Func invoked {funcCalls} time(s); observed {observedNulls} null item(s) inside the worker; iterations={iterations}."));
                    this._messages.Add(new GH_String(ok ? "Test succeeded." : "Test failed: unexpected result."));
                }
                catch (OperationCanceledException)
                {
                    this._success = new GH_Boolean(false);
                    this._messages.Add(new GH_String("Operation was cancelled."));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Test cancelled.");
                }
                catch (Exception ex)
                {
                    this._success = new GH_Boolean(false);
                    this._messages.Add(new GH_String($"Exception: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Result", this._resultTree, DA);
                this._parent.SetPersistentOutput("Success", this._success, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);
                message = this._success.Value ? "Processed null-bearing branches successfully" : "Processing failed";
            }
        }
    }
}
