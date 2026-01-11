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
    /// Test Case 7 & 8: A={0}, B={1;0},{1;1} - Deeper paths under different root 1
    /// Rule 3 applies: A broadcasts to ALL deeper paths regardless of root
    /// </summary>
    public class DataTreeProcessorBroadcastDeeperDiffRootTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("D659A768-A076-4946-80B9-A8AD99D4F740");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.quinary;

        public DataTreeProcessorBroadcastDeeperDiffRootTestComponent()
            : base("Test Broadcast (Deeper Diff Root)", "TEST-BC-DEEP-ROOT1",
                  "Tests flat {0} broadcasting to deeper paths {1;0},{1;1}",
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
            private readonly DataTreeProcessorBroadcastDeeperDiffRootTestComponent _parent;

            public Worker(DataTreeProcessorBroadcastDeeperDiffRootTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var path10 = new GH_Path(1, 0);
                    var path11 = new GH_Path(1, 1);

                    // A: flat {0} with [7]
                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(7), path0);

                    // B: {1;0} with [10], {1;1} with [20]
                    var treeB = new GH_Structure<GH_Integer>();
                    treeB.Append(new GH_Integer(10), path10);
                    treeB.Append(new GH_Integer(20), path11);

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

                        // Multiply A by B
                        if (aList.Count > 0 && bList.Count > 0)
                        {
                            int product = aList[0].Value * bList[0].Value;
                            return new Dictionary<string, List<GH_Integer>> { { "Result", new List<GH_Integer> { new GH_Integer(product) } } };
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

                    // Expected: A broadcasts to {1;0} and {1;1}
                    // {1;0}: 7 * 10 = 70
                    // {1;1}: 7 * 20 = 140
                    bool ok =
                        _resultTree != null &&
                        _resultTree.PathCount == 2 &&
                        _resultTree.get_Branch(path10) != null && _resultTree.get_Branch(path10).Count == 1 &&
                        _resultTree.get_Branch(path10)[0] is GH_Integer v10 && v10.Value == 70 &&
                        _resultTree.get_Branch(path11) != null && _resultTree.get_Branch(path11).Count == 1 &&
                        _resultTree.get_Branch(path11)[0] is GH_Integer v11 && v11.Value == 140;

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"Case 7&8: A={{0}} [7], B={{1;0}} [10], {{1;1}} [20]. Expected: A broadcasts to deeper paths under different root (Rule 3)."));
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
                message = _success.Value ? "Broadcast to deeper different root test passed" : "Test failed";
            }
        }
    }
}
