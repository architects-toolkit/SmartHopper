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
    /// Test Case 9 & 10: A={0}, B={0},{0;0},{0;1} - Direct match + deeper paths
    /// Rule 4 applies: A matches ONLY B's {0}, NOT the deeper {0;0} or {0;1}
    /// </summary>
    public class DataTreeProcessorDirectMatchPrecedenceTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("77095C92-474F-4D5C-9EA6-6FE31FFFA710");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.quinary;

        public DataTreeProcessorDirectMatchPrecedenceTestComponent()
            : base("Test Direct Match Precedence", "TEST-BC-DIRECT",
                  "Tests direct {0} match taking precedence over deeper paths",
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
            private readonly DataTreeProcessorDirectMatchPrecedenceTestComponent _parent;

            public Worker(DataTreeProcessorDirectMatchPrecedenceTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var path01 = new GH_Path(0, 1);

                    // A: flat {0} with [99]
                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(99), path0);

                    // B: {0} with [1], {0;0} with [2], {0;1} with [3]
                    var treeB = new GH_Structure<GH_Integer>();
                    treeB.Append(new GH_Integer(1), path0);
                    treeB.Append(new GH_Integer(2), path00);
                    treeB.Append(new GH_Integer(3), path01);

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

                    // Expected: A matches ONLY {0}, not {0;0} or {0;1}
                    // {0}: [99] + [1] = [99,1]
                    // {0;0}: [] + [2] = [2] (A doesn't broadcast here)
                    // {0;1}: [] + [3] = [3] (A doesn't broadcast here)
                    bool ok =
                        _resultTree != null &&
                        _resultTree.PathCount == 3 &&
                        _resultTree.get_Branch(path0) != null && _resultTree.get_Branch(path0).Count == 2 &&
                        _resultTree.get_Branch(path0)[0] is GH_Integer v00 && v00.Value == 99 &&
                        _resultTree.get_Branch(path0)[1] is GH_Integer v01 && v01.Value == 1 &&
                        _resultTree.get_Branch(path00) != null && _resultTree.get_Branch(path00).Count == 1 &&
                        _resultTree.get_Branch(path00)[0] is GH_Integer v10 && v10.Value == 2 &&
                        _resultTree.get_Branch(path01) != null && _resultTree.get_Branch(path01).Count == 1 &&
                        _resultTree.get_Branch(path01)[0] is GH_Integer v20 && v20.Value == 3;

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"Case 9&10: A={{0}} [99], B={{0}} [1], {{0;0}} [2], {{0;1}} [3]. Expected: A matches only {{0}}, not deeper (Rule 4)."));
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
                message = _success.Value ? "Direct match precedence test passed" : "Test failed";
            }
        }
    }
}

