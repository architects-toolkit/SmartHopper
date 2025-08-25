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
    /// Test component: two inputs, first input three items, second input one item, different paths.
    /// </summary>
    public class DataTreeProcessorDifferentPathsFirstThreeSecondOneTestComponent : StatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("7A6E5F0B-9D3C-4A0C-8B2E-1F3A4D5C6B7E");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.senary;

        public DataTreeProcessorDifferentPathsFirstThreeSecondOneTestComponent()
            : base("Test DataTreeProcessor (Different Paths, 3 + 1 items)", "TEST-DTP-DIFF-3-1",
                  "Tests DataTreeProcessor with different paths where A has 3 items and B has 1 item.",
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

        private class Worker : AsyncWorkerBase
        {
            private GH_Structure<GH_Integer> _resultTree = new GH_Structure<GH_Integer>();
            private GH_Boolean _success = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly DataTreeProcessorDifferentPathsFirstThreeSecondOneTestComponent _parent;

            public Worker(DataTreeProcessorDifferentPathsFirstThreeSecondOneTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    var pathA = new GH_Path(0);
                    var pathB = new GH_Path(1);

                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(1), pathA);
                    treeA.Append(new GH_Integer(2), pathA);
                    treeA.Append(new GH_Integer(3), pathA);

                    var treeB = new GH_Structure<GH_Integer>();
                    treeB.Append(new GH_Integer(5), pathB); // 1 item at {1}

                    var trees = new Dictionary<string, GH_Structure<GH_Integer>>
                    {
                        { "A", treeA },
                        { "B", treeB }
                    };

                    var (iterations, dataCount) = DataTreeProcessor.GetProcessingPathMetrics(trees, onlyMatchingPaths: false, groupIdenticalBranches: false);
                    Debug.WriteLine($"[DiffPaths3+1] Iterations: {iterations}, DataCount: {dataCount}");

                    async Task<Dictionary<string, List<GH_Integer>>> Func(Dictionary<string, List<GH_Integer>> branches)
                    {
                        await Task.Yield();
                        var aList = branches.ContainsKey("A") ? branches["A"] : null;
                        var bList = branches.ContainsKey("B") ? branches["B"] : null;
                        if (aList == null || bList == null)
                            return new Dictionary<string, List<GH_Integer>> { { "Sum", new List<GH_Integer>() } };
                        int sum = aList.Sum(x => x.Value) + bList.Sum(x => x.Value);
                        return new Dictionary<string, List<GH_Integer>> { { "Sum", new List<GH_Integer> { new GH_Integer(sum) } } };
                    }

                    var result = await DataTreeProcessor.RunFunctionAsync<GH_Integer, GH_Integer>(
                        trees,
                        Func,
                        progressCallback: null,
                        onlyMatchingPaths: false,
                        groupIdenticalBranches: false,
                        token: token);

                    if (result != null && result.TryGetValue("Sum", out var sumTree) && sumTree != null)
                        _resultTree = sumTree;
                    else
                        _resultTree = new GH_Structure<GH_Integer>();

                    int expected = (1 + 2 + 3) + 5; // 11
                    // With different paths and A having multiple items, expect result at A's path (pathA)
                    bool ok =
                        _resultTree != null &&
                        _resultTree.PathCount == 1 &&
                        _resultTree.get_Branch(pathA) != null &&
                        _resultTree.get_Branch(pathA).Count == 1 &&
                        _resultTree.get_Branch(pathA)[0] is GH_Integer gi && gi.Value == expected;

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"Different paths A={pathA} (3 items), B={pathB} (1 item). Expected path {pathA} with sum {expected}."));
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
                message = _success.Value ? "Processed different-path trees (3+1) successfully" : "Processing failed";
            }
        }
    }
}
