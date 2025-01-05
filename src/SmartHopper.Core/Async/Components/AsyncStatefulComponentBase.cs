/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 * 
 * Portions of this code adapted from:
 * https://github.com/lamest/AsyncComponent
 * MIT License
 * Copyright (c) 2022 Ivan Sukhikh
 */

using Grasshopper.Kernel;
using SmartHopper.Core.Async.Core;
using SmartHopper.Core.Async.Core.StateManagement;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace SmartHopper.Core.Async.Components
{
    public abstract class AsyncStatefulComponentBase : AsyncComponentBase
    {
        /// <summary>
        /// Manages the state of the component throughout its lifecycle.
        /// </summary>
        private readonly IComponentStateManager _stateManager;

        /// <summary>
        /// Handles messaging and reporting for the component.
        /// </summary>
        private readonly ComponentMessaging _messaging;

        /// <summary>
        /// Tracks the last time the component completed its processing.
        /// </summary>
        private DateTime _lastCompletionTime = DateTime.MinValue;

        /// <summary>
        /// Defines the minimum time interval between processing attempts to prevent rapid re-execution.
        /// </summary>
        private const int DEBOUNCE_MS = 1000;

        /// <summary>
        /// Gets the data access instance for the component.
        /// </summary>
        protected IGH_DataAccess DataAccess { get; private set; }

        /// <summary>
        /// Initializes a new instance of the AsyncStatefulComponentBase class.
        /// </summary>
        /// <param name="name">The name of the component.</param>
        /// <param name="nickname">The nickname of the component.</param>
        /// <param name="description">The description of the component.</param>
        /// <param name="category">The category of the component.</param>
        /// <param name="subCategory">The subcategory of the component.</param>
        protected AsyncStatefulComponentBase(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            _stateManager = new ComponentStateManager(this);
            _stateManager.StateChanged += OnStateChanged;
            _messaging = new ComponentMessaging(this);
            base.StateManager = _stateManager;
        }

        /// <summary>
        /// Solves the instance of the component, managing state transitions and async operations.
        /// </summary>
        /// <param name="DA">The Grasshopper data access object.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (DataAccess == null) DataAccess = DA;

            bool run = false;
            DA.GetData("Run", ref run);

            // Update needsRun based on input changes and current state
            bool needsRun = run;
            if (_stateManager.CurrentState == ComponentState.Completed)
            {
                needsRun = false;
            }

            // Log the current state and flags for debugging
            Debug.WriteLine($"[AsyncStatefulComponentBase] SolveInstance - State: {_stateManager.CurrentState}, Run: {run}, NeedsRun: {needsRun}, InPreSolve: {InPreSolve}, IsWorkerCompletion: {_stateManager.CurrentState == ComponentState.Completed}");

            if (InPreSolve && needsRun && _stateManager.CurrentState != ComponentState.Processing)
            {
                // Start the asynchronous operation during PreSolve phase
                TransitionTo(ComponentState.Processing);
                base.SolveInstance(DA);
            }
            else if (_stateManager.CurrentState == ComponentState.Completed && !InPreSolve)
            {
                // The worker has completed and we're not in PreSolve, so it's safe to set outputs
                Debug.WriteLine("[AsyncStatefulComponentBase] In Completed state (results phase), calling base to set outputs");
                base.SolveInstance(DA);

                // Transition back to Waiting state after outputs are set and update completion time
                Debug.WriteLine("[AsyncStatefulComponentBase] Completed state finished outputting, transitioning to Waiting");
                _lastCompletionTime = DateTime.Now;
                TransitionTo(ComponentState.Waiting);
            }
            else if (!InPreSolve &&
                (DateTime.Now - _lastCompletionTime).TotalMilliseconds > DEBOUNCE_MS &&
                (_stateManager.CurrentState == ComponentState.Waiting ||
                _stateManager.CurrentState == ComponentState.NeedsRerun ||
                _stateManager.CurrentState == ComponentState.NeedsRun ||
                _stateManager.CurrentState == ComponentState.Error ||
                _stateManager.CurrentState == ComponentState.Cancelled)
                )
            {
                Debug.WriteLine($"[AsyncStatefulComponentBase] External solution expiration while Waiting (Time since completion: {(DateTime.Now - _lastCompletionTime).TotalMilliseconds}ms), transitioning to NeedsRerun");
                TransitionTo(ComponentState.NeedsRerun);
            }
        }

        /// <summary>
        /// Expires downstream objects based on the current component state.
        /// Only expires if the state is Completed or NeedsRun.
        /// </summary>
        protected override void ExpireDownStreamObjects()
        {
            // Only expire downstream objects if we're not in the middle of a worker completion
            // This prevents the flash of null data until the new solution is ready
            Debug.WriteLine($"[AsyncStatefulComponentBase] ExpireDownStreamObjects - Values - CurrentState: {_stateManager.CurrentState} --> Expiring? {_stateManager.CurrentState == ComponentState.Completed}");
            if (StateManager.CurrentState == ComponentState.Completed ||
                StateManager.CurrentState == ComponentState.NeedsRun)
            {
                Debug.WriteLine("Expiring downstream objects from AsyncStatefulComponentBase");
                base.ExpireDownStreamObjects();
            }
        }

        /// <summary>
        /// Appends additional menu items to the component's context menu.
        /// Adds a cancel option when the component is in Processing state.
        /// </summary>
        /// <param name="menu">The dropdown menu to append items to.</param>
        protected override void AppendAdditionalComponentMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);

            // Only show cancel option when component is processing
            if (_stateManager.CurrentState == ComponentState.Processing)
            {
                Menu_AppendSeparator(menu);
                Menu_AppendItem(menu, "Cancel processing", (s, e) =>
                {
                    RequestTaskCancellation();
                    TransitionTo(ComponentState.Cancelled);
                });
            }
        }

        /// <summary>
        /// Cancels the current operation and transitions to cancelled state.
        /// </summary>
        protected override void Cancel()
        {
            base.Cancel();
            TransitionTo(ComponentState.Cancelled);
        }

        /// <summary>
        /// Gets the current state of the component.
        /// </summary>
        /// <returns>The current ComponentState of the component.</returns>
        public ComponentState GetCurrentState() => _stateManager.CurrentState;

        /// <summary>
        /// Transitions the component to a new state.
        /// </summary>
        /// <param name="newState">The target state to transition to.</param>
        internal virtual void TransitionTo(ComponentState newState)
        {
            Debug.WriteLine($"[AsyncStatefulComponentBase] State transition: {_stateManager.CurrentState} -> {newState}");
            _stateManager.TransitionTo(newState);
        }

        /// <summary>
        /// Handles state changes in the component.
        /// </summary>
        /// <param name="newState">The new state to transition to.</param>
        protected virtual void OnStateChanged(ComponentState newState)
        {
            if (newState == ComponentState.Completed)
            {
                Debug.WriteLine("[AsyncStatefulComponentBase] Worker completed, calling OnWorkerCompleted");
                OnWorkerCompleted();
            }
        }

        /// <summary>
        /// Handles the completion of the worker process, updating component state and triggering necessary UI updates.
        /// </summary>
        internal void OnWorkerCompleted()
        {
            try
            {
                TransitionTo(ComponentState.Completed);
                Debug.WriteLine("[AsyncStatefulComponentBase] Worker completed, expiring solution");

                // First, trigger the solution expiration on the UI thread
                Rhino.RhinoApp.InvokeOnUiThread((Action)delegate
                {
                    ExpireSolution(true);
                });

                // Then update the display - this needs to happen after ExpireSolution
                // to ensure proper synchronization
                Message = "Done";
                Rhino.RhinoApp.InvokeOnUiThread((Action)delegate
                {
                    OnDisplayExpired(true);
                });
            }
            finally
            {
                // State will transition to Waiting in SolveInstance
            }
        }

        /// <summary>
        /// Reports progress message for the component.
        /// </summary>
        /// <param name="message">The progress message to display.</param>
        public void ReportProgress(string message)
        {
            _messaging.ReportProgress(message);
        }

        /// <summary>
        /// Reports an error message and handles the runtime error state.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        protected void ReportError(string message)
        {
            _messaging.ReportError(message);
            _stateManager.HandleRuntimeError();
        }

        /// <summary>
        /// Reports a warning message for the component.
        /// </summary>
        /// <param name="message">The warning message to display.</param>
        protected void ReportWarning(string message)
        {
            _messaging.ReportWarning(message);
        }

        /// <summary>
        /// Reports a remark message for the component.
        /// </summary>
        /// <param name="message">The remark message to display.</param>
        protected void ReportRemark(string message)
        {
            _messaging.ReportRemark(message);
        }

        
    }
}
