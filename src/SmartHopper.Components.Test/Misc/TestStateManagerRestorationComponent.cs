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
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Components.Test.Misc
{
    /// <summary>
    /// Test component for validating ComponentStateManager file restoration scenarios.
    /// This component demonstrates the new state management pattern and can be used
    /// to manually test file save/restore behavior in Grasshopper.
    /// </summary>
    public class TestStateManagerRestorationComponent : StatefulComponentBase
    {
        /// <summary>
        /// The ComponentStateManager instance for this component.
        /// Used to demonstrate the new centralized state management approach.
        /// </summary>
        private readonly ComponentStateManager stateManager;

        /// <summary>
        /// Tracks the number of times Read() was called.
        /// </summary>
        private int readCallCount;

        /// <summary>
        /// Tracks the number of times SolveInstance() was called after restoration.
        /// </summary>
        private int solveAfterRestoreCount;

        /// <summary>
        /// Indicates if restoration successfully preserved outputs.
        /// </summary>
        private bool restorationSuccess;

        /// <summary>
        /// Gets the unique component identifier.
        /// </summary>
        public override Guid ComponentGuid => new Guid("E3C612B0-2C57-47CE-B9FE-E10621F18936");

        /// <summary>
        /// Gets the component icon (null for test component).
        /// </summary>
        protected override Bitmap Icon => null;

        /// <summary>
        /// Gets the component exposure level.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestStateManagerRestorationComponent"/> class.
        /// </summary>
        public TestStateManagerRestorationComponent()
            : base(
                  "Test StateManager Restoration",
                  "TEST-RESTORE",
                  "Test component for validating ComponentStateManager file restoration. " +
                  "Run with a number, save the file, close, reopen - outputs should be preserved.",
                  "SmartHopper",
                  "Testing Base")
        {
            this.stateManager = new ComponentStateManager(this.GetType().Name);
            this.stateManager.StateChanged += this.OnStateManagerStateChanged;
        }

        /// <summary>
        /// Handles state changes from the ComponentStateManager.
        /// </summary>
        /// <param name="oldState">The previous state.</param>
        /// <param name="newState">The new state.</param>
        private void OnStateManagerStateChanged(ComponentState oldState, ComponentState newState)
        {
            Debug.WriteLine($"[{this.GetType().Name}] StateManager: {oldState} -> {newState}");
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Number", "N", "A number to process (1-100).", GH_ParamAccess.item, 10);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Result", "R", "The processed result (N * 2 + 1).", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Restoration status information.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new TestRestorationWorker(this, this.AddRuntimeMessage);
        }

        /// <inheritdoc/>
        public override bool Read(GH_IReader reader)
        {
            this.readCallCount++;
            Debug.WriteLine($"[{this.GetType().Name}] Read() called (count: {this.readCallCount})");

            // Demonstrate using ComponentStateManager for restoration
            this.stateManager.BeginRestoration();

            try
            {
                // Read state manager hashes if they exist
                var hashes = new Dictionary<string, int>();
                var branchCounts = new Dictionary<string, int>();

                if (reader.ItemExists("SM_HashCount"))
                {
                    int hashCount = reader.GetInt32("SM_HashCount");
                    for (int i = 0; i < hashCount; i++)
                    {
                        string key = reader.GetString($"SM_HashKey_{i}");
                        int value = reader.GetInt32($"SM_HashValue_{i}");
                        hashes[key] = value;
                    }
                }

                this.stateManager.RestoreCommittedHashes(hashes, branchCounts);

                // Call base Read which handles persistent outputs
                var result = base.Read(reader);

                this.stateManager.CommitHashes();
                return result;
            }
            finally
            {
                this.stateManager.EndRestoration();
            }
        }

        /// <inheritdoc/>
        public override bool Write(GH_IWriter writer)
        {
            Debug.WriteLine($"[{this.GetType().Name}] Write() called");

            // Write state manager hashes
            var hashes = this.stateManager.GetCommittedHashes();
            writer.SetInt32("SM_HashCount", hashes.Count);

            int i = 0;
            foreach (var kvp in hashes)
            {
                writer.SetString($"SM_HashKey_{i}", kvp.Key);
                writer.SetInt32($"SM_HashValue_{i}", kvp.Value);
                i++;
            }

            return base.Write(writer);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Track solves after restoration
            if (this.stateManager.IsSuppressingInputChanges)
            {
                this.solveAfterRestoreCount++;
                Debug.WriteLine($"[{this.GetType().Name}] First solve after restoration (count: {this.solveAfterRestoreCount})");

                // Clear suppression after first solve - this is the key to preventing data loss
                this.stateManager.ClearSuppressionAfterFirstSolve();
            }

            // Check if we have restored outputs
            if (this.persistentOutputs.ContainsKey("Result"))
            {
                this.restorationSuccess = true;
            }

            // Let base class handle the normal flow
            base.SolveInstance(DA);

            // Set status output
            string status = $"Read calls: {this.readCallCount}, Solves after restore: {this.solveAfterRestoreCount}, " +
                           $"Restoration success: {this.restorationSuccess}, StateManager state: {this.stateManager.CurrentState}";
            DA.SetData("Status", status);
        }

        /// <summary>
        /// Worker that performs a simple calculation to test persistence.
        /// </summary>
        private sealed class TestRestorationWorker : AsyncWorkerBase
        {
            private int inputNumber = 10;
            private double result;
            private readonly TestStateManagerRestorationComponent parent;

            /// <summary>
            /// Initializes a new instance of the <see cref="TestRestorationWorker"/> class.
            /// </summary>
            /// <param name="parent">The parent component.</param>
            /// <param name="addRuntimeMessage">The runtime message handler.</param>
            public TestRestorationWorker(
                TestStateManagerRestorationComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            /// <inheritdoc/>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                int n = 10;
                DA.GetData("Number", ref n);
                this.inputNumber = Math.Max(1, Math.Min(n, 100));
                dataCount = 1;
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                // Simple calculation with a small delay to simulate async work
                await Task.Delay(100, token);
                this.result = (this.inputNumber * 2d) + 1d;
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Result", this.result, DA);
                message = $"Calculated: {this.inputNumber} * 2 + 1 = {this.result}";
            }
        }
    }
}
