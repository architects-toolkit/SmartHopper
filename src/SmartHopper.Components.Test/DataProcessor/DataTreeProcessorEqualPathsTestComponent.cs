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
    /// Test component to validate DataTreeProcessor with two trees having equal paths (one item each).
    /// Internal hardcoded inputs are used; only Run? is exposed. Outputs the result tree, success flag, and messages.
    /// </summary>
    public class DataTreeProcessorEqualPathsTestComponent : StatefulAsyncComponentBase
    {
        public override Guid ComponentGuid => new Guid("B0C2B1B7-3A6C-46A5-9E52-9F9E4F6B7C11");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        public DataTreeProcessorEqualPathsTestComponent()
            : base("Test DataTreeProcessor (Equal Paths)", "TEST-DTP-EQ",
                  "Tests DataTreeProcessor with two input trees that share equal branch paths (one item each).",
                  "SmartHopper", "Testing Data")
        {
            // We want the component to run when Run? toggles on, even if inputs did not change (they are internal)
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            // No external inputs; this component uses internal hardcoded data for testing
        }

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
            private readonly DataTreeProcessorEqualPathsTestComponent _parent;

            public Worker(DataTreeProcessorEqualPathsTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                _parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                // No inputs to fetch; we will use internal hardcoded trees
                dataCount = 1;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    // Prepare two input trees with equal paths {0} and one item each
                    var path = new GH_Path(0);

                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(2), path);

                    var treeB = new GH_Structure<GH_Integer>();
                    treeB.Append(new GH_Integer(5), path);

                    var trees = new Dictionary<string, GH_Structure<GH_Integer>>
                    {
                        { "A", treeA },
                        { "B", treeB }
                    };

                    // Optional: log metrics
                    var (iterations, dataCount) = DataTreeProcessor.GetProcessingPathMetrics(trees, onlyMatchingPaths: true, groupIdenticalBranches: false);
                    Debug.WriteLine($"[DataTreeProcessorEqualPathsTest] Iterations: {iterations}, DataCount: {dataCount}");

                    // Define processing function: sums A and B branch first items and returns under key "Sum"
                    async Task<Dictionary<string, List<GH_Integer>>> Func(Dictionary<string, List<GH_Integer>> branches)
                    {
                        await Task.Yield(); // keep async signature

                        var aList = branches.ContainsKey("A") ? branches["A"] : null;
                        var bList = branches.ContainsKey("B") ? branches["B"] : null;

                        if (aList == null || bList == null || aList.Count == 0 || bList.Count == 0)
                            return new Dictionary<string, List<GH_Integer>> { { "Sum", new List<GH_Integer>() } };

                        int sum = aList[0].Value + bList[0].Value;
                        return new Dictionary<string, List<GH_Integer>>
                        {
                            { "Sum", new List<GH_Integer> { new GH_Integer(sum) } }
                        };
                    }

                    // Execute
                    var result = await DataTreeProcessor.RunFunctionAsync<GH_Integer, GH_Integer>(
                        trees,
                        Func,
                        progressCallback: null,
                        onlyMatchingPaths: true,
                        groupIdenticalBranches: false,
                        token: token);

                    // Extract result tree
                    if (result != null && result.TryGetValue("Sum", out var sumTree) && sumTree != null)
                    {
                        _resultTree = sumTree;
                    }
                    else
                    {
                        _resultTree = new GH_Structure<GH_Integer>();
                    }

                    // Validate expected output: path {0} with one item = 7
                    int expected = 7;
                    bool ok =
                        _resultTree != null &&
                        _resultTree.PathCount == 1 &&
                        _resultTree.get_Branch(path) != null &&
                        _resultTree.get_Branch(path).Count == 1 &&
                        _resultTree.get_Branch(path)[0] is GH_Integer gi && gi.Value == expected;

                    _success = new GH_Boolean(ok);

                    _messages.Add(new GH_String($"Inputs A=2, B=5 at path {path}. Expected sum {expected}."));
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

                message = _success.Value ? "Processed equal-path trees successfully" : "Processing failed";
            }
        }
    }
}
