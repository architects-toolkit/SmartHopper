/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
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
    /// Test Case 3: A={0}, B={0},{1} - Multiple top-level paths including {0}
    /// Rule 2 applies: A broadcasts to ALL paths in B (including {0} and {1})
    /// </summary>
    public class DataTreeProcessorBroadcastMultipleTopLevelTestComponent : StatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("CBD8E900-B6EF-4ADE-B68C-A1A6AB486647");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.quinary;

        public DataTreeProcessorBroadcastMultipleTopLevelTestComponent()
            : base("Test Broadcast (Multiple Top-Level)", "TEST-BC-MULTI-TOP",
                  "Tests flat {0} broadcasting to multiple top-level paths {0},{1}",
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
            private readonly DataTreeProcessorBroadcastMultipleTopLevelTestComponent _parent;

            public Worker(DataTreeProcessorBroadcastMultipleTopLevelTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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

                    // A: flat {0} with [10, 20]
                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(10), path0);
                    treeA.Append(new GH_Integer(20), path0);

                    // B: {0} with [1], {1} with [2]
                    var treeB = new GH_Structure<GH_Integer>();
                    treeB.Append(new GH_Integer(1), path0);
                    treeB.Append(new GH_Integer(2), path1);

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

                        // Concatenate A and B values
                        var result = new List<GH_Integer>();
                        result.AddRange(aList);
                        result.AddRange(bList);

                        return new Dictionary<string, List<GH_Integer>> { { "Result", result } };
                    }

                    var result = await DataTreeProcessor.RunAsync<GH_Integer, GH_Integer>(
                        trees,
                        Func,
                        options,
                        progressCallback: null,
                        token: token).ConfigureAwait(false);

                    if (result != null && result.TryGetValue("Result", out var outTree) && outTree != null)
                        _resultTree = outTree;

                    // Expected: A broadcasts to both {0} and {1}
                    // {0}: [10,20] + [1] = [10,20,1]
                    // {1}: [10,20] + [2] = [10,20,2]
                    bool ok =
                        _resultTree != null &&
                        _resultTree.PathCount == 2 &&
                        _resultTree.get_Branch(path0) != null && _resultTree.get_Branch(path0).Count == 3 &&
                        _resultTree.get_Branch(path0)[0] is GH_Integer a00 && a00.Value == 10 &&
                        _resultTree.get_Branch(path0)[1] is GH_Integer a01 && a01.Value == 20 &&
                        _resultTree.get_Branch(path0)[2] is GH_Integer a02 && a02.Value == 1 &&
                        _resultTree.get_Branch(path1) != null && _resultTree.get_Branch(path1).Count == 3 &&
                        _resultTree.get_Branch(path1)[0] is GH_Integer a10 && a10.Value == 10 &&
                        _resultTree.get_Branch(path1)[1] is GH_Integer a11 && a11.Value == 20 &&
                        _resultTree.get_Branch(path1)[2] is GH_Integer a12 && a12.Value == 2;

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"Case 3: A={{0}} [10,20], B={{0}} [1], {{1}} [2]. Expected: A broadcasts to ALL paths (Rule 2)."));
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
                message = _success.Value ? "Broadcast to multiple top-level test passed" : "Test failed";
            }
        }
    }
}
