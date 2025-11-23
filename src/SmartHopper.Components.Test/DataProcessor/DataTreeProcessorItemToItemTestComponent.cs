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
    /// Test component for ItemToItem topology: processes items independently across matching paths.
    /// Each item from input trees is processed independently, and results maintain the same branch structure.
    /// </summary>
    public class DataTreeProcessorItemToItemTestComponent : StatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("C4D5E6F7-8A9B-4C0D-9E1F-2A3B4C5D6E7F");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public DataTreeProcessorItemToItemTestComponent()
            : base("Test DataTreeProcessor (ItemToItem)", "TEST-DTP-ITEM",
                  "Tests DataTreeProcessor with ItemToItem topology where each item is processed independently.",
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
            private readonly DataTreeProcessorItemToItemTestComponent _parent;

            public Worker(DataTreeProcessorItemToItemTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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

                    // Create two trees: A=[1,2,3], B=[10,20,30] at same path
                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(1), path);
                    treeA.Append(new GH_Integer(2), path);
                    treeA.Append(new GH_Integer(3), path);

                    var treeB = new GH_Structure<GH_Integer>();
                    treeB.Append(new GH_Integer(10), path);
                    treeB.Append(new GH_Integer(20), path);
                    treeB.Append(new GH_Integer(30), path);

                    var trees = new Dictionary<string, GH_Structure<GH_Integer>>
                    {
                        { "A", treeA },
                        { "B", treeB },
                    };

                    var (iterations, dataCount) = DataTreeProcessor.GetProcessingPathMetrics(trees, onlyMatchingPaths: false, groupIdenticalBranches: false);
                    Debug.WriteLine($"[ItemToItem] Iterations: {iterations}, DataCount: {dataCount}");

                    // ItemToItem: function receives one item from each tree at a time
                    async Task<Dictionary<string, List<GH_Integer>>> Func(Dictionary<string, List<GH_Integer>> items)
                    {
                        await Task.Yield();
                        
                        // In ItemToItem, each dictionary contains exactly one item per input tree
                        var aItem = items.ContainsKey("A") && items["A"].Count > 0 ? items["A"][0] : null;
                        var bItem = items.ContainsKey("B") && items["B"].Count > 0 ? items["B"][0] : null;

                        if (aItem == null || bItem == null)
                            return new Dictionary<string, List<GH_Integer>> { { "Sum", new List<GH_Integer>() } };

                        int sum = aItem.Value + bItem.Value;
                        return new Dictionary<string, List<GH_Integer>> { { "Sum", new List<GH_Integer> { new GH_Integer(sum) } } };
                    }

                    var options = new ProcessingOptions
                    {
                        Topology = ProcessingTopology.ItemToItem,
                        OnlyMatchingPaths = false,
                        GroupIdenticalBranches = false,
                    };

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

                    // ItemToItem should process each item independently: (1+10), (2+20), (3+30) = [11, 22, 33]
                    bool ok =
                        _resultTree != null &&
                        _resultTree.PathCount == 1 &&
                        _resultTree.get_Branch(path) != null &&
                        _resultTree.get_Branch(path).Count == 3 &&
                        _resultTree.get_Branch(path)[0] is GH_Integer gi0 && gi0.Value == 11 &&
                        _resultTree.get_Branch(path)[1] is GH_Integer gi1 && gi1.Value == 22 &&
                        _resultTree.get_Branch(path)[2] is GH_Integer gi2 && gi2.Value == 33;

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"ItemToItem topology at path {path}. A=[1,2,3], B=[10,20,30]. Expected item-wise sums [11,22,33]."));
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
                message = _success.Value ? "Processed ItemToItem topology successfully" : "Processing failed";
            }
        }
    }
}
