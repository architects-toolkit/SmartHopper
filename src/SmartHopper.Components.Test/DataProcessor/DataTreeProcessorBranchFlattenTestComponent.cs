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
    /// Test component for BranchFlatten topology: all items from all branches are flattened into a single list,
    /// processed together, and the results are placed in a single output branch.
    /// </summary>
    public class DataTreeProcessorBranchFlattenTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("E9642177-D368-4E9D-9BD6-E84C46D0958F");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public DataTreeProcessorBranchFlattenTestComponent()
            : base("Test DataTreeProcessor (BranchFlatten)", "TEST-DTP-FLATTEN",
                  "Tests DataTreeProcessor with BranchFlatten topology where all branches are flattened and processed together.",
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
            private readonly DataTreeProcessorBranchFlattenTestComponent _parent;
            private int _functionCallCount = 0;

            public Worker(DataTreeProcessorBranchFlattenTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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

                    // Create a tree with two branches: {0}=[1,2], {1}=[3,4]
                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(1), path0);
                    treeA.Append(new GH_Integer(2), path0);
                    treeA.Append(new GH_Integer(3), path1);
                    treeA.Append(new GH_Integer(4), path1);

                    var trees = new Dictionary<string, GH_Structure<GH_Integer>>
                    {
                        { "A", treeA },
                    };

                    var options = new ProcessingOptions
                    {
                        Topology = ProcessingTopology.BranchFlatten,
                        OnlyMatchingPaths = false,
                        GroupIdenticalBranches = false,
                    };

                    var (dataCount, iterations) = DataTreeProcessor.CalculateProcessingMetrics(trees, options);
                    Debug.WriteLine($"[BranchFlatten] Iterations: {iterations}, DataCount: {dataCount}");

                    // BranchFlatten: function receives ALL items from ALL branches as a single flattened list
                    async Task<Dictionary<string, List<GH_Integer>>> Func(Dictionary<string, List<GH_Integer>> branches)
                    {
                        await Task.Yield();
                        _functionCallCount++;

                        var aList = branches.ContainsKey("A") ? branches["A"] : null;
                        if (aList == null || aList.Count == 0)
                            return new Dictionary<string, List<GH_Integer>> { { "Sum", new List<GH_Integer>() } };

                        // Sum all values together
                        int sum = aList.Sum(item => item?.Value ?? 0);
                        return new Dictionary<string, List<GH_Integer>> { { "Sum", new List<GH_Integer> { new GH_Integer(sum) } } };
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

                    // BranchFlatten should:
                    // 1. Call function only ONCE with all items flattened: [1,2,3,4]
                    // 2. Result sum = 1+2+3+4 = 10
                    // 3. Output placed in single branch {0}
                    bool ok =
                        _functionCallCount == 1 &&
                        _resultTree != null &&
                        _resultTree.PathCount == 1 &&
                        _resultTree.get_Branch(path0) != null &&
                        _resultTree.get_Branch(path0).Count == 1 &&
                        _resultTree.get_Branch(path0)[0] is GH_Integer gi && gi.Value == 10;

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"BranchFlatten topology. Input: {{{path0}}}=[1,2], {{{path1}}}=[3,4]."));
                    _messages.Add(new GH_String($"Function called {_functionCallCount} time(s) (expected 1)."));
                    _messages.Add(new GH_String($"Expected flattened sum [10] at {{{path0}}}."));
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
                message = _success.Value ? "Processed BranchFlatten topology successfully" : "Processing failed";
            }
        }
    }
}
