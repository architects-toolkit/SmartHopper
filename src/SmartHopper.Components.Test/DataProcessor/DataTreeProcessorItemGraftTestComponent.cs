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
    /// Test component for ItemGraft topology: each item is grafted into its own separate branch.
    /// Each item from input trees is processed independently, and results are grafted into separate branches.
    /// </summary>
    public class DataTreeProcessorItemGraftTestComponent : StatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("3B09EE1F-00A3-4B7D-86DA-4C7EB0C6C0C3");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.septenary;

        public DataTreeProcessorItemGraftTestComponent()
            : base("Test DataTreeProcessor (ItemGraft)", "TEST-DTP-GRAFT",
                  "Tests DataTreeProcessor with ItemGraft topology where each item result is grafted into a separate branch.",
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
            private readonly DataTreeProcessorItemGraftTestComponent _parent;

            public Worker(DataTreeProcessorItemGraftTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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

                    // Create two trees: A=[1,2], B=[10,20] at same path
                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(1), path);
                    treeA.Append(new GH_Integer(2), path);

                    var treeB = new GH_Structure<GH_Integer>();
                    treeB.Append(new GH_Integer(10), path);
                    treeB.Append(new GH_Integer(20), path);

                    var trees = new Dictionary<string, GH_Structure<GH_Integer>>
                    {
                        { "A", treeA },
                        { "B", treeB },
                    };

                    var (iterations, dataCount) = DataTreeProcessor.GetProcessingPathMetrics(trees, onlyMatchingPaths: false, groupIdenticalBranches: false);
                    Debug.WriteLine($"[ItemGraft] Iterations: {iterations}, DataCount: {dataCount}");

                    // ItemGraft: function receives one item from each tree at a time
                    async Task<Dictionary<string, List<GH_Integer>>> Func(Dictionary<string, List<GH_Integer>> items)
                    {
                        await Task.Yield();
                        
                        // In ItemGraft, each dictionary contains exactly one item per input tree
                        var aItem = items.ContainsKey("A") && items["A"].Count > 0 ? items["A"][0] : null;
                        var bItem = items.ContainsKey("B") && items["B"].Count > 0 ? items["B"][0] : null;

                        if (aItem == null || bItem == null)
                            return new Dictionary<string, List<GH_Integer>> { { "Sum", new List<GH_Integer>() } };

                        int sum = aItem.Value + bItem.Value;
                        return new Dictionary<string, List<GH_Integer>> { { "Sum", new List<GH_Integer> { new GH_Integer(sum) } } };
                    }

                    var options = new ProcessingOptions
                    {
                        Topology = ProcessingTopology.ItemGraft,
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

                    // ItemGraft should graft each result into separate branches:
                    // {0;0} = [11], {0;1} = [22]
                    var expectedPath0 = new GH_Path(0, 0);
                    var expectedPath1 = new GH_Path(0, 1);

                    bool ok =
                        _resultTree != null &&
                        _resultTree.PathCount == 2 &&
                        _resultTree.get_Branch(expectedPath0) != null &&
                        _resultTree.get_Branch(expectedPath0).Count == 1 &&
                        _resultTree.get_Branch(expectedPath0)[0] is GH_Integer gi0 && gi0.Value == 11 &&
                        _resultTree.get_Branch(expectedPath1) != null &&
                        _resultTree.get_Branch(expectedPath1).Count == 1 &&
                        _resultTree.get_Branch(expectedPath1)[0] is GH_Integer gi1 && gi1.Value == 22;

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"ItemGraft topology at path {path}. A=[1,2], B=[10,20]. Expected grafted results: {{{expectedPath0}}}=[11], {{{expectedPath1}}}=[22]."));
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
                message = _success.Value ? "Processed ItemGraft topology successfully" : "Processing failed";
            }
        }
    }
}
