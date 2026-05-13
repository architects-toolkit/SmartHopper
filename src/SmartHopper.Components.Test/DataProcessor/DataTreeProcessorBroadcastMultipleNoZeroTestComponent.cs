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
    /// Test Case 4: A={0}, B={1},{2} - Multiple top-level paths, none is {0}
    /// Rule 2 applies: A broadcasts to ALL paths in B
    /// </summary>
    public class DataTreeProcessorBroadcastMultipleNoZeroTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("11287A68-04D7-46F4-99DE-C5B0C45F0732");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.quinary;

        public DataTreeProcessorBroadcastMultipleNoZeroTestComponent()
            : base("Test Broadcast (Multiple No {0})", "TEST-BC-MULTI-NO0",
                  "Tests flat {0} broadcasting to multiple paths {1},{2} (no {0} in B)",
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
            private readonly DataTreeProcessorBroadcastMultipleNoZeroTestComponent _parent;

            public Worker(DataTreeProcessorBroadcastMultipleNoZeroTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var path2 = new GH_Path(2);

                    // A: flat {0} with [100]
                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(100), path0);

                    // B: {1} with [1], {2} with [2]
                    var treeB = new GH_Structure<GH_Integer>();
                    treeB.Append(new GH_Integer(1), path1);
                    treeB.Append(new GH_Integer(2), path2);

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

                        // Sum A and B
                        if (aList.Count > 0 && bList.Count > 0)
                        {
                            int sum = aList[0].Value + bList[0].Value;
                            return new Dictionary<string, List<GH_Integer>> { { "Result", new List<GH_Integer> { new GH_Integer(sum) } } };
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

                    // Expected: A broadcasts to both {1} and {2}
                    // {1}: 100 + 1 = 101
                    // {2}: 100 + 2 = 102
                    bool ok =
                        _resultTree != null &&
                        _resultTree.PathCount == 2 &&
                        _resultTree.get_Branch(path1) != null && _resultTree.get_Branch(path1).Count == 1 &&
                        _resultTree.get_Branch(path1)[0] is GH_Integer v1 && v1.Value == 101 &&
                        _resultTree.get_Branch(path2) != null && _resultTree.get_Branch(path2).Count == 1 &&
                        _resultTree.get_Branch(path2)[0] is GH_Integer v2 && v2.Value == 102;

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"Case 4: A={{0}} [100], B={{1}} [1], {{2}} [2]. Expected: A broadcasts to ALL paths (Rule 2)."));
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
                message = _success.Value ? "Broadcast to multiple paths (no {0}) test passed" : "Test failed";
            }
        }
    }
}
