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
using Grasshopper.Kernel;
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Components.Test.Misc
{
    /// <summary>
    /// Test component for validating ComponentStateManager debounce behavior.
    /// This component demonstrates debounce cancellation and generation-based
    /// stale callback prevention.
    /// </summary>
    public class TestStateManagerDebounceComponent : StatefulComponentBase
    {
        /// <summary>
        /// The ComponentStateManager instance for this component.
        /// </summary>
        private readonly ComponentStateManager stateManager;

        /// <summary>
        /// Tracks state transition history for debugging.
        /// </summary>
        private readonly List<string> stateHistory = new List<string>();

        /// <summary>
        /// Tracks debounce events.
        /// </summary>
        private int debounceStartCount;

        /// <summary>
        /// Tracks debounce cancellation events.
        /// </summary>
        private int debounceCancelCount;

        /// <summary>
        /// Tracks rejected transitions.
        /// </summary>
        private int rejectedTransitionCount;

        /// <summary>
        /// Gets the unique component identifier.
        /// </summary>
        public override Guid ComponentGuid => new Guid("F4C612B0-2C57-47CE-B9FE-E10621F18937");

        /// <summary>
        /// Gets the component icon (null for test component).
        /// </summary>
        protected override Bitmap Icon => null;

        /// <summary>
        /// Gets the component exposure level.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestStateManagerDebounceComponent"/> class.
        /// </summary>
        public TestStateManagerDebounceComponent()
            : base(
                  "Test StateManager Debounce",
                  "TEST-DEBOUNCE",
                  "Test component for validating ComponentStateManager debounce behavior. " +
                  "Rapidly change inputs to test debounce cancellation and generation tracking.",
                  "SmartHopper",
                  "Testing Base")
        {
            this.stateManager = this.StateManager;

            // Subscribe to all events for comprehensive testing
            this.stateManager.StateChanged += this.OnStateChanged;
            this.stateManager.StateEntered += this.OnStateEntered;
            this.stateManager.StateExited += this.OnStateExited;
            this.stateManager.DebounceStarted += this.OnDebounceStarted;
            this.stateManager.DebounceCancelled += this.OnDebounceCancelled;
            this.stateManager.TransitionRejected += this.OnTransitionRejected;
        }

        #region Event Handlers

        private void OnStateChanged(ComponentState oldState, ComponentState newState)
        {
            var entry = $"{DateTime.Now:HH:mm:ss.fff}: {oldState} -> {newState}";
            this.stateHistory.Add(entry);
            Debug.WriteLine($"[{this.GetType().Name}] StateChanged: {entry}");

            // Keep history limited
            if (this.stateHistory.Count > 50)
            {
                this.stateHistory.RemoveAt(0);
            }
        }

        private void OnStateEntered(ComponentState newState)
        {
            Debug.WriteLine($"[{this.GetType().Name}] StateEntered: {newState}");
        }

        private void OnStateExited(ComponentState oldState)
        {
            Debug.WriteLine($"[{this.GetType().Name}] StateExited: {oldState}");
        }

        private void OnDebounceStarted(ComponentState targetState, int milliseconds)
        {
            this.debounceStartCount++;
            Debug.WriteLine($"[{this.GetType().Name}] DebounceStarted: target={targetState}, ms={milliseconds}, count={this.debounceStartCount}");
        }

        private void OnDebounceCancelled()
        {
            this.debounceCancelCount++;
            Debug.WriteLine($"[{this.GetType().Name}] DebounceCancelled: count={this.debounceCancelCount}");
        }

        private void OnTransitionRejected(ComponentState from, ComponentState to, string message)
        {
            this.rejectedTransitionCount++;
            Debug.WriteLine($"[{this.GetType().Name}] TransitionRejected: {from} -> {to}, reason: {message}");
        }

        #endregion

        /// <inheritdoc/>
        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("Value", "V", "A value to process.", GH_ParamAccess.item, 1);
            pManager.AddIntegerParameter("DebounceMs", "D", "Debounce time in milliseconds.", GH_ParamAccess.item, 500);
        }

        /// <inheritdoc/>
        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Result", "R", "The processed result.", GH_ParamAccess.item);
            pManager.AddTextParameter("History", "H", "State transition history.", GH_ParamAccess.list);
            pManager.AddTextParameter("Stats", "S", "Debounce statistics.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new TestDebounceWorker(this, this.AddRuntimeMessage);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Get debounce time from input
            int debounceMs = 500;
            DA.GetData("DebounceMs", ref debounceMs);

            // Demonstrate using StateManager for input change detection
            var currentHashes = new Dictionary<string, int>();
            for (int i = 0; i < this.Params.Input.Count; i++)
            {
                var param = this.Params.Input[i];
                currentHashes[param.Name] = param.VolatileData.GetHashCode();
            }

            this.stateManager.UpdatePendingHashes(currentHashes);

            // Check for changes using StateManager
            var changedInputs = this.stateManager.GetChangedInputs();
            if (changedInputs.Count > 0)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Inputs changed: {string.Join(", ", changedInputs)}");

                // Demonstrate debounce with StateManager
                // Note: This is for demonstration - in real usage, the base class would handle this
                // this.stateManager.StartDebounce(ComponentState.NeedsRun, debounceMs);
            }

            // Let base class handle the normal flow
            base.SolveInstance(DA);

            // Output history and stats
            DA.SetDataList("History", this.stateHistory);

            string stats = $"Debounce starts: {this.debounceStartCount}, " +
                          $"Debounce cancels: {this.debounceCancelCount}, " +
                          $"Rejected transitions: {this.rejectedTransitionCount}, " +
                          $"Current state: {this.stateManager.CurrentState}";
            DA.SetData("Stats", stats);
        }

        /// <summary>
        /// Worker that performs a simple calculation.
        /// </summary>
        private sealed class TestDebounceWorker : AsyncWorkerBase
        {
            private int inputValue = 1;
            private double result;
            private readonly TestStateManagerDebounceComponent parent;

            /// <summary>
            /// Initializes a new instance of the <see cref="TestDebounceWorker"/> class.
            /// </summary>
            /// <param name="parent">The parent component.</param>
            /// <param name="addRuntimeMessage">The runtime message handler.</param>
            public TestDebounceWorker(
                TestStateManagerDebounceComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            /// <inheritdoc/>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                int v = 1;
                DA.GetData("Value", ref v);
                this.inputValue = v;
                dataCount = 1;
            }

            /// <inheritdoc/>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                // Simulate some async work
                await Task.Delay(200, token);
                this.result = this.inputValue * 3.14159;
            }

            /// <inheritdoc/>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                this.parent.SetPersistentOutput("Result", this.result, DA);
                message = $"Processed: {this.inputValue} -> {this.result:F4}";
            }
        }
    }
}
