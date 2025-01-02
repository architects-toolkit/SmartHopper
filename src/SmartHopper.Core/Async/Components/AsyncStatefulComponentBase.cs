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
        private readonly IComponentStateManager _stateManager;
        private readonly ComponentMessaging _messaging;
        private DateTime _lastCompletionTime = DateTime.MinValue;
        private const int DEBOUNCE_MS = 100;
        protected IGH_DataAccess DataAccess { get; private set; }

        public ComponentState GetCurrentState() => _stateManager.CurrentState;

        protected AsyncStatefulComponentBase(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            _stateManager = new ComponentStateManager(this);
            _stateManager.StateChanged += OnStateChanged;
            _messaging = new ComponentMessaging(this);
            base.StateManager = _stateManager;
        }

        protected virtual void OnStateChanged(ComponentState newState)
        {
            if (newState == ComponentState.Completed)
            {
                Debug.WriteLine("[AsyncStatefulComponentBase] Worker completed, calling OnWorkerCompleted");
                OnWorkerCompleted();
            }
        }

        internal virtual void TransitionTo(ComponentState newState)
        {
            Debug.WriteLine($"[AsyncStatefulComponentBase] State transition: {_stateManager.CurrentState} -> {newState}");
            _stateManager.TransitionTo(newState);
        }

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

        protected override void Cancel()
        {
            base.Cancel();
            TransitionTo(ComponentState.Cancelled);
        }

        public void ReportProgress(string message)
        {
            _messaging.ReportProgress(message);
        }

        protected void ReportError(string message)
        {
            _messaging.ReportError(message);
            _stateManager.HandleRuntimeError();
        }

        protected void ReportWarning(string message)
        {
            _messaging.ReportWarning(message);
        }

        protected void ReportRemark(string message)
        {
            _messaging.ReportRemark(message);
        }

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
    }
}
