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
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Core.DataTree;

namespace SmartHopper.Components.Test.DataProcessor
{
    /// <summary>
    /// Test component: two inputs, first input one item, second input three items, equal paths.
    /// </summary>
    public class DataTreeProcessorEqualPathsFirstOneSecondThreeTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("0C6B2C9E-2D68-45AC-A2D8-7B2E5F97F9C3");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        public DataTreeProcessorEqualPathsFirstOneSecondThreeTestComponent()
            : base("Test DataTreeProcessor (Equal Paths, 1 + 3 items)", "TEST-DTP-EQ-1-3",
                  "Tests DataTreeProcessor with equal paths where A has 1 item and B has 3 items.",
                  "SmartHopper", "Testing Data")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager) { }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Result", "R", "Result data tree produced by DataTreeProcessor.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Success", "S", "True if the result matches the expected value.", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Diagnostic messages.", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Structure<GH_Integer> _resultTree = new GH_Structure<GH_Integer>();
            private GH_Boolean _success = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly DataTreeProcessorEqualPathsFirstOneSecondThreeTestComponent _parent;

            public Worker(DataTreeProcessorEqualPathsFirstOneSecondThreeTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                _parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 1;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    var path = new GH_Path(0);

                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(2), path); // 1 item

                    var treeB = new GH_Structure<GH_Integer>();
                    treeB.Append(new GH_Integer(1), path);
                    treeB.Append(new GH_Integer(2), path);
                    treeB.Append(new GH_Integer(3), path);

                    var trees = new Dictionary<string, GH_Structure<GH_Integer>>
                    {
                        { "A", treeA },
                        { "B", treeB },
                    };

                    var options = new ProcessingOptions
                    {
                        Topology = ProcessingTopology.BranchToBranch,
                        OnlyMatchingPaths = true,
                        GroupIdenticalBranches = false,
                    };

                    var (dataCount, iterations) = DataTreeProcessor.CalculateProcessingMetrics(trees, options);
                    Debug.WriteLine($"[EqualPaths1+3] Iterations: {iterations}, DataCount: {dataCount}");

                    async Task<Dictionary<string, List<GH_Integer>>> Func(Dictionary<string, List<GH_Integer>> branches)
                    {
                        await Task.Yield();
                        var aList = branches.ContainsKey("A") ? branches["A"] : null;
                        var bList = branches.ContainsKey("B") ? branches["B"] : null;
                        if (aList == null || bList == null || aList.Count == 0 || bList.Count == 0)
                            return new Dictionary<string, List<GH_Integer>> { { "Sum", new List<GH_Integer>() } };

                        var normalized = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_Integer>> { aList, bList });
                        var aNorm = normalized[0];
                        var bNorm = normalized[1];
                        var sums = new List<GH_Integer>();
                        for (int i = 0; i < Math.Max(aList.Count, bList.Count); i++)
                        {
                            int ai = aNorm[i]?.Value ?? 0;
                            int bi = bNorm[i]?.Value ?? 0;
                            sums.Add(new GH_Integer(ai + bi));
                        }

                        return new Dictionary<string, List<GH_Integer>> { { "Sum", sums } };
                    }

                    var result = await DataTreeProcessor.RunAsync<GH_Integer, GH_Integer>(
                        trees,
                        Func,
                        options,
                        progressCallback: null,
                        token: token).ConfigureAwait(false);

                    if (result != null && result.TryGetValue("Sum", out var sumTree) && sumTree != null)
                        _resultTree = sumTree;
                    else
                        _resultTree = new GH_Structure<GH_Integer>();

                    bool ok =
                        _resultTree != null &&
                        _resultTree.PathCount == 1 &&
                        _resultTree.get_Branch(path) != null &&
                        _resultTree.get_Branch(path).Count == 3 &&
                        _resultTree.get_Branch(path)[0] is GH_Integer gi0 && gi0.Value == (2 + 1) &&
                        _resultTree.get_Branch(path)[1] is GH_Integer gi1 && gi1.Value == (2 + 2) &&
                        _resultTree.get_Branch(path)[2] is GH_Integer gi2 && gi2.Value == (2 + 3);

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"Equal paths {path}. A=[2], B=[1,2,3]. Expected pairwise sums [3,4,5]."));
                    _messages.Add(new GH_String(ok ? "Test succeeded." : "Test failed: unexpected result."));
                }
                catch (OperationCanceledException)
                {
                    _success = new GH_Boolean(false);
                    _messages.Add(new GH_String("Operation was cancelled."));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Test cancelled.");
                }
                catch (Exception ex)
                {
                    _success = new GH_Boolean(false);
                    _messages.Add(new GH_String($"Exception: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                _parent.SetPersistentOutput("Result", _resultTree, DA);
                _parent.SetPersistentOutput("Success", _success, DA);
                _parent.SetPersistentOutput("Messages", _messages, DA);
                message = _success.Value ? "Processed equal-path trees (1+3) successfully" : "Processing failed";
            }
        }
    }
}
