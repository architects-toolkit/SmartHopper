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
    /// Test component: two inputs, three items each, different paths.
    /// When inputs have different paths, items must NOT be combined across paths.
    /// Only when paths match should items be matched item-by-item. In this test,
    /// the output should be identical to the input trees (per-branch passthrough).
    /// </summary>
    public class DataTreeProcessorDifferentPathsThreeItemsEachTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("5A7B9B0C-12D0-4B90-AE17-5D1F764C6C5A");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.quinary;

        public DataTreeProcessorDifferentPathsThreeItemsEachTestComponent()
            : base("Test DataTreeProcessor (Different Paths, 3 items each)", "TEST-DTP-DIFF-3",
                  "Tests DataTreeProcessor with two input trees with different paths (three items each).",
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
            private readonly DataTreeProcessorDifferentPathsThreeItemsEachTestComponent _parent;

            public Worker(DataTreeProcessorDifferentPathsThreeItemsEachTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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
                    treeB.Append(new GH_Integer(4), pathB);
                    treeB.Append(new GH_Integer(5), pathB);
                    treeB.Append(new GH_Integer(6), pathB);

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

                    var (dataCount, iterations) = DataTreeProcessor.CalculateProcessingMetrics(trees, options);
                    Debug.WriteLine($"[DiffPaths3Each] Iterations: {iterations}, DataCount: {dataCount}");

                    async Task<Dictionary<string, List<GH_Integer>>> Func(Dictionary<string, List<GH_Integer>> branches)
                    {
                        await Task.Yield();
                        var aList = branches.ContainsKey("A") ? branches["A"] : null;
                        var bList = branches.ContainsKey("B") ? branches["B"] : null;

                        // If both inputs exist on the SAME path, match item-by-item (example: sum).
                        if (aList != null && bList != null)
                        {
                            var normalized = DataTreeProcessor.NormalizeBranchLengths(new List<List<GH_Integer>> { aList, bList });
                            var aNorm = normalized[0];
                            var bNorm = normalized[1];
                            var sums = new List<GH_Integer>();
                            for (int i = 0; i < Math.Max(aList.Count, bList.Count); i++)
                            {
                                int ai = aNorm[i]?.Value ?? 0;
                                int bi = bNorm[i]?.Value ?? 0;
                                sums.Add(new GH_Integer(ai + bi));
                            }

                            return new Dictionary<string, List<GH_Integer>> { { "Result", sums } };
                        }

                        // Different paths case: passthrough the existing branch items unchanged.
                        if (aList != null)
                            return new Dictionary<string, List<GH_Integer>> { { "Result", new List<GH_Integer>(aList) } };
                        if (bList != null)
                            return new Dictionary<string, List<GH_Integer>> { { "Result", new List<GH_Integer>(bList) } };

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
                    else
                        _resultTree = new GH_Structure<GH_Integer>();

                    bool ok =
                        _resultTree != null &&
                        _resultTree.PathCount == 2 &&
                        _resultTree.get_Branch(pathA) != null && _resultTree.get_Branch(pathA).Count == 3 &&
                        _resultTree.get_Branch(pathA)[0] is GH_Integer giA0 && giA0.Value == 1 &&
                        _resultTree.get_Branch(pathA)[1] is GH_Integer giA1 && giA1.Value == 2 &&
                        _resultTree.get_Branch(pathA)[2] is GH_Integer giA2 && giA2.Value == 3 &&
                        _resultTree.get_Branch(pathB) != null && _resultTree.get_Branch(pathB).Count == 3 &&
                        _resultTree.get_Branch(pathB)[0] is GH_Integer giB0 && giB0.Value == 4 &&
                        _resultTree.get_Branch(pathB)[1] is GH_Integer giB1 && giB1.Value == 5 &&
                        _resultTree.get_Branch(pathB)[2] is GH_Integer giB2 && giB2.Value == 6;

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"Different paths A={pathA}, B={pathB}. A=[1,2,3], B=[4,5,6]. Expected passthrough: branch {pathA} -> [1,2,3], branch {pathB} -> [4,5,6]. No cross-path combination."));
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
                message = _success.Value ? "Processed different-path trees (3 each) successfully" : "Processing failed";
            }
        }
    }
}

