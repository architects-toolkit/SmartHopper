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
    /// Test component to validate DataTreeProcessor with two trees having equal paths (three items each).
    /// Uses internal data; outputs result tree, success flag, and messages.
    /// </summary>
    public class DataTreeProcessorEqualPathsThreeItemsTestComponent : StatefulComponentBase
    {
        /// <summary>
        /// Gets the unique component identifier.
        /// </summary>
        public override Guid ComponentGuid => new Guid("1F4D5C1B-8E6D-49B4-B55F-1A3F5E2E6B31");

        /// <summary>
        /// Gets the component icon (not used for test components).
        /// </summary>
        protected override Bitmap Icon => null;

        /// <summary>
        /// Gets the exposure level for this component in the toolbar.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataTreeProcessorEqualPathsThreeItemsTestComponent"/> class.
        /// </summary>
        public DataTreeProcessorEqualPathsThreeItemsTestComponent()
            : base("Test DataTreeProcessor (Equal Paths, 3 items)", "TEST-DTP-EQ-3",
                  "Tests DataTreeProcessor with two input trees that share equal branch paths (three items each).",
                  "SmartHopper", "Testing Data")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <summary>
        /// Registers additional input parameters (none for this test component).
        /// </summary>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager) { }

        /// <summary>
        /// Registers output parameters for the test results.
        /// </summary>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Result", "R", "Result data tree produced by DataTreeProcessor.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("Success", "S", "True if the result matches the expected value.", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Diagnostic messages.", GH_ParamAccess.list);
        }

        /// <summary>
        /// Creates the worker that performs the asynchronous test logic.
        /// </summary>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, AddRuntimeMessage);
        }

        private class Worker : AsyncWorkerBase
        {
            private GH_Structure<GH_Integer> _resultTree = new GH_Structure<GH_Integer>();
            private GH_Boolean _success = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly DataTreeProcessorEqualPathsThreeItemsTestComponent _parent;

            public Worker(DataTreeProcessorEqualPathsThreeItemsTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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

                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(1), path);
                    treeA.Append(new GH_Integer(2), path);
                    treeA.Append(new GH_Integer(3), path);

                    var treeB = new GH_Structure<GH_Integer>();
                    treeB.Append(new GH_Integer(4), path);
                    treeB.Append(new GH_Integer(5), path);
                    treeB.Append(new GH_Integer(6), path);

                    var trees = new Dictionary<string, GH_Structure<GH_Integer>>
                    {
                        { "A", treeA },
                        { "B", treeB },
                    };

                    var options = new ProcessingOptions
                    {
                        Topology = ProcessingTopology.BranchToBranch,
                        OnlyMatchingPaths = true,
                        GroupIdenticalBranches = false,
                    };

                    var (dataCount, iterations) = DataTreeProcessor.CalculateProcessingMetrics(trees, options);
                    Debug.WriteLine($"[EqualPaths3Items] Iterations: {iterations}, DataCount: {dataCount}");

                    async Task<Dictionary<string, List<GH_Integer>>> Func(Dictionary<string, List<GH_Integer>> branches)
                    {
                        await Task.Yield();
                        var aList = branches.ContainsKey("A") ? branches["A"] : null;
                        var bList = branches.ContainsKey("B") ? branches["B"] : null;
                        if (aList == null || bList == null || aList.Count == 0 || bList.Count == 0)
                        {
                            return new Dictionary<string, List<GH_Integer>> { { "Sum", new List<GH_Integer>() } };
                        }

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

                        return new Dictionary<string, List<GH_Integer>> { { "Sum", sums } };
                    }

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

                    bool ok =
                        _resultTree != null &&
                        _resultTree.PathCount == 1 &&
                        _resultTree.get_Branch(path) != null &&
                        _resultTree.get_Branch(path).Count == 3 &&
                        _resultTree.get_Branch(path)[0] is GH_Integer gi0 && gi0.Value == (1 + 4) &&
                        _resultTree.get_Branch(path)[1] is GH_Integer gi1 && gi1.Value == (2 + 5) &&
                        _resultTree.get_Branch(path)[2] is GH_Integer gi2 && gi2.Value == (3 + 6);

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"Equal paths {path}. A=[1,2,3], B=[4,5,6]. Expected pairwise sums [5,7,9]."));
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
                message = _success.Value ? "Processed equal-path trees (3 items) successfully" : "Processing failed";
            }
        }
    }
}

