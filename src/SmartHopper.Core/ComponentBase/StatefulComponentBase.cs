/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * Portions of this code adapted from:
 * https://github.com/specklesystems/GrasshopperAsyncComponent
 * Apache License 2.0
 * Copyright (c) 2021 Speckle Systems
 */

/*
 * V2 implementation of StatefulAsyncComponentBase using ComponentStateManager.
 * This class delegates state management to ComponentStateManager for cleaner
 * state transitions, proper file restoration handling, and generation-based
 * debounce to prevent stale callbacks.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#if DEBUG
using System.Windows.Forms;
#endif
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.DataTree;
using SmartHopper.Core.IO;
using SmartHopper.Infrastructure.Settings;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// V2 implementation of stateful async component base using ComponentStateManager.
    /// Provides integrated state management, parallel processing, messaging, and persistence.
    /// </summary>
    public abstract class StatefulComponentBase : AsyncComponentBase
    {
        #region Fields

        /// <summary>
        /// The centralized state manager handling all state transitions, debouncing, and hash tracking.
        /// </summary>
        protected readonly ComponentStateManager StateManager;

        /// <summary>
        /// Storage for persistent output values that survive state transitions.
        /// </summary>
        protected readonly Dictionary<string, object> persistentOutputs;

        /// <summary>
        /// Storage for persistent output type information.
        /// </summary>
        private readonly Dictionary<string, Type> persistentDataTypes;

        /// <summary>
        /// Runtime messages that persist across state transitions.
        /// </summary>
        private readonly Dictionary<string, (GH_RuntimeMessageLevel Level, string Message)> runtimeMessages;

        /// <summary>
        /// The last data access object, used for state transitions.
        /// </summary>
        private IGH_DataAccess lastDA;

        /// <summary>
        /// The current Run parameter value.
        /// </summary>
        private bool run;

        private bool hasPreviousRun;

        private bool previousRun;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the component should only run when inputs change.
        /// If true (default), the component will only run when inputs have changed and Run is true.
        /// If false, the component will run whenever the Run parameter is set to true.
        /// </summary>
        public bool RunOnlyOnInputChanges { get; set; } = true;

        /// <summary>
        /// Gets the progress information for tracking processing operations.
        /// </summary>
        protected ProgressInfo ProgressInfo { get; private set; } = new ProgressInfo();

        /// <summary>
        /// Gets a value indicating whether the base class should automatically restore persistent outputs
        /// during Completed/Waiting states. Default is true for backward compatibility.
        /// </summary>
        protected virtual bool AutoRestorePersistentOutputs => true;

        /// <summary>
        /// Gets the default processing options used for data tree processing.
        /// </summary>
        protected virtual ProcessingOptions ComponentProcessingOptions => new ProcessingOptions
        {
            Topology = ProcessingTopology.ItemToItem,
            OnlyMatchingPaths = false,
            GroupIdenticalBranches = true,
        };

        /// <summary>
        /// Gets a value indicating whether the component is requested to run for the current solve.
        /// </summary>
        public bool Run => this.run;

        /// <summary>
        /// Gets the current state of the component.
        /// </summary>
        public ComponentState CurrentState => this.StateManager.CurrentState;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="StatefulComponentBase"/> class.
        /// </summary>
        /// <param name="name">The component's display name.</param>
        /// <param name="nickname">The component's nickname.</param>
        /// <param name="description">Description of the component's functionality.</param>
        /// <param name="category">Category in the Grasshopper toolbar.</param>
        /// <param name="subCategory">Subcategory in the Grasshopper toolbar.</param>
        protected StatefulComponentBase(
            string name,
            string nickname,
            string description,
            string category,
            string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            this.persistentOutputs = new Dictionary<string, object>();
            this.persistentDataTypes = new Dictionary<string, Type>();
            this.runtimeMessages = new Dictionary<string, (GH_RuntimeMessageLevel Level, string Message)>();

            // Initialize the centralized state manager
            this.StateManager = new ComponentStateManager(this.GetType().Name);

            // Subscribe to state manager events
            this.StateManager.StateChanged += this.OnStateManagerStateChanged;
            this.StateManager.StateEntered += this.OnStateManagerStateEntered;
        }

        #endregion

        #region State Manager Event Handlers

        /// <summary>
        /// Handles state changes from the ComponentStateManager.
        /// </summary>
        /// <param name="oldState">The previous state.</param>
        /// <param name="newState">The new state.</param>
        private void OnStateManagerStateChanged(ComponentState oldState, ComponentState newState)
        {
            Debug.WriteLine($"[{this.GetType().Name}] StateManager: {oldState} -> {newState}");

            // Update component message
            this.Message = this.GetStateMessage();

            // Clear messages when entering NeedsRun or Processing from a different state
            if ((newState == ComponentState.NeedsRun || newState == ComponentState.Processing) &&
                oldState != ComponentState.NeedsRun && oldState != ComponentState.Processing)
            {
                this.ClearPersistentRuntimeMessages();
            }

            // Handle specific state transitions
            switch (newState)
            {
                case ComponentState.Processing:
                    if (oldState != ComponentState.Processing)
                    {
                        Debug.WriteLine($"[{this.GetType().Name}] Resetting async state for fresh Processing transition");
                        this.ResetAsyncState();
                        this.ResetProgress();

                        // Safety net for boolean toggle scenarios
                        this.ScheduleProcessingSafetyCheck();
                    }

                    break;

                case ComponentState.NeedsRun:
                    Rhino.RhinoApp.InvokeOnUiThread(() =>
                    {
                        this.OnDisplayExpired(true);
                    });
                    break;

                case ComponentState.Cancelled:
                    Rhino.RhinoApp.InvokeOnUiThread(() =>
                    {
                        this.OnDisplayExpired(true);
                    });
                    break;
            }

            // Expire solution to trigger a new solve cycle
            if (newState != oldState)
            {
                Rhino.RhinoApp.InvokeOnUiThread(() =>
                {
                    this.ExpireSolution(true);
                });
            }
        }

        /// <summary>
        /// Handles entering a new state from the ComponentStateManager.
        /// </summary>
        /// <param name="newState">The state being entered.</param>
        private void OnStateManagerStateEntered(ComponentState newState)
        {
            this.Message = this.GetStateMessage();
        }

        /// <summary>
        /// Schedules a safety check for Processing state to handle boolean toggle edge cases.
        /// </summary>
        private void ScheduleProcessingSafetyCheck()
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(this.GetDebounceTime());

                if (this.StateManager.CurrentState == ComponentState.Processing && this.Workers.Count == 0)
                {
                    Debug.WriteLine($"[{this.GetType().Name}] Processing state without workers after delay, forcing ExpireSolution");
                    Rhino.RhinoApp.InvokeOnUiThread(() =>
                    {
                        this.ExpireSolution(true);
                    });
                }
            });
        }

        #endregion

        #region Parameter Registration

        /// <summary>
        /// Registers input parameters for the component.
        /// </summary>
        /// <param name="pManager">The input parameter manager.</param>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            this.RegisterAdditionalInputParams(pManager);
            pManager.AddBooleanParameter("Run?", "R", "Set this parameter to true to run the component.", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Register component-specific input parameters.
        /// </summary>
        /// <param name="pManager">The input parameter manager.</param>
        protected abstract void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager);

        /// <summary>
        /// Registers output parameters for the component.
        /// </summary>
        /// <param name="pManager">The output parameter manager.</param>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            this.RegisterAdditionalOutputParams(pManager);
        }

        /// <summary>
        /// Register component-specific output parameters.
        /// </summary>
        /// <param name="pManager">The output parameter manager.</param>
        protected abstract void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager);

        #endregion

        #region Lifecycle

        /// <summary>
        /// Performs pre-solve initialization and guards against unintended resets during processing.
        /// </summary>
        protected override void BeforeSolveInstance()
        {
            if (this.StateManager.CurrentState == ComponentState.Processing)
            {
                Debug.WriteLine("[StatefulComponentBase] Processing state... jumping to SolveInstance");
                return;
            }

            base.BeforeSolveInstance();
        }

        /// <summary>
        /// Main solving method for the component.
        /// </summary>
        /// <param name="DA">The data access object.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            this.lastDA = DA;

            // Handle first solve after restoration - clear suppression
            if (this.StateManager.IsSuppressingInputChanges)
            {
                Debug.WriteLine($"[{this.GetType().Name}] First solve after restoration, clearing suppression");
                this.StateManager.ClearSuppressionAfterFirstSolve();

                // If we have persistent outputs, stay in Completed state
                if (this.persistentOutputs.Count > 0)
                {
                    this.OnStateCompleted(DA);
                    return;
                }
                else
                {
                    // No outputs restored, transition to NeedsRun
                    this.StateManager.RequestTransition(ComponentState.NeedsRun, TransitionReason.FileRestoration);
                    return;
                }
            }

            // Read Run parameter
            bool run = false;
            DA.GetData("Run?", ref run);
            this.run = run;

            // Note: GH_Button drives volatile data and may not affect PersistentData hashes.
            // Track the last observed Run value to detect button pulses reliably.
            bool runValueChanged = !this.hasPreviousRun || this.previousRun != this.run;

            Debug.WriteLine($"[{this.GetType().Name}] SolveInstance - State: {this.StateManager.CurrentState}, InPreSolve: {this.InPreSolve}, Run: {this.run}");

            // Calculate current input hashes
            this.UpdatePendingInputHashes();

            // Execute state handler
            switch (this.StateManager.CurrentState)
            {
                case ComponentState.Completed:
                    this.OnStateCompleted(DA);
                    break;
                case ComponentState.Waiting:
                    this.OnStateWaiting(DA);
                    break;
                case ComponentState.NeedsRun:
                    this.OnStateNeedsRun(DA);
                    break;
                case ComponentState.Processing:
                    this.OnStateProcessing(DA);
                    break;
                case ComponentState.Cancelled:
                    this.OnStateCancelled(DA);
                    break;
                case ComponentState.Error:
                    this.OnStateError(DA);
                    break;
            }

            // Handle input change detection after state handlers
            this.HandleInputChangeDetection(DA, runValueChanged);

            this.hasPreviousRun = true;
            this.previousRun = this.run;
        }

        /// <summary>
        /// Updates the pending input hashes in the StateManager.
        /// </summary>
        private void UpdatePendingInputHashes()
        {
            var hashes = new Dictionary<string, int>();
            var branchCounts = new Dictionary<string, int>();

            for (int i = 0; i < this.Params.Input.Count; i++)
            {
                var param = this.Params.Input[i];
                int branchCount;
                int hash = CalculatePersistentDataHash(param, out branchCount);
                hashes[param.Name] = hash;
                branchCounts[param.Name] = branchCount;
            }

            this.StateManager.UpdatePendingHashes(hashes);
            this.StateManager.UpdatePendingBranchCounts(branchCounts);
        }

        /// <summary>
        /// Handles input change detection and debounce logic.
        /// </summary>
        /// <param name="DA">The data access object.</param>
        private void HandleInputChangeDetection(IGH_DataAccess DA, bool runValueChanged)
        {
            var currentState = this.StateManager.CurrentState;

            // Only check for input changes in idle states
            if (currentState != ComponentState.Completed &&
                currentState != ComponentState.Waiting &&
                currentState != ComponentState.Cancelled &&
                currentState != ComponentState.Error)
            {
                return;
            }

            // Get changed inputs from StateManager
            var changedInputs = this.StateManager.GetChangedInputs();

            // When configured to always run, a Run=true pulse should trigger Processing
            // even if the Run? input is driven by volatile data (e.g. GH_Button).
            if (!this.RunOnlyOnInputChanges && runValueChanged && this.run)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Run value changed to true (volatile-aware), transitioning to Processing");
                this.StateManager.RequestTransition(ComponentState.Processing, TransitionReason.RunEnabled);
                return;
            }

            // If only Run parameter changed to false, stay in current state
            if (changedInputs.Count == 1 && changedInputs[0] == "Run?" && !this.run)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Only Run changed to false, staying in current state");
                return;
            }

            // If only Run parameter changed to true
            if (changedInputs.Count == 1 && changedInputs[0] == "Run?" && this.run)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Only Run changed to true");

                if (this.RunOnlyOnInputChanges)
                {
                    // Default behavior - transition to Waiting
                    this.StateManager.RequestTransition(ComponentState.Waiting, TransitionReason.RunEnabled);
                }
                else
                {
                    // Always run when Run is true
                    this.StateManager.RequestTransition(ComponentState.Processing, TransitionReason.RunEnabled);
                }

                return;
            }

            // If other inputs changed
            if (changedInputs.Count > 0)
            {
                if (!this.run)
                {
                    Debug.WriteLine($"[{this.GetType().Name}] Inputs changed, starting debounce to NeedsRun");
                    this.StateManager.StartDebounce(ComponentState.NeedsRun, this.GetDebounceTime());
                }
                else
                {
                    Debug.WriteLine($"[{this.GetType().Name}] Inputs changed with Run=true, starting debounce to Processing");
                    this.StateManager.StartDebounce(ComponentState.Processing, this.GetDebounceTime());
                }
            }
        }

        /// <summary>
        /// Finalizes processing by committing hashes and transitioning to Completed.
        /// </summary>
        protected override void OnWorkerCompleted()
        {
            // Commit current hashes as the new baseline
            this.StateManager.CommitHashes();

            // Cancel any pending debounce
            this.StateManager.CancelDebounce();

            // Transition to Completed
            this.StateManager.RequestTransition(ComponentState.Completed, TransitionReason.ProcessingComplete);

            base.OnWorkerCompleted();
            Debug.WriteLine("[StatefulComponentBase] Worker completed, expiring solution");
            this.ExpireSolution(true);
        }

        /// <summary>
        /// Ensures the state machine does not remain stuck in Processing when the underlying tasks are canceled.
        /// </summary>
        protected override void OnTasksCanceled()
        {
            if (this.StateManager.CurrentState == ComponentState.Processing)
            {
                this.StateManager.RequestTransition(ComponentState.Cancelled, TransitionReason.Cancelled);
            }
        }

        #endregion

        #region State Handlers

        /// <summary>
        /// Handles the Completed state.
        /// </summary>
        /// <param name="DA">The data access object.</param>
        private void OnStateCompleted(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[{this.GetType().Name}] OnStateCompleted");

            this.Message = ComponentState.Completed.ToMessageString();
            this.ApplyPersistentRuntimeMessages();

            if (this.AutoRestorePersistentOutputs)
            {
                this.RestorePersistentOutputs(DA);
            }
        }

        /// <summary>
        /// Handles the Waiting state.
        /// </summary>
        /// <param name="DA">The data access object.</param>
        private void OnStateWaiting(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[{this.GetType().Name}] OnStateWaiting");

            this.ApplyPersistentRuntimeMessages();

            if (this.AutoRestorePersistentOutputs)
            {
                this.RestorePersistentOutputs(DA);
            }
        }

        /// <summary>
        /// Handles the NeedsRun state.
        /// </summary>
        /// <param name="DA">The data access object.</param>
        private void OnStateNeedsRun(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[{this.GetType().Name}] OnStateNeedsRun");

            bool run = false;
            DA.GetData("Run?", ref run);

            if (run)
            {
                this.ClearOnePersistentRuntimeMessage("needs_run");
                this.StateManager.RequestTransition(ComponentState.Processing, TransitionReason.RunEnabled);
            }
            else
            {
                this.SetPersistentRuntimeMessage("needs_run", GH_RuntimeMessageLevel.Warning, "The component needs to recalculate. Set Run to true!", false);
                this.ClearDataOnly();
            }
        }

        /// <summary>
        /// Handles the Processing state.
        /// </summary>
        /// <param name="DA">The data access object.</param>
        private void OnStateProcessing(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[{this.GetType().Name}] OnStateProcessing");

            // Delegate to AsyncComponentBase
            base.SolveInstance(DA);
        }

        /// <summary>
        /// Handles the Cancelled state.
        /// </summary>
        /// <param name="DA">The data access object.</param>
        private void OnStateCancelled(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[{this.GetType().Name}] OnStateCancelled");

            this.ApplyPersistentRuntimeMessages();
            this.SetPersistentRuntimeMessage("cancelled", GH_RuntimeMessageLevel.Error, "The execution was manually cancelled", false);

            bool run = false;
            DA.GetData("Run?", ref run);

            // Check for changes using StateManager
            var changedInputs = this.StateManager.GetChangedInputs();

            // If Run changed to true and no other inputs changed, transition to Processing
            if (changedInputs.Count == 1 && changedInputs[0] == "Run?" && run)
            {
                this.StateManager.RequestTransition(ComponentState.Processing, TransitionReason.RunEnabled);
            }
        }

        /// <summary>
        /// Handles the Error state.
        /// </summary>
        /// <param name="DA">The data access object.</param>
        private void OnStateError(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[{this.GetType().Name}] OnStateError");
            this.ApplyPersistentRuntimeMessages();
        }

        #endregion

        #region Debounce

        /// <summary>
        /// Minimum debounce time in milliseconds.
        /// </summary>
        private const int MINDEBOUNCETIME = 1000;

        /// <summary>
        /// Gets the debounce time from settings.
        /// </summary>
        /// <returns>The debounce time in milliseconds.</returns>
        protected virtual int GetDebounceTime()
        {
            var settingsDebounceTime = SmartHopperSettings.Load().DebounceTime;
            return Math.Max(settingsDebounceTime, MINDEBOUNCETIME);
        }

        /// <summary>
        /// Restarts the debounce timer with the default target state.
        /// </summary>
        protected void RestartDebounceTimer()
        {
            this.StateManager.StartDebounce(ComponentState.NeedsRun, this.GetDebounceTime());
        }

        /// <summary>
        /// Restarts the debounce timer with a specific target state.
        /// </summary>
        /// <param name="targetState">The state to transition to after debounce.</param>
        protected void RestartDebounceTimer(ComponentState targetState)
        {
            this.StateManager.StartDebounce(targetState, this.GetDebounceTime());
        }

        #endregion

        #region Runtime Messages

        /// <summary>
        /// Adds or updates a runtime message and optionally transitions to Error state.
        /// </summary>
        /// <param name="key">Unique identifier for the message.</param>
        /// <param name="level">The message severity level.</param>
        /// <param name="message">The message content.</param>
        /// <param name="transitionToError">If true and level is Error, transitions to Error state.</param>
        protected void SetPersistentRuntimeMessage(string key, GH_RuntimeMessageLevel level, string message, bool transitionToError = true)
        {
            Debug.WriteLine($"[{this.GetType().Name}] [PersistentMessage] key='{key}', level={level}, message='{message}'");
            this.runtimeMessages[key] = (level, message);

            if (transitionToError && level == GH_RuntimeMessageLevel.Error)
            {
                this.StateManager.RequestTransition(ComponentState.Error, TransitionReason.Error);
            }
            else
            {
                this.ApplyPersistentRuntimeMessages();
            }
        }

        /// <summary>
        /// Clears a specific runtime message by its key.
        /// </summary>
        /// <param name="key">The unique identifier of the message to clear.</param>
        /// <returns>True if the message was found and cleared.</returns>
        protected bool ClearOnePersistentRuntimeMessage(string key)
        {
            var removed = this.runtimeMessages.Remove(key);
            if (removed)
            {
                this.ClearRuntimeMessages();
                this.ApplyPersistentRuntimeMessages();
            }

            return removed;
        }

        /// <summary>
        /// Clears all runtime messages.
        /// </summary>
        protected void ClearPersistentRuntimeMessages()
        {
            this.runtimeMessages.Clear();
            this.ClearRuntimeMessages();
        }

        /// <summary>
        /// Applies stored runtime messages to the component.
        /// </summary>
        private void ApplyPersistentRuntimeMessages()
        {
            Debug.WriteLine($"[{this.GetType().Name}] Applying {this.runtimeMessages.Count} runtime messages");
            foreach (var (level, message) in this.runtimeMessages.Values)
            {
                this.AddRuntimeMessage(level, message);
            }
        }

        #endregion

        #region Progress Tracking

        /// <summary>
        /// Initializes progress tracking with the specified total count.
        /// </summary>
        /// <param name="total">The total number of items to process.</param>
        protected virtual void InitializeProgress(int total)
        {
            this.ProgressInfo.Total = total;
            this.ProgressInfo.Current = 1;
        }

        /// <summary>
        /// Updates the current progress and triggers a UI refresh.
        /// </summary>
        /// <param name="current">The current item being processed.</param>
        protected virtual void UpdateProgress(int current)
        {
            this.ProgressInfo.UpdateCurrent(current);
            this.Message = this.GetStateMessage();

            Rhino.RhinoApp.InvokeOnUiThread(() =>
            {
                this.OnDisplayExpired(false);
            });
        }

        /// <summary>
        /// Resets progress tracking.
        /// </summary>
        protected virtual void ResetProgress()
        {
            this.ProgressInfo.Reset();
        }

        /// <summary>
        /// Gets the current state message with progress information.
        /// </summary>
        /// <returns>A formatted state message string.</returns>
        public virtual string GetStateMessage()
        {
            return this.StateManager.CurrentState.ToMessageString(this.ProgressInfo);
        }

        /// <summary>
        /// Runs data-tree processing using the unified runner.
        /// </summary>
        protected async Task<Dictionary<string, GH_Structure<U>>> RunProcessingAsync<T, U>(
            Dictionary<string, GH_Structure<T>> trees,
            Func<Dictionary<string, List<T>>, Task<Dictionary<string, List<U>>>> function,
            DataTree.ProcessingOptions options,
            CancellationToken token = default)
            where T : IGH_Goo
            where U : IGH_Goo
        {
            var (dataCount, iterationCount) = DataTree.DataTreeProcessor.CalculateProcessingMetrics(trees, options);

            this.SetDataCount(dataCount);
            this.InitializeProgress(iterationCount);

            var result = await DataTree.DataTreeProcessor.RunAsync(
                trees,
                function,
                options,
                progressCallback: (current, total) =>
                {
                    this.UpdateProgress(current);
                },
                token).ConfigureAwait(false);

            return result;
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Writes the component's persistent data to the Grasshopper file.
        /// </summary>
        /// <param name="writer">The writer to use for serialization.</param>
        /// <returns>True if successful.</returns>
        public override bool Write(GH_IWriter writer)
        {
            if (!base.Write(writer))
            {
                return false;
            }

            try
            {
                // Store input hashes from StateManager
                var hashes = this.StateManager.GetCommittedHashes();
                foreach (var kvp in hashes)
                {
                    writer.SetInt32($"InputHash_{kvp.Key}", kvp.Value);
                }

                var branchCounts = this.StateManager.GetCommittedBranchCounts();
                foreach (var kvp in branchCounts)
                {
                    writer.SetInt32($"InputBranchCount_{kvp.Key}", kvp.Value);
                }

                // Build GUID-keyed structure dictionary for v2 persistence
                var outputsByGuid = new Dictionary<Guid, GH_Structure<IGH_Goo>>();
                foreach (var p in this.Params.Output)
                {
                    if (!this.persistentOutputs.TryGetValue(p.Name, out var value))
                    {
                        continue;
                    }

                    if (value is IGH_Structure structure)
                    {
                        var tree = ConvertToGooTree(structure);
                        outputsByGuid[p.InstanceGuid] = tree;
                    }
                    else if (p.Access == GH_ParamAccess.list && value is System.Collections.IEnumerable enumerable && value is not string)
                    {
                        var tree = new GH_Structure<IGH_Goo>();
                        var path = new GH_Path(0, 0);
                        foreach (var item in enumerable)
                        {
                            if (item == null)
                            {
                                continue;
                            }

                            var goo = item as IGH_Goo ?? GH_Convert.ToGoo(item) ?? new GH_String(item.ToString());
                            tree.Append(goo, path);
                        }

                        outputsByGuid[p.InstanceGuid] = tree;
                    }
                    else
                    {
                        var tree = new GH_Structure<IGH_Goo>();
                        var path = new GH_Path(0, 0);
                        var goo = value as IGH_Goo ?? GH_Convert.ToGoo(value) ?? new GH_String(value?.ToString() ?? string.Empty);
                        tree.Append(goo, path);
                        outputsByGuid[p.InstanceGuid] = tree;
                    }
                }

                var persistence = new GHPersistenceService();
                persistence.WriteOutputsV2(writer, this, outputsByGuid);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads the component's persistent data from the Grasshopper file.
        /// </summary>
        /// <param name="reader">The reader to use for deserialization.</param>
        /// <returns>True if successful.</returns>
        public override bool Read(GH_IReader reader)
        {
            if (!base.Read(reader))
            {
                return false;
            }

            // Begin restoration - suppresses input change detection
            this.StateManager.BeginRestoration();

            try
            {
                // Restore input hashes
                var hashes = new Dictionary<string, int>();
                var branchCounts = new Dictionary<string, int>();

                foreach (var item in reader.Items)
                {
                    var key = item.Name;
                    if (key.StartsWith("InputHash_"))
                    {
                        string paramName = key.Substring("InputHash_".Length);
                        hashes[paramName] = reader.GetInt32(key);
                    }
                    else if (key.StartsWith("InputBranchCount_"))
                    {
                        string paramName = key.Substring("InputBranchCount_".Length);
                        branchCounts[paramName] = reader.GetInt32(key);
                    }
                }

                // Restore hashes to StateManager
                this.StateManager.RestoreCommittedHashes(hashes, branchCounts);

                // Clear previous outputs
                this.persistentOutputs.Clear();

                // Try safe V2 restore
                var persistence = new GHPersistenceService();
                var v2Outputs = persistence.ReadOutputsV2(reader, this);
                if (v2Outputs != null && v2Outputs.Count > 0)
                {
                    foreach (var p in this.Params.Output)
                    {
                        if (v2Outputs.TryGetValue(p.InstanceGuid, out var tree))
                        {
                            this.persistentOutputs[p.Name] = tree;
                            Debug.WriteLine($"[StatefulComponentBase] [Read] Restored output '{p.Name}' paths={tree.PathCount}");
                        }
                    }
                }
                else if (PersistenceConstants.EnableLegacyRestore)
                {
                    // Legacy fallback
                    this.RestoreLegacyOutputs(reader);
                }

                Debug.WriteLine($"[StatefulComponentBase] [Read] Restored with {this.persistentOutputs.Count} outputs");

                return true;
            }
            finally
            {
                // End restoration - suppression stays active for first solve
                this.StateManager.EndRestoration();
            }
        }

        /// <summary>
        /// Restores legacy output format from older files.
        /// </summary>
        /// <param name="reader">The reader.</param>
        private void RestoreLegacyOutputs(GH_IReader reader)
        {
            foreach (var item in reader.Items)
            {
                string key = item.Name;
                if (!key.StartsWith("Value_"))
                {
                    continue;
                }

                string paramName = key.Substring("Value_".Length);

                try
                {
                    string typeName = reader.GetString($"Type_{paramName}");
                    if (string.IsNullOrWhiteSpace(typeName))
                    {
                        continue;
                    }

                    Type type = Type.GetType(typeName);
                    if (type == null)
                    {
                        continue;
                    }

                    byte[] chunkBytes = reader.GetByteArray($"Value_{paramName}");
                    if (chunkBytes == null || chunkBytes.Length == 0)
                    {
                        continue;
                    }

                    var chunk = new GH_LooseChunk($"Value_{paramName}");
                    chunk.Deserialize_Binary(chunkBytes);

                    var instance = Activator.CreateInstance(type);
                    var readMethod = type.GetMethod("Read");
                    if (instance == null || readMethod == null)
                    {
                        continue;
                    }

                    readMethod.Invoke(instance, new object[] { chunk });
                    this.persistentOutputs[paramName] = instance;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StatefulComponentBase] [Read] Legacy restore failed for '{paramName}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Restores all persistent outputs to their respective parameters.
        /// </summary>
        /// <param name="DA">The data access object.</param>
        protected virtual void RestorePersistentOutputs(IGH_DataAccess DA)
        {
            Debug.WriteLine("[StatefulComponentBase] Restoring persistent outputs");

            for (int i = 0; i < this.Params.Output.Count; i++)
            {
                var param = this.Params.Output[i];
                var savedValue = this.GetPersistentOutput<object>(param.Name);
                if (savedValue == null)
                {
                    continue;
                }

                try
                {
                    this.RestoreOutputParameter(param, savedValue, DA, i);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StatefulComponentBase] Failed to restore '{param.Name}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Restores a single output parameter value.
        /// </summary>
        private void RestoreOutputParameter(IGH_Param param, object savedValue, IGH_DataAccess DA, int paramIndex)
        {
            if (savedValue is IGH_Structure structure)
            {
                if (param.Access == GH_ParamAccess.tree)
                {
                    this.SetPersistentOutput(param.Name, structure, DA);
                }
                else if (param.Access == GH_ParamAccess.list)
                {
                    this.SetPersistentOutput(param.Name, structure, DA);
                }
                else
                {
                    IGH_Goo first = null;
                    foreach (var path in structure.Paths)
                    {
                        var branch = structure.get_Branch(path);
                        if (branch != null && branch.Count > 0)
                        {
                            first = branch[0] as IGH_Goo ?? GH_Convert.ToGoo(branch[0]);
                            break;
                        }
                    }

                    if (first != null)
                    {
                        this.SetPersistentOutput(param.Name, first, DA);
                    }
                }
            }
            else if (param.Access == GH_ParamAccess.list && savedValue is System.Collections.IEnumerable enumerable && savedValue is not string)
            {
                this.SetPersistentOutput(param.Name, enumerable, DA);
            }
            else
            {
                IGH_Goo gooValue;
                if (savedValue is IGH_Goo existingGoo)
                {
                    gooValue = existingGoo;
                }
                else
                {
                    var gooType = param.Type;
                    gooValue = GH_Convert.ToGoo(savedValue);
                    if (gooValue == null)
                    {
                        gooValue = (IGH_Goo)Activator.CreateInstance(gooType);
                        gooValue.CastFrom(savedValue);
                    }
                }

                this.SetPersistentOutput(param.Name, gooValue, DA);
            }
        }

        /// <summary>
        /// Stores a value in persistent storage and sets the output.
        /// </summary>
        /// <param name="paramName">Name of the parameter.</param>
        /// <param name="value">Value to store.</param>
        /// <param name="DA">The data access object.</param>
        protected void SetPersistentOutput(string paramName, object value, IGH_DataAccess DA)
        {
            try
            {
                var param = this.Params.Output.FirstOrDefault(p => p.Name == paramName);
                var paramIndex = this.Params.Output.IndexOf(param);
                if (param == null)
                {
                    return;
                }

                // Extract inner value if wrapped
                value = ExtractGHObjectWrapperValue(value);

                // Store in persistent storage
                this.persistentOutputs[paramName] = value;

                if (value != null)
                {
                    this.persistentDataTypes[paramName] = value.GetType();
                }
                else
                {
                    this.persistentDataTypes.Remove(paramName);
                }

                // Set the data through DA
                if (DA != null)
                {
                    this.SetOutputData(param, paramIndex, value, DA);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StatefulComponentBase] Failed to set output '{paramName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Sets output data based on parameter access type.
        /// </summary>
        private void SetOutputData(IGH_Param param, int paramIndex, object value, IGH_DataAccess DA)
        {
            if (param.Access == GH_ParamAccess.tree)
            {
                if (value is IGH_Structure tree)
                {
                    bool hasItems = tree.PathCount > 0 && tree.Paths.Any(p =>
                    {
                        var branch = tree.get_Branch(p);
                        return branch != null && branch.Count > 0;
                    });

                    if (hasItems)
                    {
                        DA.SetDataTree(paramIndex, tree);
                    }
                }
                else
                {
                    var newTree = new GH_Structure<IGH_Goo>();
                    if (!(value is IGH_Goo))
                    {
                        value = GH_Convert.ToGoo(value);
                    }

                    if (value is IGH_Goo goo)
                    {
                        newTree.Append(goo, new GH_Path(0));
                        DA.SetDataTree(paramIndex, newTree);
                    }
                }
            }
            else if (param.Access == GH_ParamAccess.list)
            {
                if (value is IGH_Structure structValue)
                {
                    var list = new List<IGH_Goo>();
                    foreach (var path in structValue.Paths)
                    {
                        var branch = structValue.get_Branch(path);
                        if (branch == null)
                        {
                            continue;
                        }

                        foreach (var item in branch)
                        {
                            if (item == null)
                            {
                                continue;
                            }

                            var gooItem = item as IGH_Goo ?? GH_Convert.ToGoo(item);
                            if (gooItem != null)
                            {
                                list.Add(gooItem);
                            }
                        }
                    }

                    DA.SetDataList(paramIndex, list);
                }
                else if (value is System.Collections.IEnumerable enumerable && !(value is string))
                {
                    var list = new List<IGH_Goo>();
                    foreach (var item in enumerable)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        var gooItem = item as IGH_Goo ?? GH_Convert.ToGoo(item);
                        if (gooItem != null)
                        {
                            list.Add(gooItem);
                        }
                    }

                    DA.SetDataList(paramIndex, list);
                }
                else
                {
                    var single = value as IGH_Goo ?? GH_Convert.ToGoo(value);
                    if (single != null)
                    {
                        DA.SetDataList(paramIndex, new List<IGH_Goo> { single });
                    }
                }
            }
            else
            {
                if (!(value is IGH_Goo))
                {
                    value = GH_Convert.ToGoo(value);
                }

                DA.SetData(paramIndex, value);
            }
        }

        /// <summary>
        /// Retrieves a value from persistent storage.
        /// </summary>
        protected T GetPersistentOutput<T>(string paramName, T defaultValue = default)
        {
            if (this.persistentOutputs.TryGetValue(paramName, out object value) && value is T typedValue)
            {
                return typedValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Extracts inner value from GH_ObjectWrapper.
        /// </summary>
        private static object ExtractGHObjectWrapperValue(object value)
        {
            if (value?.GetType()?.FullName == "Grasshopper.Kernel.Types.GH_ObjectWrapper")
            {
                var valueProperty = value.GetType().GetProperty("Value");
                if (valueProperty != null)
                {
                    return valueProperty.GetValue(value);
                }
            }

            return value;
        }

        /// <summary>
        /// Converts an IGH_Structure to GH_Structure of IGH_Goo.
        /// </summary>
        private static GH_Structure<IGH_Goo> ConvertToGooTree(IGH_Structure src)
        {
            var dst = new GH_Structure<IGH_Goo>();
            if (src == null)
            {
                return dst;
            }

            foreach (var path in src.Paths)
            {
                var branch = src.get_Branch(path);
                if (branch == null)
                {
                    dst.EnsurePath(path);
                    continue;
                }

                foreach (var item in branch)
                {
                    IGH_Goo goo = item as IGH_Goo;
                    if (goo == null)
                    {
                        goo = GH_Convert.ToGoo(item);
                        if (goo == null)
                        {
                            goo = new GH_String(item?.ToString() ?? string.Empty);
                        }
                    }

                    dst.Append(goo, path);
                }
            }

            return dst;
        }

        #endregion

        #region Hash Calculation

        /// <summary>
        /// Calculates the hash for a single input parameter's data.
        /// </summary>
        private static int CalculatePersistentDataHash(IGH_Param param, out int branchCount)
        {
            var data = param.VolatileData;
            int currentHash = 0;
            branchCount = data.PathCount;

            foreach (var branch in data.Paths)
            {
                int branchHash = StableStringHash(branch.ToString());
                foreach (var item in data.get_Branch(branch))
                {
                    branchHash = CombineHashCodes(branchHash, StableVolatileItemHash(item));
                }

                currentHash = CombineHashCodes(currentHash, branchHash);
            }

            return currentHash;
        }

        /// <summary>
        /// Combines two hash codes.
        /// </summary>
        private static int CombineHashCodes(int h1, int h2)
        {
            unchecked
            {
                return ((h1 << 5) + h1) ^ h2;
            }
        }

        /// <summary>
        /// Computes a deterministic 32-bit hash for a string (FNV-1a), suitable for persisted change tracking.
        /// </summary>
        private static int StableStringHash(string value)
        {
            if (value == null)
            {
                return 0;
            }

            unchecked
            {
                const int offsetBasis = unchecked((int)2166136261);
                const int prime = 16777619;

                int hash = offsetBasis;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= prime;
                }

                return hash;
            }
        }

        /// <summary>
        /// Computes a deterministic hash for a single volatile data item.
        /// Avoids using object.GetHashCode(), which is not stable across sessions for many types.
        /// </summary>
        private static int StableVolatileItemHash(object item)
        {
            if (item == null)
            {
                return 0;
            }

            if (item is IGH_Goo goo)
            {
                object scriptValue = null;
                try
                {
                    if (goo.IsValid)
                    {
                        scriptValue = goo.ScriptVariable();
                    }
                }
                catch
                {
                    scriptValue = null;
                }

                int typeHash = StableStringHash(goo.GetType().FullName);
                int valueHash = StableVolatileValueHash(scriptValue ?? goo.ToString());
                return CombineHashCodes(typeHash, valueHash);
            }

            return StableVolatileValueHash(item);
        }

        /// <summary>
        /// Computes a deterministic hash for common primitive/script values.
        /// </summary>
        private static int StableVolatileValueHash(object value)
        {
            if (value == null)
            {
                return 0;
            }

            unchecked
            {
                switch (value)
                {
                    case int i:
                        return i;
                    case long l:
                        return CombineHashCodes((int)l, (int)(l >> 32));
                    case bool b:
                        return b ? 1 : 0;
                    case double d:
                        {
                            long bits = BitConverter.DoubleToInt64Bits(d);
                            return CombineHashCodes((int)bits, (int)(bits >> 32));
                        }
                    case float f:
                        {
                            int bits = BitConverter.SingleToInt32Bits(f);
                            return bits;
                        }
                    case string s:
                        return StableStringHash(s);
                    default:
                        return StableStringHash(value.ToString());
                }
            }
        }

        /// <summary>
        /// Determines which inputs have changed since the last successful run.
        /// Maintained for API compatibility - delegates to StateManager.
        /// </summary>
        protected virtual List<string> InputsChanged()
        {
            return this.StateManager.GetChangedInputs().ToList();
        }

        /// <summary>
        /// Checks if a specific input has changed.
        /// </summary>
        protected bool InputsChanged(string inputName, bool exclusively = true)
        {
            var changedInputs = this.InputsChanged();

            if (exclusively)
            {
                return changedInputs.Count == 1 && changedInputs.Any(name => name == inputName);
            }
            else
            {
                return changedInputs.Any(name => name == inputName);
            }
        }

        /// <summary>
        /// Checks if any of the specified inputs have changed.
        /// </summary>
        protected bool InputsChanged(IEnumerable<string> inputNames, bool exclusively = true)
        {
            var changedInputs = this.InputsChanged();
            var inputNamesList = inputNames.ToList();

            if (exclusively)
            {
                return changedInputs.Count > 0 && !changedInputs.Except(inputNamesList).Any();
            }
            else
            {
                return changedInputs.Any(changed => inputNamesList.Contains(changed));
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Clears persistent storage and output parameters.
        /// </summary>
        protected override void ClearDataOnly()
        {
            this.persistentOutputs.Clear();
            base.ClearDataOnly();
        }

        /// <summary>
        /// Expires downstream objects when appropriate.
        /// </summary>
        protected override void ExpireDownStreamObjects()
        {
            var currentState = this.StateManager.CurrentState;
            bool allowDuringProcessing = currentState == ComponentState.Processing
                                         && !this.InPreSolve
                                         && (this.SetData == 1 || (this.persistentOutputs != null && this.persistentOutputs.Count > 0));

            if (currentState == ComponentState.Completed || allowDuringProcessing)
            {
                base.ExpireDownStreamObjects();
            }
        }

        /// <summary>
        /// Requests cancellation and transitions to Cancelled state.
        /// </summary>
        public override void RequestTaskCancellation()
        {
            base.RequestTaskCancellation();
            this.StateManager.RequestTransition(ComponentState.Cancelled, TransitionReason.Cancelled);
        }

#if DEBUG
        /// <summary>
        /// Appends debug menu items.
        /// </summary>
        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, $"Debug: State = {this.StateManager.CurrentState}", null);
            Menu_AppendItem(menu, "Debug: Force Completed", (s, e) =>
            {
                this.StateManager.ForceState(ComponentState.Completed);
                this.ExpireSolution(true);
            });
            Menu_AppendItem(menu, "Debug: Force NeedsRun", (s, e) =>
            {
                this.StateManager.ForceState(ComponentState.NeedsRun);
                this.ExpireSolution(true);
            });
            Menu_AppendItem(menu, "Debug: Reset StateManager", (s, e) =>
            {
                this.StateManager.Reset();
                this.ExpireSolution(true);
            });
        }
#endif

        #endregion
    }
}
