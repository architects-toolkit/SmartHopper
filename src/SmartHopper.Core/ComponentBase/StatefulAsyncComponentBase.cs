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
 * Base class for all stateful asynchronous SmartHopper components.
 * This class provides the fundamental structure for components that need to perform
 * asynchronous, showing an State message, while maintaining
 * Grasshopper's component lifecycle.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SmartHopper.Infrastructure.Settings;
using Timer = System.Threading.Timer;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for stateful asynchronous Grasshopper components.
    /// Provides integrated state management, parallel processing, messaging, and persistence capabilities.
    /// </summary>
    public abstract class StatefulAsyncComponentBase : AsyncComponentBase
    {
        /// <summary>
        /// Gets or sets a value indicating whether the component should only run when inputs change.
        /// If true (default), the component will only run when inputs have changed and Run is true.
        /// If false, the component will run whenever the Run parameter is set to true,
        /// regardless of whether inputs have changed.
        /// </summary>
        public bool RunOnlyOnInputChanges { get; set; } = true;

        #region CONSTRUCTOR

        /// <summary>
        /// Initializes a new instance of the <see cref="StatefulAsyncComponentBase"/> class.
        /// Creates a new instance of the stateful async component.
        /// </summary>
        /// <param name="name">The component's display name.</param>
        /// <param name="nickname">The component's nickname.</param>
        /// <param name="description">Description of the component's functionality.</param>
        /// <param name="category">Category in the Grasshopper toolbar.</param>
        /// <param name="subCategory">Subcategory in the Grasshopper toolbar.</param>
        protected StatefulAsyncComponentBase(
            string name,
            string nickname,
            string description,
            string category,
            string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            this.persistentOutputs = new Dictionary<string, object>();
            this.persistentDataTypes = new Dictionary<string, Type>();
            this.previousInputHashes = new Dictionary<string, int>();
            this.previousInputBranchCounts = new Dictionary<string, int>();

            // Initialize timer
            // Actions defined here will happen after the debounce time
            this.debounceTimer = new Timer(
                (state) =>
            {
                lock (this.timerLock)
                {
                    var targetState = this.debounceTargetState;

                    // if (!_run)
                    // {
                    //     targetState = ComponentState.NeedsRun;
                    // }
                    Debug.WriteLine($"[{this.GetType().Name}] Debounce timer elapsed - Inputs stable, transitioning to {targetState}");
                    Debug.WriteLine($"[{this.GetType().Name}] Debounce timer elapsed - Changes during debounce: {this.inputChangedDuringDebounce}");
                    Rhino.RhinoApp.InvokeOnUiThread(() =>
                    {
                        this.TransitionTo(targetState, this.lastDA);
                    });

                    if (this.inputChangedDuringDebounce > 0 && this.run)
                    {
                        Rhino.RhinoApp.InvokeOnUiThread(() =>
                        {
                            this.ExpireSolution(true);
                        });
                    }

                    // Reset default values after debounce
                    Debug.WriteLine($"[{this.GetType().Name}] Debounce timer elapsed - Resetting debounce values");

                    // Reset debounce values
                    this.inputChangedDuringDebounce = 0;
                    this.debounceTargetState = ComponentState.Waiting;
                }
            }, null, Timeout.Infinite, Timeout.Infinite); // Initially disabled
        }

        #endregion

        // --------------------------------------------------
        //                COMPONENT DEFINITION
        // --------------------------------------------------
        //
        // This section of code is responsible for managing
        // the component's lifecycle and state transitions,
        // implementing the necessary methods for a
        // Grasshopper Component.
        #region PARAMS

        private bool run;

        public bool Run => this.run;

        /// <summary>
        /// Registers input parameters for the component.
        /// </summary>
        /// <param name="pManager">The input parameter manager.</param>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // Allow derived classes to add their specific inputs
            this.RegisterAdditionalInputParams(pManager);

            pManager.AddBooleanParameter("Run?", "R", "Set this parameter to true to run the component.", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Register component-specific input parameters, to define in derived classes.
        /// </summary>
        /// <param name="pManager">The input parameter manager.</param>
        protected abstract void RegisterAdditionalInputParams(GH_InputParamManager pManager);

        /// <summary>
        /// Registers output parameters for the component.
        /// </summary>
        /// <param name="pManager">The output parameter manager.</param>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // Allow derived classes to add their specific outputs
            this.RegisterAdditionalOutputParams(pManager);
        }

        /// <summary>
        /// Register component-specific output parameters, to define in derived classes.
        /// </summary>
        /// <param name="pManager">The output parameter manager.</param>
        protected abstract void RegisterAdditionalOutputParams(GH_OutputParamManager pManager);

        #endregion

        #region LIFECYCLE

        protected override void BeforeSolveInstance()
        {
            if (this.currentState == ComponentState.Processing && !this.run)
            {
                Debug.WriteLine("[StatefulAsyncComponentBase] Processing state... jumping to SolveInstance");
                return; // Jump to SolveInstance, prevent resetting data
            }

            base.BeforeSolveInstance();
        }

        /// <summary>
        /// Main solving method for the component.
        /// Handles the execution flow and persistence of results.
        /// </summary>
        /// <param name="DA">The data access object.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // If we just restored from file, and no outputs were restored,
            // transition to NeedsRun state
            if (this.justRestoredFromFile && this.persistentOutputs.Count == 0)
            {
                this.justRestoredFromFile = false;
                this.TransitionTo(ComponentState.NeedsRun, DA);
                return;
            }
            
            this.lastDA = DA;

            // Store Run parameter
            bool run = false;
            DA.GetData("Run?", ref run);
            this.run = run;

            Debug.WriteLine($"[{this.GetType().Name}] SolveInstance - Current State: {this.currentState}, InPreSolve: {this.InPreSolve}, State: {this._state}, SetData: {this._setData}, Workers: {this.Workers.Count}, Changes during debounce: {this.inputChangedDuringDebounce}, Run: {this.run}, IsTransitioning: {this.isTransitioning}, Pending Transitions: {this.pendingTransitions.Count}");

            // Execute the appropriate state handler
            switch (this.currentState)
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

            // If inputs changed...
            switch (this.currentState)
            {
                case ComponentState.Completed:
                case ComponentState.Waiting:
                case ComponentState.Cancelled:
                case ComponentState.Error:
                    // Check if inputs changed
                    var changedInputs = this.InputsChanged();

                    // If only the Run parameter changed to false, stay in Completed state
                    if (this.InputsChanged("Run?", true) && !this.run)
                    {
                        Debug.WriteLine($"[{this.GetType().Name}] Only Run parameter changed to false, staying in Completed state");
                    }

                    // If only the Run parameter changed to true, restart debounce timer with target to the Waiting state to output the results again
                    else if (this.InputsChanged("Run?", true) && this.run)
                    {
                        Debug.WriteLine($"[{this.GetType().Name}] Only Run parameter changed to true, restarting debounce timer with target state Waiting");

                        if (!this.RunOnlyOnInputChanges)
                        {
                            // Always transition to Processing state regardless of input changes
                            Debug.WriteLine($"[{this.GetType().Name}] Component set to always run when Run is true, transitioning to Processing state");
                            this.TransitionTo(ComponentState.Processing, DA);
                        }
                        else
                        {
                            // Default behavior - transition to Waiting state
                            this.TransitionTo(ComponentState.Waiting, DA);
                        }
                    }

                    // If any other input changed, and run is false
                    else if (changedInputs.Any() && !this.run)
                    {
                        Debug.WriteLine($"[{this.GetType().Name}] Inputs changed, restarting debounce timer with target state NeedsRun");
                        this.RestartDebounceTimer(ComponentState.NeedsRun);
                    }

                    // If any other input changed, and run is true
                    else if (changedInputs.Any() && this.run)
                    {
                        Debug.WriteLine($"[{this.GetType().Name}] Inputs changed, restarting debounce timer");
                        this.RestartDebounceTimer(ComponentState.Processing);
                    }

                    break;
                default:
                    break;
            }

            this.ResetInputChanged();
        }

        protected override void OnWorkerCompleted()
        {
            // Update input hashes before transitioning to prevent false input changes
            this.CalculatePersistentDataHashes();
            this.TransitionTo(ComponentState.Completed, this.lastDA);
            base.OnWorkerCompleted();
            Debug.WriteLine("[StatefulAsyncComponentBase] Worker completed, expiring solution");
            this.ExpireSolution(true);
        }

        #endregion

        // --------------------------------------------------
        //                  STATE MANAGEMENT
        // --------------------------------------------------
        //
        // Implement State Management
        #region STATE

        // PRIVATE FIELDS

        // Default state, field to store the current state
        private ComponentState currentState = ComponentState.Completed;
        private readonly object stateLock = new ();
        private bool isTransitioning;
        private TaskCompletionSource<bool> stateCompletionSource;
        private Queue<ComponentState> pendingTransitions = new ();
        private IGH_DataAccess lastDA;
        
        // Flag to track if component was just restored from file with existing outputs
        private bool justRestoredFromFile = false;

        // PUBLIC PROPERTIES

        /// <summary>
        /// Gets the current state of the component.
        /// </summary>
        public ComponentState CurrentState => this.currentState;

        private async Task ProcessTransition(ComponentState newState, IGH_DataAccess? DA = null)
        {
            var oldState = this.currentState;
            Debug.WriteLine($"[{this.GetType().Name}] Attempting transition from {oldState} to {newState}");

            if (!this.IsValidTransition(newState))
            {
                Debug.WriteLine($"[{this.GetType().Name}] Invalid state transition from {oldState} to {newState}");
                return;
            }

            this.currentState = newState;
            Debug.WriteLine($"[{this.GetType().Name}] State transition: {oldState} -> {newState}");
            this.Message = newState.ToMessageString();

            this.stateCompletionSource = new TaskCompletionSource<bool>();

            // Clear messages only when entering NeedsRun or Processing from a different state
            if ((newState == ComponentState.NeedsRun || newState == ComponentState.Processing) &&
                oldState != ComponentState.NeedsRun && oldState != ComponentState.Processing)
            {
                this.ClearPersistentRuntimeMessages();
            }

            // Actions here only happen when transitioning
            // Action in the OnState___ methods happen on every solve
            switch (newState)
            {
                case ComponentState.Completed:
                    this.OnStateCompleted(DA);
                    break;
                case ComponentState.Waiting:
                    //// OnStateWaiting is only called in SolveInstance
                    // OnStateWaiting(DA);
                    break;
                case ComponentState.NeedsRun:
                    this.OnStateNeedsRun(DA);
                    this.OnDisplayExpired(true);
                    break;
                case ComponentState.Processing:
                    // OnStateProcessing(DA);
                    break;
                case ComponentState.Cancelled:
                    this.OnStateCancelled(DA);
                    this.OnDisplayExpired(true);
                    break;
                case ComponentState.Error:
                    this.OnStateError(DA);
                    break;
            }

            await this.stateCompletionSource.Task.ConfigureAwait(false);
            Debug.WriteLine($"[{this.GetType().Name}] Completed transition {oldState} -> {newState}");
            return;
        }

        protected void CompleteStateTransition()
        {
            this.stateCompletionSource?.TrySetResult(true);
        }

        private async void TransitionTo(ComponentState newState, IGH_DataAccess? DA = null)
        {
            if (DA == null)
            {
                DA = this.lastDA;
            }

            lock (this.stateLock)
            {
                if (this.isTransitioning && newState == ComponentState.Completed)
                {
                    Debug.WriteLine($"[{this.GetType().Name}] Queuing transition to {newState} while in {this.currentState}");
                    this.pendingTransitions.Enqueue(newState);
                    return;
                }

                this.isTransitioning = true;
            }

            try
            {
                await this.ProcessTransition(newState, DA).ConfigureAwait(false);
            }
            finally
            {
                lock (this.stateLock)
                {
                    this.isTransitioning = false;
                    if (this.pendingTransitions.Count > 0)
                    {
                        var nextState = this.pendingTransitions.Dequeue();
                        Debug.WriteLine($"[{this.GetType().Name}] Processing queued transition to {nextState}");
                        this.TransitionTo(nextState, DA);
                    }
                }
            }
        }

        private void OnStateCompleted(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[{this.GetType().Name}] OnStateCompleted, _state: {this._state}, InPreSolve: {this.InPreSolve}, SetData: {this._setData}, Workers: {this.Workers.Count}, Changes during debounce: {this.inputChangedDuringDebounce}");

            // Ensure message is set correctly for Completed state
            // This is especially important after file restoration when ProcessTransition might not be called
            this.Message = ComponentState.Completed.ToMessageString();

            // Reapply runtime messages in completed state
            this.ApplyPersistentRuntimeMessages();

            // Restore data from persistent storage, necessary when opening the file
            this.RestorePersistentOutputs(DA);

            this.CompleteStateTransition();
        }

        private void OnStateWaiting(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[{this.GetType().Name}] OnStateWaiting");

            // Reapply runtime messages in completed state
            this.ApplyPersistentRuntimeMessages();

            // Restore data from persistent storage, necessary when opening the file
            this.RestorePersistentOutputs(DA);

            this.CompleteStateTransition();
        }

        private void OnStateNeedsRun(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[{this.GetType().Name}] OnStateNeedsRun");

            // Check Run parameter
            bool run = false;
            DA.GetData("Run?", ref run);

            if (run)
            {
                // Transition to Processing and let base class handle async work
                this.TransitionTo(ComponentState.Processing, DA);

                // Clear the "needs_run" message if it exists
                this.ClearOnePersistentRuntimeMessage("needs_run");
            }
            else
            {
                this.SetPersistentRuntimeMessage("needs_run", GH_RuntimeMessageLevel.Warning, "The component needs to recalculate. Set Run to true!", false);
                this.ClearDataOnly();
            }

            this.CompleteStateTransition();
        }

        private void OnStateProcessing(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[{this.GetType().Name}] OnStateProcessing");

            // The base AsyncComponentBase handles the actual processing
            // When done it will call OnWorkerCompleted which transitions to Completed
            base.SolveInstance(DA);

            this.CompleteStateTransition();
        }

        private void OnStateCancelled(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[{this.GetType().Name}] OnStateCancelled");

            // Reapply runtime messages in cancelled state
            this.ApplyPersistentRuntimeMessages();
            this.SetPersistentRuntimeMessage("cancelled", GH_RuntimeMessageLevel.Error, "The execution was manually cancelled", false);

            // Check if inputs changed
            var changedInputs = this.InputsChanged();

            // Check Run parameter
            bool run = false;
            DA.GetData("Run?", ref run);

            // If any input changed (excluding "Run?" from the list)
            if (changedInputs.Any(input => input != "Run?"))
            {
                // Debug.WriteLine($"[{GetType().Name}] Inputs changed, restarting debounce timer");
                // RestartDebounceTimer();
            }

            // Else, if "Run?" changed and run is True, directly transition to Processing
            else if (changedInputs.Any(input => input == "Run?") && run)
            {
                this.TransitionTo(ComponentState.Processing, DA);
                this.ExpireSolution(true);
            }

            this.CompleteStateTransition();
        }

        private void OnStateError(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[{this.GetType().Name}] OnStateError");

            // Reapply runtime messages in error state
            this.ApplyPersistentRuntimeMessages();

            // TransitionTo(ComponentState.Waiting, DA);
            this.CompleteStateTransition();
        }

        private bool IsValidTransition(ComponentState newState)
        {
            // Special cases: Transition to Error can always happen
            if (newState == ComponentState.Error)
            {
                return true;
            }

            // Normal flow validation
            switch (this.currentState)
            {
                case ComponentState.Completed:
                    return newState == ComponentState.Waiting || newState == ComponentState.NeedsRun || newState == ComponentState.Processing;
                case ComponentState.Waiting:
                    return newState == ComponentState.NeedsRun || newState == ComponentState.Processing;
                case ComponentState.NeedsRun:
                    return newState == ComponentState.Processing;
                case ComponentState.Processing:
                    return newState == ComponentState.Completed || newState == ComponentState.Cancelled;
                case ComponentState.Cancelled:
                case ComponentState.Error:
                    return newState == ComponentState.Waiting || newState == ComponentState.NeedsRun || newState == ComponentState.Processing;
                default:
                    return false;
            }
        }

        #endregion

        #region ERRORS

        private readonly Dictionary<string, (GH_RuntimeMessageLevel Level, string Message)> runtimeMessages = new ();

        /// <summary>
        /// Adds or updates a runtime message and optionally transitions to Error state.
        /// </summary>
        /// <param name="key">Unique identifier for the message.</param>
        /// <param name="level">The message severity level.</param>
        /// <param name="message">The message content.</param>
        /// <param name="transitionToError">If true and level is Error, transitions to Error state.</param>
        protected void SetPersistentRuntimeMessage(string key, GH_RuntimeMessageLevel level, string message, bool transitionToError = true)
        {
            this.runtimeMessages[key] = (level, message);

            if (transitionToError && level == GH_RuntimeMessageLevel.Error)
            {
                this.TransitionTo(ComponentState.Error, this.lastDA);
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
        /// <returns>True if the message was found and cleared, false otherwise.</returns>
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
            Debug.WriteLine($"[{this.GetType().Name}] [Runtime Messages] Applying {this.runtimeMessages.Count} runtime messages");
            foreach (var (level, message) in this.runtimeMessages.Values)
            {
                this.AddRuntimeMessage(level, message);
            }
        }

        #endregion

        #region DEBOUNCE

        /// <summary>
        /// Minimum debounce time in milliseconds. Input changes within this period will be ignored.
        /// </summary>
        private const int MINDEBOUNCETIME = 1000;

        /// <summary>
        /// Timer used to track the debounce period. When it elapses, if inputs are stable,
        /// the component will transition to NeedsRun state and trigger a solve.
        /// </summary>
        private readonly object timerLock = new ();
        private readonly Timer debounceTimer;
        private int inputChangedDuringDebounce;

        private ComponentState debounceTargetState = ComponentState.Waiting;

        /// <summary>
        /// Gets the debounce time from the SmartHopperSettings and returns the maximum between the settings value and the minimum value defined in MIN_DEBOUNCE_TIME.
        /// </summary>
        /// <returns>The debounce time in milliseconds.</returns>
        protected virtual int GetDebounceTime()
        {
            var settingsDebounceTime = SmartHopperSettings.Load().DebounceTime;
            return Math.Max(settingsDebounceTime, MINDEBOUNCETIME);
        }

        protected void RestartDebounceTimer()
        {
            lock (this.timerLock)
            {
                this.inputChangedDuringDebounce++;
                this.debounceTimer.Change(this.GetDebounceTime(), Timeout.Infinite);
                Debug.WriteLine($"[{this.GetType().Name}] Restarting debounce timer - Will transition to {this.debounceTargetState}");
            }
        }

        protected void RestartDebounceTimer(ComponentState targetState)
        {
            this.debounceTargetState = targetState;
            this.RestartDebounceTimer();
        }

        #endregion

        // --------------------------------------------------
        //             PERSISTENT DATA MANAGEMENT
        // --------------------------------------------------
        //
        // This section of code is responsible for storing
        // and retrieving persistent data for the component.
        #region PERSISTENT DATA

        // PRIVATE FIELDS
        private Dictionary<string, int> previousInputHashes;
        private Dictionary<string, int> previousInputBranchCounts;
        private readonly Dictionary<string, object> persistentOutputs;
        private readonly Dictionary<string, Type> persistentDataTypes;

        /// <summary>
        /// Restores all persistent outputs to their respective parameters.
        /// </summary>
        /// <param name="DA">The data access object.</param>
        protected virtual void RestorePersistentOutputs(IGH_DataAccess DA)
        {
            Debug.WriteLine("[StatefulAsyncComponentBase] [PersistentData] Restoring persistent outputs");

            for (int i = 0; i < this.Params.Output.Count; i++)
            {
                var param = this.Params.Output[i];
                var savedValue = this.GetPersistentOutput<object>(param.Name);
                if (savedValue != null)
                {
                    try
                    {
                        // Create a new IGH_Goo instance if the value isn't already one
                        IGH_Goo gooValue;
                        if (savedValue is IGH_Goo existingGoo)
                        {
                            gooValue = existingGoo;
                        }
                        else
                        {
                            // Try to create a new goo instance of the appropriate type
                            var gooType = param.Type;
                            gooValue = GH_Convert.ToGoo(savedValue);
                            if (gooValue == null)
                            {
                                // If direct conversion fails, try creating instance and casting
                                gooValue = (IGH_Goo)Activator.CreateInstance(gooType);
                                gooValue.CastFrom(savedValue);
                            }
                        }

                        // Add the properly typed goo value
                        this.SetPersistentOutput(param.Name, gooValue, DA);

                        Debug.WriteLine("[StatefulAsyncComponentBase] [PersistentData] Successfully restored output '" + param.Name + "' with value '" + gooValue + "' of type '" + this.persistentDataTypes[param.Name] + "'");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[StatefulAsyncComponentBase] [PersistentData] Failed to restore output '" + param.Name + "': " + ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Writes the component's persistent data to the Grasshopper file.
        /// </summary>
        /// <param name="writer">The writer to use for serialization.</param>
        /// <returns>True if the write operation succeeds, false if it fails or an exception occurs.</returns>
        public override bool Write(GH_IWriter writer)
        {
            if (!base.Write(writer))
            {
                return false;
            }

            try
            {
                // Store input hashes
                foreach (var kvp in this.previousInputHashes)
                {
                    writer.SetInt32($"InputHash_{kvp.Key}", kvp.Value);
                    Debug.WriteLine($"[StatefulAsyncComponentBase] [Write] Stored input hash for '{kvp.Key}': {kvp.Value}");
                }

                // Store input branch counts
                foreach (var kvp in this.previousInputBranchCounts)
                {
                    writer.SetInt32($"InputBranchCount_{kvp.Key}", kvp.Value);
                    Debug.WriteLine($"[StatefulAsyncComponentBase] [Write] Stored input branch count for '{kvp.Key}': {kvp.Value}");
                }

                // Store each output with its parameter name
                foreach (var kvp in this.persistentOutputs)
                {
                    string paramName = kvp.Key;
                    object paramValue = kvp.Value;

                    // Store the value
                    if (paramValue != null)
                    {
                        var chunk = new GH_LooseChunk($"Value_{paramName}");
                        if (paramValue is IGH_Structure structure)
                        {
                            // LogStructureDetails(structure);

                            // Use reflection to call Write method
                            var writeMethod = structure.GetType().GetMethod("Write");
                            writeMethod?.Invoke(structure, new object[] { chunk });
                            writer.SetString($"Type_{paramName}", structure.GetType().AssemblyQualifiedName);
                        }
                        else
                        {
                            break;
                        }

                        var chunkBytes = chunk.Serialize_Binary();

                        Debug.WriteLine($"[StatefulAsyncComponentBase] [Write] Serialized chunk size: {chunkBytes.Length} bytes");

                        // Write data
                        writer.SetByteArray($"Value_{paramName}", chunkBytes);

                        Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Stored output '{paramName}' with value '{paramValue}' of type '{paramValue.GetType().FullName}'");
                    }
                }

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
        /// <returns>True if the read operation succeeds, false if it fails, required data is missing, or an exception occurs.</returns>
        public override bool Read(GH_IReader reader)
        {
            if (!base.Read(reader))
            {
                return false;
            }

            // Clear previous hashes
            this.previousInputHashes.Clear();

            // Clear previous branch counts
            this.previousInputBranchCounts.Clear();

            // Clear previous outputs
            this.persistentOutputs.Clear();

            // Restore component specific data
            foreach (var item in reader.Items)
            {
                string key = item.Name;

                // Restore input hashes
                if (key.StartsWith("InputHash_"))
                {
                    string paramName = key.Substring("InputHash_".Length);

                    // Store data in local field
                    this.previousInputHashes[paramName] = reader.GetInt32(key);

                    Debug.WriteLine($"[StatefulAsyncComponentBase] [Read] Restored input hash for '{paramName}': {this.previousInputHashes[paramName]}");
                }

                // Restore input branch counts
                if (key.StartsWith("InputBranchCount_"))
                {
                    string paramName = key.Substring("InputBranchCount_".Length);

                    // Store data in local field
                    this.previousInputBranchCounts[paramName] = reader.GetInt32(key);

                    Debug.WriteLine($"[StatefulAsyncComponentBase] [Read] Restored input branch count for '{paramName}': {this.previousInputBranchCounts[paramName]}");
                }

                // Restore outputs
                if (key.StartsWith("Value_"))
                {
                    string paramName = key.Substring("Value_".Length);

                    string typeName = reader.GetString($"Type_{paramName}");
                    Type type = Type.GetType(typeName);

                    // Get the binary data and create a chunk from it
                    byte[] chunkBytes = reader.GetByteArray($"Value_{paramName}");
                    var chunk = new GH_LooseChunk($"Value_{paramName}");
                    chunk.Deserialize_Binary(chunkBytes);

                    // Create an instance of the type and read from chunk
                    var instance = Activator.CreateInstance(type);
                    var readMethod = type.GetMethod("Read");
                    readMethod?.Invoke(instance, new object[] { chunk });

                    // Store data in local field
                    this.persistentOutputs[paramName] = instance;
                }
            }

            // Outputs restored flag
            this.justRestoredFromFile = true;
            Debug.WriteLine($"[StatefulAsyncComponentBase] [Read] Restored from file with {this.persistentOutputs.Count} existing outputs, staying in Completed state");

            return true;
        }

        /// <summary>
        /// Extracts the inner value from a GH_ObjectWrapper if the object is of that type.
        /// </summary>
        /// <param name="value">The value to extract from.</param>
        /// <returns>The inner value if the input is a GH_ObjectWrapper, otherwise returns the input value unchanged.</returns>
        private static object ExtractGHObjectWrapperValue(object value)
        {
            if (value?.GetType()?.FullName == "Grasshopper.Kernel.Types.GH_ObjectWrapper")
            {
                var valueProperty = value.GetType().GetProperty("Value");
                if (valueProperty != null)
                {
                    var innerValue = valueProperty.GetValue(value);
                    Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Extracted inner value from GH_ObjectWrapper: {innerValue?.GetType()?.FullName ?? "null"}");
                    return innerValue;
                }
            }

            return value;
        }

        // private static void LogStructureDetails(IGH_Structure structure)
        // {
        //     Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Structure details:");
        //     Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] - Path count: {structure.PathCount}");
        //     Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] - Data count: {structure.DataCount}");
        //     Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] - Paths:");
        //     foreach (var path in structure.Paths)
        //     {
        //         var branch = structure.get_Branch(path);
        //         Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData]   - Path {path}: {branch?.Count ?? 0} items");
        //         if (branch != null)
        //         {
        //             foreach (var item in branch)
        //             {
        //                 Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData]     - {item?.ToString() ?? "null"} ({item?.GetType()?.FullName ?? "null"})");
        //             }
        //         }
        //     }
        // }

        /// <summary>
        /// Stores a value in the persistent storage.
        /// </summary>
        /// <param name="paramName">Name of the parameter to store.</param>
        /// <param name="value">Value to store.</param>
        /// <remarks>
        /// This method is protected to allow derived classes to manually store values if needed.
        /// However, values are automatically stored after solving, so manual storage is rarely necessary.
        /// </remarks>
        protected void SetPersistentOutput(string paramName, object value, IGH_DataAccess DA)
        {
            try
            {
                // Find the output parameter
                var param = this.Params.Output.FirstOrDefault(p => p.Name == paramName);
                var paramIndex = this.Params.Output.IndexOf(param);
                if (param != null)
                {
                    Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Initial value type: {value?.GetType()?.FullName ?? "null"}");

                    // Extract inner value if it's a GH_ObjectWrapper
                    value = ExtractGHObjectWrapperValue(value);
                    Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Value after extraction: {value?.GetType()?.FullName ?? "null"}");

                    // Store the value in persistent storage
                    this.persistentOutputs[paramName] = value;

                    // Store the type information
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
                        if (param.Access == GH_ParamAccess.tree)
                        {
                            Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Creating tree for parameter type: {param.Type.FullName}");

                            // If the value is already a GH_Structure, use it directly
                            if (value is IGH_Structure tree)
                            {
                                Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Using existing tree of type: {tree.GetType().FullName}");
                                DA.SetDataTree(paramIndex, tree);
                            }
                            else
                            {
                                // Create a new tree and add the single value
                                Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Creating new tree for single value");
                                var newTree = new GH_Structure<IGH_Goo>();

                                // Convert to IGH_Goo if needed
                                if (!(value is IGH_Goo))
                                {
                                    Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Converting to IGH_Goo: {value}");
                                    value = GH_Convert.ToGoo(value);
                                    Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Converted type: {value?.GetType()?.FullName ?? "null"}");
                                }

                                // Convert to the correct type if needed
                                if (value is IGH_Goo goo)
                                {
                                    Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Current value is IGH_Goo of type: {goo.GetType().FullName}");
                                    var targetType = param.Type;
                                    Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Target type: {targetType.FullName}");

                                    if (!goo.GetType().Equals(targetType))
                                    {
                                        Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Type mismatch, attempting conversion");

                                        // Get the raw value
                                        var rawValue = goo.ScriptVariable();
                                        Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] ScriptVariable type: {rawValue?.GetType()?.FullName ?? "null"}");
                                        Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] ScriptVariable value: {rawValue}");

                                        // If the target type is GH_Number, handle it specifically
                                        if (targetType == typeof(GH_Number) && rawValue is IConvertible)
                                        {
                                            Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Converting to GH_Number");
                                            value = new GH_Number(Convert.ToDouble(rawValue));
                                            Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Conversion successful: {value.GetType().FullName}");
                                        }
                                        else
                                        {
                                            var converted = GH_Convert.ToGoo(rawValue);
                                            Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Converted type: {converted?.GetType()?.FullName ?? "null"}");

                                            if (converted != null && converted.GetType().Equals(targetType))
                                            {
                                                Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Conversion successful");
                                                value = converted;
                                            }
                                            else
                                            {
                                                Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Conversion failed or type mismatch");
                                            }
                                        }
                                    }
                                }

                                Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Final value type before tree append: {value?.GetType()?.FullName ?? "null"}");
                                newTree.Append(value as IGH_Goo, new GH_Path(0));
                                DA.SetDataTree(paramIndex, newTree);
                            }
                        }
                        else
                        {
                            // Convert to IGH_Goo if needed for non-tree parameters
                            if (!(value is IGH_Goo))
                            {
                                value = GH_Convert.ToGoo(value);
                            }

                            DA.SetData(paramIndex, value);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[StatefulAsyncComponentBase] [PersistentData] DA is null, cannot set data.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[StatefulAsyncComponentBase] [PersistentData] Failed to set output '" + paramName + "': " + ex.Message);
                Debug.WriteLine($"[StatefulAsyncComponentBase] [PersistentData] Exception stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Retrieves a value from persistent storage.
        /// </summary>
        /// <typeparam name="T">The type of value to retrieve.</typeparam>
        /// <param name="paramName">Name of the parameter to retrieve.</param>
        /// <param name="defaultValue">Value to return if the parameter is not found.</param>
        /// <returns>The stored value or defaultValue if not found.</returns>
        protected T GetPersistentOutput<T>(string paramName, T defaultValue = default)
        {
            if (this.persistentOutputs.TryGetValue(paramName, out object value) && value is T typedValue)
            {
                return typedValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Calculates the hash for a single input parameter's data and branch structure.
        /// </summary>
        /// <param name="param">The input parameter to calculate hash for.</param>
        /// <param name="branchCount">Output parameter that returns the number of branches in the data.</param>
        /// <returns>The combined hash of the parameter's data and branch structure.</returns>
        private static int CalculatePersistentDataHash(IGH_Param param, out int branchCount)
        {
            var data = param.VolatileData;
            int currentHash = 0;
            branchCount = data.PathCount;

            // Hash both the data and branch structure
            foreach (var branch in data.Paths)
            {
                int branchHash = 0;
                foreach (var item in data.get_Branch(branch))
                {
                    if (item != null)
                    {
                        // Use value-based hashing instead of object-instance hashing
                        // to prevent false-positive changes when connecting new sources with same values
                        int itemHash;
                        
                        // Try to get the actual value for common Grasshopper types
                        if (item is IGH_Goo goo && goo.IsValid)
                        {
                            var value = goo.ScriptVariable();
                            itemHash = value?.GetHashCode() ?? 0;
                        }
                        else
                        {
                            // Fallback to string representation for consistent value-based hashing
                            itemHash = item.GetHashCode();
                        }
                        
                        branchHash = CombineHashCodes(branchHash, itemHash);
                    }
                }

                // Combine the branch data hash (captures the VALUES in this branch)
                currentHash = CombineHashCodes(currentHash, branchHash);
                
                // Combine the branch path hash (captures the STRUCTURE/PATH of this branch)
                // This is crucial because branches {0} and {1} with identical data should have different hashes
                currentHash = CombineHashCodes(currentHash, branch.GetHashCode());
            }

            return currentHash;
        }

        private void CalculatePersistentDataHashes()
        {
            if (this.previousInputHashes == null)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Initializing hash dictionaries");
                this.previousInputHashes = new Dictionary<string, int>();
                this.previousInputBranchCounts = new Dictionary<string, int>();
            }

            // Check each input parameter
            for (int i = 0; i < this.Params.Input.Count; i++)
            {
                var param = this.Params.Input[i];
                this.StorePersistentDataHash(param);
            }
        }

        private void StorePersistentDataHash(IGH_Param param)
        {
            int branchCount;
            int currentHash = CalculatePersistentDataHash(param, out branchCount);

            this.previousInputHashes[param.Name] = currentHash;
            this.previousInputBranchCounts[param.Name] = branchCount;
        }

        protected virtual List<string> InputsChanged()
        {
            if (this.previousInputHashes == null)
            {
                this.CalculatePersistentDataHashes();
            }

            var changedInputs = new List<string>();

            // Check each input parameter
            for (int i = 0; i < this.Params.Input.Count; i++)
            {
                var param = this.Params.Input[i];
                int branchCount;
                int currentHash = CalculatePersistentDataHash(param, out branchCount);

                bool inputChanged = false;

                // Check if hash changed
                if (!this.previousInputHashes.TryGetValue(param.Name, out int previousHash))
                {
                    Debug.WriteLine($"[{this.GetType().Name}] [CheckInputs Changed - {param.Name}] - No previous hash found for '{param.Name}'");
                    inputChanged = true;
                }
                else if (previousHash != currentHash)
                {
                    Debug.WriteLine($"[{this.GetType().Name}] [CheckInputs Changed - {param.Name}] - Hash changed for '{param.Name}' ({previousHash} to {currentHash})");
                    inputChanged = true;
                }

                // Check if branch count changed
                if (!this.previousInputBranchCounts.TryGetValue(param.Name, out int previousBranchCount))
                {
                    Debug.WriteLine($"[{this.GetType().Name}] [CheckInputs Changed - {param.Name}] - No previous branch count found for '{param.Name}'");
                    inputChanged = true;
                }
                else if (previousBranchCount != branchCount)
                {
                    Debug.WriteLine($"[{this.GetType().Name}] [CheckInputs Changed - {param.Name}] - Branch count changed for '{param.Name}' ({previousBranchCount} to {branchCount})");
                    inputChanged = true;
                }

                if (inputChanged)
                {
                    changedInputs.Add(param.Name);
                }
            }

            return changedInputs;
        }

        /// <summary>
        /// Checks if a specific input has changed since the last run.
        /// </summary>
        /// <param name="inputName">Name of the input parameter to check.</param>
        /// <param name="exclusively">If true, the check is performed exclusively, meaning only the given input is considered.</param>
        /// <returns>True if the input has changed.</returns>
        protected bool InputsChanged(string inputName, bool exclusively = true)
        {
            var changedInputs = this.InputsChanged();

            // If exclusively is true, check if the changedInputs list contains only the given inputName
            if (exclusively)
            {
                return changedInputs.Count == 1 && changedInputs.Any(name => name == inputName);
            }

            // If not exclusively, check if the inputName is present
            else
            {
                return changedInputs.Any(name => name == inputName);
            }
        }

        /// <summary>
        /// Checks if any of the specified inputs have changed since the last run.
        /// </summary>
        /// <param name="inputNames">List of input parameter names to check.</param>
        /// <param name="exclusively">If true, the check is performed exclusively, meaning only the given inputs are considered.</param>
        /// <returns>True if any of the specified inputs have changed.</returns>
        protected bool InputsChanged(IEnumerable<string> inputNames, bool exclusively = true)
        {
            var changedInputs = this.InputsChanged();
            var inputNamesList = inputNames.ToList();

            // If exclusively is true, check if the changedInputs list contains any of the given inputNames, and no other than the given inputNames
            if (exclusively)
            {
                // There cannot be changedInputs that are not in the inputNames list
                return changedInputs.Count > 0 && !changedInputs.Except(inputNamesList).Any();
            }

            // If not exclusively, check if any of the inputNames is present in changed inputs
            else
            {
                return changedInputs.Any(changed => inputNamesList.Contains(changed));
            }
        }

        private void ResetInputChanged()
        {
            for (int i = 0; i < this.Params.Input.Count; i++)
            {
                var param = this.Params.Input[i];
                int branchCount;
                int currentHash = CalculatePersistentDataHash(param, out branchCount);

                // Store current values for next comparison
                this.previousInputHashes[param.Name] = currentHash;
                this.previousInputBranchCounts[param.Name] = branchCount;
            }
        }

        private static int CombineHashCodes(int h1, int h2)
        {
            unchecked
            {
                return ((h1 << 5) + h1) ^ h2;
            }
        }

        /// <summary>
        /// Clears both persistent storage and output parameters while preserving runtime messages.
        /// </summary>
        protected override void ClearDataOnly()
        {
            this.persistentOutputs.Clear();
            base.ClearDataOnly();
        }

        #endregion

        #region AUX

        protected override void ExpireDownStreamObjects()
        {
            // Only expire downstream objects if we're in the completed state, which means that data is ready to output
            // This prevents the flash of null data until the new solution is ready
            if (this.currentState == ComponentState.Completed)
            {
                Debug.WriteLine("[StatefulAsyncComponentBase] Expiring downstream objects");
                base.ExpireDownStreamObjects();
            }

            return;
        }

        // JUST FOR DEBUG PURPOSES

        // public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        // {
        //     base.AppendAdditionalMenuItems(menu);
        //     Menu_AppendSeparator(menu);
        //     Menu_AppendItem(menu, "Debug: OnDisplayExpired(true)", (s, e) =>
        //     {
        //         Debug.WriteLine("[StatefulAsyncComponentBase] Manual OnDisplayExpired(true)");
        //         OnDisplayExpired(true);
        //     });
        //     Menu_AppendItem(menu, "Debug: OnDisplayExpired(false)", (s, e) =>
        //     {
        //         Debug.WriteLine("[StatefulAsyncComponentBase] Manual OnDisplayExpired(false)");
        //         OnDisplayExpired(false);
        //     });
        //     Menu_AppendItem(menu, "Debug: ExpireSolution", (s, e) =>
        //     {
        //         Debug.WriteLine("[StatefulAsyncComponentBase] Manual ExpireSolution");
        //         ExpireSolution(true);
        //     });
        //     Menu_AppendItem(menu, "Debug: ExpireDownStreamObjects", (s, e) =>
        //     {
        //         Debug.WriteLine("[StatefulAsyncComponentBase] Manual ExpireDownStreamObjects");
        //         ExpireDownStreamObjects();
        //     });
        //     Menu_AppendItem(menu, "Debug: ClearData", (s, e) =>
        //     {
        //         Debug.WriteLine("[StatefulAsyncComponentBase] Manual ClearData");
        //         ClearData();
        //     });
        //     Menu_AppendItem(menu, "Debug: ClearDataOnly", (s, e) =>
        //     {
        //         Debug.WriteLine("[StatefulAsyncComponentBase] Manual ClearDataOnly");
        //         ClearDataOnly();
        //     });
        //     Menu_AppendItem(menu, "Debug: Add Error", (s, e) =>
        //     {
        //         Debug.WriteLine("[StatefulAsyncComponentBase] Manual Add Error");
        //         SetPersistentRuntimeMessage("test-error", GH_RuntimeMessageLevel.Error, "This is an error");
        //     });
        //     Menu_AppendItem(menu, "Debug: Add Warning", (s, e) =>
        //     {
        //         Debug.WriteLine("[StatefulAsyncComponentBase] Manual Add Warning");
        //         SetPersistentRuntimeMessage("test-warning", GH_RuntimeMessageLevel.Warning, "This is a warning");
        //     });
        //     Menu_AppendItem(menu, "Debug: Add Remark", (s, e) =>
        //     {
        //         Debug.WriteLine("[StatefulAsyncComponentBase] Manual Add Remark");
        //         SetPersistentRuntimeMessage("test-remark", GH_RuntimeMessageLevel.Remark, "This is a remark");
        //     });
        // }
        public override void RequestTaskCancellation()
        {
            base.RequestTaskCancellation();
            this.TransitionTo(ComponentState.Cancelled, this.lastDA);
        }

        #endregion

    }
}
