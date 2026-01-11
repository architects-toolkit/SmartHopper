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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
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
    /// Test Case 5 & 6: A={0}, B={0;0},{0;1} - Deeper paths under same root 0
    /// Rule 3 applies: A broadcasts to ALL deeper paths
    /// </summary>
    public class DataTreeProcessorBroadcastDeeperSameRootTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("0E1105D8-1EA0-446B-B51D-F90D1EC29342");
        protected override Bitmap Icon => null;
        public override GH_Exposure Exposure => GH_Exposure.quinary;

        public DataTreeProcessorBroadcastDeeperSameRootTestComponent()
            : base("Test Broadcast (Deeper Same Root)", "TEST-BC-DEEP-ROOT0",
                  "Tests flat {0} broadcasting to deeper paths {0;0},{0;1}",
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
            private readonly DataTreeProcessorBroadcastDeeperSameRootTestComponent _parent;

            public Worker(DataTreeProcessorBroadcastDeeperSameRootTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
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

                    // A: flat {0} with [50, 60]
                    var treeA = new GH_Structure<GH_Integer>();
                    treeA.Append(new GH_Integer(50), path0);
                    treeA.Append(new GH_Integer(60), path0);

                    // B: {0;0} with [1,2], {0;1} with [3,4]
                    var treeB = new GH_Structure<GH_Integer>();
                    treeB.Append(new GH_Integer(1), path00);
                    treeB.Append(new GH_Integer(2), path00);
                    treeB.Append(new GH_Integer(3), path01);
                    treeB.Append(new GH_Integer(4), path01);

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

                        // Concatenate A and B
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

                    // Expected: A broadcasts to deeper {0;0} and {0;1}
                    // {0;0}: [50,60] + [1,2] = [50,60,1,2]
                    // {0;1}: [50,60] + [3,4] = [50,60,3,4]
                    bool ok =
                        _resultTree != null &&
                        _resultTree.PathCount == 2 &&
                        _resultTree.get_Branch(path00) != null && _resultTree.get_Branch(path00).Count == 4 &&
                        _resultTree.get_Branch(path00)[0] is GH_Integer v00 && v00.Value == 50 &&
                        _resultTree.get_Branch(path00)[1] is GH_Integer v01 && v01.Value == 60 &&
                        _resultTree.get_Branch(path00)[2] is GH_Integer v02 && v02.Value == 1 &&
                        _resultTree.get_Branch(path00)[3] is GH_Integer v03 && v03.Value == 2 &&
                        _resultTree.get_Branch(path01) != null && _resultTree.get_Branch(path01).Count == 4 &&
                        _resultTree.get_Branch(path01)[0] is GH_Integer v10 && v10.Value == 50 &&
                        _resultTree.get_Branch(path01)[1] is GH_Integer v11 && v11.Value == 60 &&
                        _resultTree.get_Branch(path01)[2] is GH_Integer v12 && v12.Value == 3 &&
                        _resultTree.get_Branch(path01)[3] is GH_Integer v13 && v13.Value == 4;

                    _success = new GH_Boolean(ok);
                    _messages.Add(new GH_String($"Case 5&6: A={{0}} [50,60], B={{0;0}} [1,2], {{0;1}} [3,4]. Expected: A broadcasts to deeper paths (Rule 3)."));
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
                message = _success.Value ? "Broadcast to deeper same root test passed" : "Test failed";
            }
        }
    }
}
