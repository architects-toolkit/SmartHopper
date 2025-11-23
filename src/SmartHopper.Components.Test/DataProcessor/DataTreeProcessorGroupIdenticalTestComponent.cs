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
    /// Test component for GroupIdenticalBranches flag: identical branches are grouped and processed only once.
    /// When branches have identical content across inputs, they should be processed only once.
    /// </summary>
    public class DataTreeProcessorGroupIdenticalTestComponent : StatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("E6F7G8H9-0C1D-4E2F-1A3B-4C5D6E7F8G9H");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public DataTreeProcessorGroupIdenticalTestComponent()
            : base("Test DataTreeProcessor (GroupIdentical)", "TEST-DTP-GROUP",
                  "Tests DataTreeProcessor with GroupIdenticalBranches=true where identical branches are processed only once.",
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
            private readonly DataTreeProcessorGroupIdenticalTestComponent _parent;
            private int _processCount = 0;

            public Worker(DataTreeProcessorGroupIdenticalTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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

                    // Create a tree with three paths, where path0 and path2 have identical content [1,2]
                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(1), path0);
                    treeA.Append(new GH_Integer(2), path0);
                    treeA.Append(new GH_Integer(3), path1);
                    treeA.Append(new GH_Integer(4), path1);
                    treeA.Append(new GH_Integer(1), path2); // Identical to path0
                    treeA.Append(new GH_Integer(2), path2); // Identical to path0

                    var trees = new Dictionary<string, GH_Structure<GH_Integer>>
                    {
                        { "A", treeA },
                    };

                    var (iterations, dataCount) = DataTreeProcessor.GetProcessingPathMetrics(trees, onlyMatchingPaths: false, groupIdenticalBranches: true);
                    Debug.WriteLine($"[GroupIdentical] Iterations: {iterations}, DataCount: {dataCount}");

                    // Function that counts how many times it's called
                    async Task<Dictionary<string, List<GH_Integer>>> Func(Dictionary<string, List<GH_Integer>> branches)
                    {
                        await Task.Yield();
                        _processCount++;
                        
                        var aList = branches.ContainsKey("A") ? branches["A"] : null;
                        if (aList == null || aList.Count == 0)
                            return new Dictionary<string, List<GH_Integer>> { { "Double", new List<GH_Integer>() } };

                        // Double each value
                        var doubled = aList.Select(item => new GH_Integer(item.Value * 2)).ToList();
                        return new Dictionary<string, List<GH_Integer>> { { "Double", doubled } };
                    }

                    var options = new ProcessingOptions
                    {
                        Topology = ProcessingTopology.BranchToBranch,
                        OnlyMatchingPaths = false,
                        GroupIdenticalBranches = true,
                    };

                    var result = await DataTreeProcessor.RunAsync<GH_Integer, GH_Integer>(
                        trees,
                        Func,
                        options,
                        progressCallback: null,
                        token: token).ConfigureAwait(false);

                    if (result != null && result.TryGetValue("Double", out var doubleTree) && doubleTree != null)
                        _resultTree = doubleTree;
                    else
                        _resultTree = new GH_Structure<GH_Integer>();

                    // With GroupIdenticalBranches=true, function should be called only twice (not three times)
                    // because path0 and path2 have identical content
                    // Results should still appear at all three paths
                    bool ok =
                        _processCount == 2 &&
                        _resultTree != null &&
                        _resultTree.PathCount == 3 &&
                        _resultTree.get_Branch(path0) != null && _resultTree.get_Branch(path0).Count == 2 &&
                        _resultTree.get_Branch(path0)[0] is GH_Integer gi00 && gi00.Value == 2 &&
                        _resultTree.get_Branch(path0)[1] is GH_Integer gi01 && gi01.Value == 4 &&
                        _resultTree.get_Branch(path1) != null && _resultTree.get_Branch(path1).Count == 2 &&
                        _resultTree.get_Branch(path1)[0] is GH_Integer gi10 && gi10.Value == 6 &&
                        _resultTree.get_Branch(path1)[1] is GH_Integer gi11 && gi11.Value == 8 &&
                        _resultTree.get_Branch(path2) != null && _resultTree.get_Branch(path2).Count == 2 &&
                        _resultTree.get_Branch(path2)[0] is GH_Integer gi20 && gi20.Value == 2 &&
                        _resultTree.get_Branch(path2)[1] is GH_Integer gi21 && gi21.Value == 4;

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"GroupIdenticalBranches=true. Input has 3 branches, but {path0} and {path2} are identical [1,2]."));
                    _messages.Add(new GH_String($"Function was called {_processCount} times (expected 2, not 3)."));
                    _messages.Add(new GH_String($"Results appear at all 3 paths: {{{path0}}}=[2,4], {{{path1}}}=[6,8], {{{path2}}}=[2,4]."));
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
                message = _success.Value ? "Processed with GroupIdenticalBranches successfully" : "Processing failed";
            }
        }
    }
}
