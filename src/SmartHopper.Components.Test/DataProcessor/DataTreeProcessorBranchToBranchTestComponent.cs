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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
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
    /// Test component for BranchToBranch topology: processes entire branches as complete lists,
    /// where each branch is processed independently and maintains its branch structure.
    /// This is the list-level processing mode used by AIListEvaluate and AIListFilter.
    /// </summary>
    public class DataTreeProcessorBranchToBranchTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("4FBE8A03-5A39-4C99-B190-F95468A0D3AC");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public DataTreeProcessorBranchToBranchTestComponent()
            : base("Test DataTreeProcessor (BranchToBranch)", "TEST-DTP-B2B",
                  "Tests DataTreeProcessor with BranchToBranch topology where each branch is processed as a complete list.",
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
            private readonly DataTreeProcessorBranchToBranchTestComponent _parent;
            private int _functionCallCount = 0;

            public Worker(DataTreeProcessorBranchToBranchTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var path0 = new GH_Path(0);
                    var path1 = new GH_Path(1);

                    // Create a tree with two branches: {0}=[1,2,3], {1}=[4,5]
                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(1), path0);
                    treeA.Append(new GH_Integer(2), path0);
                    treeA.Append(new GH_Integer(3), path0);
                    treeA.Append(new GH_Integer(4), path1);
                    treeA.Append(new GH_Integer(5), path1);

                    var trees = new Dictionary<string, GH_Structure<GH_Integer>>
                    {
                        { "A", treeA },
                    };

                    var options = new ProcessingOptions
                    {
                        Topology = ProcessingTopology.BranchToBranch,
                        OnlyMatchingPaths = false,
                        GroupIdenticalBranches = false,
                    };

                    var (dataCount, iterations) = DataTreeProcessor.CalculateProcessingMetrics(trees, options);
                    Debug.WriteLine($"[BranchToBranch] Iterations: {iterations}, DataCount: {dataCount}");

                    // BranchToBranch: function receives complete branch lists, one branch at a time
                    async Task<Dictionary<string, List<GH_Integer>>> Func(Dictionary<string, List<GH_Integer>> branches)
                    {
                        await Task.Yield();
                        _functionCallCount++;

                        var aList = branches.ContainsKey("A") ? branches["A"] : null;
                        if (aList == null || aList.Count == 0)
                            return new Dictionary<string, List<GH_Integer>> { { "BranchSum", new List<GH_Integer>() } };

                        // Sum all items in this branch and return as a single value
                        int branchSum = aList.Sum(item => item?.Value ?? 0);
                        return new Dictionary<string, List<GH_Integer>> { { "BranchSum", new List<GH_Integer> { new GH_Integer(branchSum) } } };
                    }

                    var result = await DataTreeProcessor.RunAsync<GH_Integer, GH_Integer>(
                        trees,
                        Func,
                        options,
                        progressCallback: null,
                        token: token).ConfigureAwait(false);

                    if (result != null && result.TryGetValue("BranchSum", out var sumTree) && sumTree != null)
                        _resultTree = sumTree;
                    else
                        _resultTree = new GH_Structure<GH_Integer>();

                    // BranchToBranch should:
                    // 1. Call function TWICE (once per branch)
                    // 2. First call with [1,2,3], sum = 6, placed at {0}
                    // 3. Second call with [4,5], sum = 9, placed at {1}
                    bool ok =
                        _functionCallCount == 2 &&
                        _resultTree != null &&
                        _resultTree.PathCount == 2 &&
                        _resultTree.get_Branch(path0) != null &&
                        _resultTree.get_Branch(path0).Count == 1 &&
                        _resultTree.get_Branch(path0)[0] is GH_Integer gi0 && gi0.Value == 6 &&
                        _resultTree.get_Branch(path1) != null &&
                        _resultTree.get_Branch(path1).Count == 1 &&
                        _resultTree.get_Branch(path1)[0] is GH_Integer gi1 && gi1.Value == 9;

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"BranchToBranch topology. Input: {{{path0}}}=[1,2,3], {{{path1}}}=[4,5]."));
                    _messages.Add(new GH_String($"Function called {_functionCallCount} time(s) (expected 2)."));
                    _messages.Add(new GH_String($"Expected branch sums: {{{path0}}}=[6], {{{path1}}}=[9]."));
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
                message = _success.Value ? "Processed BranchToBranch topology successfully" : "Processing failed";
            }
        }
    }
}
