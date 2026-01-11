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
    /// Test Case 12 & 13: A={0}, B={0;0},{1},{1;0} - Mixed depths and roots
    /// Rule 3 applies: A broadcasts to ALL paths (deeper topology present)
    /// </summary>
    public class DataTreeProcessorMixedDepthsTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("F788712F-B2B3-4131-87CA-E654F6153339");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.quinary;

        public DataTreeProcessorMixedDepthsTestComponent()
            : base("Test Mixed Depths", "TEST-BC-MIXED",
                  "Tests flat {0} broadcasting to mixed depth paths",
                  "SmartHopper", "Testing Data")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager) { }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Result", "R", "Result data tree", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Success", "S", "True if test passes", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Diagnostic messages", GH_ParamAccess.list);
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
            private readonly DataTreeProcessorMixedDepthsTestComponent _parent;

            public Worker(DataTreeProcessorMixedDepthsTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var path00 = new GH_Path(0, 0);
                    var path1 = new GH_Path(1);
                    var path10 = new GH_Path(1, 0);

                    // A: flat {0} with [5]
                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(5), path0);

                    // B: {0;0} with [10], {1} with [20], {1;0} with [30]
                    var treeB = new GH_Structure<GH_Integer>();
                    treeB.Append(new GH_Integer(10), path00);
                    treeB.Append(new GH_Integer(20), path1);
                    treeB.Append(new GH_Integer(30), path10);

                    var trees = new Dictionary<string, GH_Structure<GH_Integer>>
                    {
                        { "A", treeA },
                        { "B", treeB },
                    };

                    var options = new ProcessingOptions
                    {
                        Topology = ProcessingTopology.BranchToBranch,
                        OnlyMatchingPaths = false,
                        GroupIdenticalBranches = false,
                    };

                    async Task<Dictionary<string, List<GH_Integer>>> Func(Dictionary<string, List<GH_Integer>> branches)
                    {
                        await Task.Yield();
                        var aList = branches.ContainsKey("A") ? branches["A"] : new List<GH_Integer>();
                        var bList = branches.ContainsKey("B") ? branches["B"] : new List<GH_Integer>();

                        // Subtract: B - A
                        if (aList.Count > 0 && bList.Count > 0)
                        {
                            int diff = bList[0].Value - aList[0].Value;
                            return new Dictionary<string, List<GH_Integer>> { { "Result", new List<GH_Integer> { new GH_Integer(diff) } } };
                        }

                        return new Dictionary<string, List<GH_Integer>> { { "Result", new List<GH_Integer>() } };
                    }

                    var result = await DataTreeProcessor.RunAsync<GH_Integer, GH_Integer>(
                        trees,
                        Func,
                        options,
                        progressCallback: null,
                        token: token).ConfigureAwait(false);

                    if (result != null && result.TryGetValue("Result", out var outTree) && outTree != null)
                        _resultTree = outTree;

                    // Expected: A broadcasts to ALL paths (mixed depths trigger Rule 3)
                    // {0;0}: 10 - 5 = 5
                    // {1}: 20 - 5 = 15
                    // {1;0}: 30 - 5 = 25
                    bool ok =
                        _resultTree != null &&
                        _resultTree.PathCount == 3 &&
                        _resultTree.get_Branch(path00) != null && _resultTree.get_Branch(path00).Count == 1 &&
                        _resultTree.get_Branch(path00)[0] is GH_Integer v00 && v00.Value == 5 &&
                        _resultTree.get_Branch(path1) != null && _resultTree.get_Branch(path1).Count == 1 &&
                        _resultTree.get_Branch(path1)[0] is GH_Integer v1 && v1.Value == 15 &&
                        _resultTree.get_Branch(path10) != null && _resultTree.get_Branch(path10).Count == 1 &&
                        _resultTree.get_Branch(path10)[0] is GH_Integer v10 && v10.Value == 25;

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"Case 12&13: A={{0}} [5], B={{0;0}} [10], {{1}} [20], {{1;0}} [30]. Expected: A broadcasts to all mixed-depth paths (Rule 3)."));
                    _messages.Add(new GH_String(ok ? "Test succeeded." : "Test failed: unexpected result."));
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
                message = _success.Value ? "Mixed depths test passed" : "Test failed";
            }
        }
    }
}
