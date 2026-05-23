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
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
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

namespace SmartHopper.Components.Test.DataProcessor
{
    /// <summary>
    /// Test component for GH_Structure, GH_Path, and related Grasshopper data types.
    /// Tests basic functionality of Grasshopper data structures.
    /// </summary>
    public class GH_StructureTestComponent : StatefulComponentBase
    {
        public override Guid ComponentGuid => new Guid("0069CD6E-C135-4D2E-85F3-78A7FC498F82");

        protected override Bitmap Icon => null;

        public override GH_Exposure Exposure => GH_Exposure.octonary;

        public GH_StructureTestComponent()
            : base(
                "Test GH_Structure",
                "TEST-GH-STRUCT",
                "Tests GH_Structure, GH_Path, and related Grasshopper data types.",
                "SmartHopper",
                "Testing Data")
        {
            this.RunOnlyOnInputChanges = false;
        }

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager) { }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Success", "S", "True if all tests pass.", GH_ParamAccess.item);
            pManager.AddTextParameter("Messages", "M", "Test result messages.", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new Worker(this, this.AddRuntimeMessage);
        }

        private sealed class Worker : AsyncWorkerBase
        {
            private GH_Boolean _success = new GH_Boolean(false);
            private List<GH_String> _messages = new List<GH_String>();
            private readonly GH_StructureTestComponent _parent;

            public Worker(GH_StructureTestComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this._parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                dataCount = 1;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    var testsPassed = 0;
                    var testsFailed = 0;

                    // Test 1: GH_Structure_WithStrings_CanBeCreated
                    try
                    {
                        var tree = new GH_Structure<GH_String>();
                        tree.Append(new GH_String("test"), new GH_Path(0));
                        if (tree.Paths.Count == 1 && tree.DataCount == 1)
                        {
                            this._messages.Add(new GH_String("✓ GH_Structure_WithStrings_CanBeCreated"));
                            testsPassed++;
                        }
                        else
                        {
                            this._messages.Add(new GH_String("✗ GH_Structure_WithStrings_CanBeCreated: Unexpected counts"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        this._messages.Add(new GH_String($"✗ GH_Structure_WithStrings_CanBeCreated: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 2: GH_Structure_WithBooleans_CanBeCreated
                    try
                    {
                        var tree = new GH_Structure<GH_Boolean>();
                        tree.Append(new GH_Boolean(true), new GH_Path(0));
                        if (tree.Paths.Count == 1 && tree.DataCount == 1)
                        {
                            this._messages.Add(new GH_String("✓ GH_Structure_WithBooleans_CanBeCreated"));
                            testsPassed++;
                        }
                        else
                        {
                            this._messages.Add(new GH_String("✗ GH_Structure_WithBooleans_CanBeCreated: Unexpected counts"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        this._messages.Add(new GH_String($"✗ GH_Structure_WithBooleans_CanBeCreated: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 3: GH_Structure_WithIntegers_CanBeCreated
                    try
                    {
                        var tree = new GH_Structure<GH_Integer>();
                        tree.Append(new GH_Integer(42), new GH_Path(0));
                        if (tree.Paths.Count == 1 && tree.DataCount == 1)
                        {
                            this._messages.Add(new GH_String("✓ GH_Structure_WithIntegers_CanBeCreated"));
                            testsPassed++;
                        }
                        else
                        {
                            this._messages.Add(new GH_String("✗ GH_Structure_WithIntegers_CanBeCreated: Unexpected counts"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        this._messages.Add(new GH_String($"✗ GH_Structure_WithIntegers_CanBeCreated: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 4: GH_Structure_WithNumbers_CanBeCreated
                    try
                    {
                        var tree = new GH_Structure<GH_Number>();
                        tree.Append(new GH_Number(3.14), new GH_Path(0));
                        if (tree.Paths.Count == 1 && tree.DataCount == 1)
                        {
                            this._messages.Add(new GH_String("✓ GH_Structure_WithNumbers_CanBeCreated"));
                            testsPassed++;
                        }
                        else
                        {
                            this._messages.Add(new GH_String("✗ GH_Structure_WithNumbers_CanBeCreated: Unexpected counts"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        this._messages.Add(new GH_String($"✗ GH_Structure_WithNumbers_CanBeCreated: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 5: GH_Path_ToString_MatchesExpected
                    try
                    {
                        var path1 = new GH_Path(0);
                        var path2 = new GH_Path(1, 2);
                        var path3 = new GH_Path(0, 0, 0);
                        if (path1.ToString() == "{0}" && path2.ToString() == "{1;2}" && path3.ToString() == "{0;0;0}")
                        {
                            this._messages.Add(new GH_String("✓ GH_Path_ToString_MatchesExpected"));
                            testsPassed++;
                        }
                        else
                        {
                            this._messages.Add(new GH_String("✗ GH_Path_ToString_MatchesExpected: String mismatch"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        this._messages.Add(new GH_String($"✗ GH_Path_ToString_MatchesExpected: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 6: GH_Path_Equality_Works
                    try
                    {
                        var path1 = new GH_Path(0);
                        var path2 = new GH_Path(0);
                        var path3 = new GH_Path(1);
                        if (path1 == path2 && path1 != path3)
                        {
                            this._messages.Add(new GH_String("✓ GH_Path_Equality_Works"));
                            testsPassed++;
                        }
                        else
                        {
                            this._messages.Add(new GH_String("✗ GH_Path_Equality_Works: Equality check failed"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        this._messages.Add(new GH_String($"✗ GH_Path_Equality_Works: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 7: GH_Structure_MultipleItems_SamePath
                    try
                    {
                        var tree = new GH_Structure<GH_String>();
                        tree.Append(new GH_String("a"), new GH_Path(0));
                        tree.Append(new GH_String("b"), new GH_Path(0));
                        tree.Append(new GH_String("c"), new GH_Path(0));
                        if (tree.Paths.Count == 1 && tree.DataCount == 3)
                        {
                            this._messages.Add(new GH_String("✓ GH_Structure_MultipleItems_SamePath"));
                            testsPassed++;
                        }
                        else
                        {
                            this._messages.Add(new GH_String("✗ GH_Structure_MultipleItems_SamePath: Unexpected counts"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        this._messages.Add(new GH_String($"✗ GH_Structure_MultipleItems_SamePath: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 8: GH_Structure_MultipleItems_DifferentPaths
                    try
                    {
                        var tree = new GH_Structure<GH_String>();
                        tree.Append(new GH_String("a"), new GH_Path(0));
                        tree.Append(new GH_String("b"), new GH_Path(1));
                        tree.Append(new GH_String("c"), new GH_Path(2));
                        if (tree.Paths.Count == 3 && tree.DataCount == 3)
                        {
                            this._messages.Add(new GH_String("✓ GH_Structure_MultipleItems_DifferentPaths"));
                            testsPassed++;
                        }
                        else
                        {
                            this._messages.Add(new GH_String("✗ GH_Structure_MultipleItems_DifferentPaths: Unexpected counts"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        this._messages.Add(new GH_String($"✗ GH_Structure_MultipleItems_DifferentPaths: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 9: GH_Structure_GetBranch_ReturnsCorrectBranch
                    try
                    {
                        var tree = new GH_Structure<GH_String>();
                        tree.Append(new GH_String("a"), new GH_Path(0));
                        tree.Append(new GH_String("b"), new GH_Path(0));
                        tree.Append(new GH_String("c"), new GH_Path(1));
                        var branch = tree.get_Branch(new GH_Path(0));
                        if (branch != null && branch.Count == 2)
                        {
                            this._messages.Add(new GH_String("✓ GH_Structure_GetBranch_ReturnsCorrectBranch"));
                            testsPassed++;
                        }
                        else
                        {
                            this._messages.Add(new GH_String("✗ GH_Structure_GetBranch_ReturnsCorrectBranch: Branch count mismatch"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        this._messages.Add(new GH_String($"✗ GH_Structure_GetBranch_ReturnsCorrectBranch: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 10: GH_Structure_Flatten_ReducesToSinglePath
                    try
                    {
                        var tree = new GH_Structure<GH_String>();
                        tree.Append(new GH_String("a"), new GH_Path(0));
                        tree.Append(new GH_String("b"), new GH_Path(1));
                        tree.Flatten();
                        if (tree.Paths.Count == 1 && tree.DataCount == 2)
                        {
                            this._messages.Add(new GH_String("✓ GH_Structure_Flatten_ReducesToSinglePath"));
                            testsPassed++;
                        }
                        else
                        {
                            this._messages.Add(new GH_String("✗ GH_Structure_Flatten_ReducesToSinglePath: Unexpected counts"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        this._messages.Add(new GH_String($"✗ GH_Structure_Flatten_ReducesToSinglePath: {ex.Message}"));
                        testsFailed++;
                    }

                    // Test 11: GH_Structure_Graft_CreatesNestedPaths
                    try
                    {
                        var tree = new GH_Structure<GH_String>();
                        tree.Append(new GH_String("a"), new GH_Path(0));
                        tree.Append(new GH_String("b"), new GH_Path(0));
                        tree.Graft(GH_GraftMode.GraftAll);
                        if (tree.PathCount == 2)
                        {
                            this._messages.Add(new GH_String("✓ GH_Structure_Graft_CreatesNestedPaths"));
                            testsPassed++;
                        }
                        else
                        {
                            this._messages.Add(new GH_String("✗ GH_Structure_Graft_CreatesNestedPaths: Path count mismatch"));
                            testsFailed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        this._messages.Add(new GH_String($"✗ GH_Structure_Graft_CreatesNestedPaths: {ex.Message}"));
                        testsFailed++;
                    }

                    this._success = new GH_Boolean(testsFailed == 0);
                    this._messages.Insert(0, new GH_String($"GH_Structure Tests: {testsPassed} passed, {testsFailed} failed"));

                    await Task.Yield();
                }
                catch (OperationCanceledException)
                {
                    this._success = new GH_Boolean(false);
                    this._messages.Add(new GH_String("Operation was cancelled."));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Test cancelled.");
                }
                catch (Exception ex)
                {
                    this._success = new GH_Boolean(false);
                    this._messages.Add(new GH_String($"Exception: {ex.Message}"));
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this._parent.SetPersistentOutput("Success", this._success, DA);
                this._parent.SetPersistentOutput("Messages", this._messages, DA);
                message = this._success.Value ? "All GH_Structure tests passed" : "Some tests failed";
            }
        }
    }
}
