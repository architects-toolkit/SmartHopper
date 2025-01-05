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

using Rhino;
using Grasshopper.Kernel;
using SmartHopper.Core.Async.Core.StateManagement;
using SmartHopper.Core.Async.Threading;
using SmartHopper.Core.Async.Workers;
using System;
using System.Diagnostics;
using System.Threading;

namespace SmartHopper.Core.Async.Components
{
    /// <summary>
    /// Base class for asynchronous Grasshopper components that support task-based operations.
    /// </summary>
    public abstract class AsyncComponentBase : GH_Component, IGH_TaskCapableComponent, IAsyncComponent
    {
        /// <summary>
        /// Cancellation token source for managing async operations.
        /// </summary>
        private CancellationTokenSource _cts;

        /// <summary>
        /// Indicates whether the component should use task-based operations.
        /// </summary>
        private bool _useTasks = true;

        /// <summary>
        /// Indicates whether the component is in the pre-solve phase.
        /// </summary>
        private bool _inPreSolve;

        /// <summary>
        /// Gets or sets the error message when an operation fails.
        /// </summary>
        protected string ErrorMessage { get; set; }

        /// <summary>
        /// Manager for handling asynchronous tasks.
        /// </summary>
        protected TaskManager _taskManager;

        /// <summary>
        /// Progress reporter for async operations.
        /// </summary>
        protected IProgress<string> _progress;

        /// <summary>
        /// Gets or sets the current worker handling the async operation.
        /// </summary>
        protected AsyncWorker CurrentWorker { get; private set; }

        /// <summary>
        /// Gets or sets the state manager for the component.
        /// </summary>
        public virtual IComponentStateManager StateManager { get; protected set; }

        /// <summary>
        /// Gets or sets whether the component is in the pre-solve phase.
        /// </summary>
        public bool InPreSolve
        {
            get => _inPreSolve;
            set => _inPreSolve = value;
        }

        /// <summary>
        /// Gets whether the component supports task-based operations.
        /// </summary>
        public bool ITaskCapable => true;

        /// <summary>
        /// Gets or sets whether the component should use task-based operations.
        /// </summary>
        public bool UseTasks
        {
            get => _useTasks;
            set => _useTasks = value;
        }

        /// <summary>
        /// Gets whether the component is being manually expired.
        /// </summary>
        protected bool IsManualExpire => !InPreSolve;

        /// <summary>
        /// Requests cancellation of the current task.
        /// </summary>
        public void RequestTaskCancellation()
        {
            Cancel();
        }

        /// <summary>
        /// Initializes a new instance of the AsyncComponentBase class.
        /// </summary>
        /// <param name="name">The name of the component.</param>
        /// <param name="nickname">The nickname of the component.</param>
        /// <param name="description">The description of the component.</param>
        /// <param name="category">The category of the component.</param>
        /// <param name="subCategory">The subcategory of the component.</param>
        protected AsyncComponentBase(string name, string nickname, string description, string category, string subCategory) :
            base(name, nickname, description, category, subCategory)
        {
            _cts = new CancellationTokenSource();

            // Initialize task manager
            _taskManager = new TaskManager();
            _taskManager.OnTaskError += message =>
            {
                ErrorMessage = message;
                Message = "Error";
                OnDisplayExpired(true);
            };
            _taskManager.OnTaskCancelled += () =>
            {
                Message = "Cancelled";
                OnDisplayExpired(true);
            };
            _taskManager.OnTaskCompleted += () => OnDisplayExpired(true);

            StateManager = new ComponentStateManager(this);
        }

        /// <summary>
        /// Creates a worker instance for handling asynchronous operations.
        /// </summary>
        /// <param name="progressReporter">Action to report progress during the operation.</param>
        /// <returns>An instance of AsyncWorker to handle the work.</returns>
        protected abstract AsyncWorker CreateWorker(Action<string> progressReporter);

        /// <summary>
        /// Cancels the current operation and updates the component state.
        /// </summary>
        protected virtual void Cancel()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                Message = "Cancelled";
                OnDisplayExpired(true);
            }
            _taskManager?.Cancel();
        }

        /// <summary>
        /// Processes the component synchronously when async operations are disabled.
        /// </summary>
        /// <param name="DA">The Grasshopper data access object.</param>
        protected virtual void ProcessSynchronously(IGH_DataAccess DA)
        {
            Debug.WriteLine("[AsyncComponentBase] Processing synchronously");
            var worker = CreateWorker(p => Message = p);
            worker.GatherInput(DA);
            worker.DoWorkAsync(CancellationToken.None).Wait();
            worker.SetOutput(DA, out _);
            OnWorkerCompleted();
        }

        /// <summary>
        /// Solves the instance of the component, managing the async workflow and state transitions.
        /// </summary>
        /// <param name="DA">The Grasshopper data access object.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[AsyncComponentBase] SolveInstance - Start. InPreSolve: {InPreSolve}, CurrentWorker: {CurrentWorker != null}, IsWorkerCompletion: {StateManager.CurrentState == ComponentState.Completed}");

            if (StateManager.CurrentState == ComponentState.Completed)
            {
                Debug.WriteLine("[AsyncComponentBase] Worker completion phase, setting outputs");
                Debug.WriteLine($"[AsyncComponentBase] Conditions: InPreSolve={InPreSolve}, ErrorMessage={ErrorMessage != null}, CurrentWorker={CurrentWorker != null}, IsDone={CurrentWorker?.IsDone}");
                if (CurrentWorker != null && CurrentWorker.IsDone)
                {
                    string doneMessage = null;
                    CurrentWorker.SetOutput(DA, out doneMessage);
                    Message = string.IsNullOrWhiteSpace(doneMessage) ? "Done" : doneMessage;
                }
                return;
            }

            if (!UseTasks)
            {
                ProcessSynchronously(DA);
                return;
            }

            if (!InPreSolve) // Results phase
            {
                Debug.WriteLine("[AsyncComponentBase] Results phase");
                Debug.WriteLine($"[AsyncComponentBase] Conditions: InPreSolve={InPreSolve}, ErrorMessage={ErrorMessage != null}, CurrentWorker={CurrentWorker != null}, IsDone={CurrentWorker?.IsDone}");

                if (ErrorMessage != null)
                {
                    Debug.WriteLine($"[AsyncComponentBase] Error: {ErrorMessage}");
                    var w = GH_RuntimeMessageLevel.Error;
                    AddRuntimeMessage(w, ErrorMessage);
                    Message = "Error";
                }
                else if (CurrentWorker != null && CurrentWorker.IsDone)
                {
                    Debug.WriteLine($"[AsyncComponentBase] Worker state - CurrentWorker: {CurrentWorker != null}, IsDone: {CurrentWorker?.IsDone}");
                    Debug.WriteLine("[AsyncComponentBase] Setting output from worker");
                    string doneMessage = null;
                    CurrentWorker.SetOutput(DA, out doneMessage);
                    Message = string.IsNullOrWhiteSpace(doneMessage) ? "Done" : doneMessage;

                    // Mark that we've set the data
                    StateManager.SetData = true;
                }
                else
                {
                    Debug.WriteLine($"[AsyncComponentBase] Output condition not met - CurrentWorker: {CurrentWorker != null}, IsDone: {CurrentWorker?.IsDone}");
                }

                if (!InPreSolve)
                {
                    OnDisplayExpired(true);
                }

                return;
            }

            Debug.WriteLine("[AsyncComponentBase] Starting new work");
            Debug.WriteLine($"[AsyncComponentBase] StateManager state: {(StateManager == null ? "null" : StateManager.CurrentState.ToString())}");
            Debug.WriteLine($"[AsyncComponentBase] _taskManager state: {(_taskManager == null ? "null" : "initialized")}");

            _cts = new CancellationTokenSource();
            Debug.WriteLine("[AsyncComponentBase] Created new CancellationTokenSource");

            // Create progress reporter
            _progress = new ProgressReporter(p =>
            {
                Debug.WriteLine($"[AsyncComponentBase] Progress Report: {p}");
                Message = p;
                OnDisplayExpired(true);
            }, _taskManager?.Token ?? CancellationToken.None);
            Debug.WriteLine("[AsyncComponentBase] Created ProgressReporter");

            // Create and setup worker
            try
            {
                Debug.WriteLine("[AsyncComponentBase] About to create worker");
                CurrentWorker = CreateWorker(p => _progress.Report(p));
                Debug.WriteLine($"[AsyncComponentBase] Worker created: {(CurrentWorker == null ? "null" : "initialized")}");

                if (CurrentWorker == null)
                {
                    throw new InvalidOperationException("CreateWorker returned null");
                }

                Debug.WriteLine("[AsyncComponentBase] About to gather input");
                CurrentWorker.GatherInput(DA);
                Debug.WriteLine("[AsyncComponentBase] Input gathered successfully");

                // Start the task
                Debug.WriteLine("[AsyncComponentBase] About to start task");
                _taskManager.RunAsync(token => CurrentWorker.DoWorkAsync(token), _progress);
                Debug.WriteLine("[AsyncComponentBase] Task started successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AsyncComponentBase] Exception during worker setup: {ex.GetType().Name} - {ex.Message}");
                Debug.WriteLine($"[AsyncComponentBase] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Expires downstream objects when the component is updated.
        /// </summary>
        protected override void ExpireDownStreamObjects()
        {
            // Only expire downstream objects if we're not in the middle of a worker completion
            // This prevents the flash of null data until the new solution is ready
            Debug.WriteLine($"[AsyncComponentBase] ExpireDownStreamObjects - Values - CurrentState: {StateManager.CurrentState}, SetData: {StateManager.SetData}");
            // if (StateManager.CurrentState == ComponentState.Completed && StateManager.SetData)
            // {
            Debug.WriteLine("Expiring downstream objects");
            base.ExpireDownStreamObjects();
            // }
        }

        /// <summary>
        /// Called when the worker completes its operation.
        /// </summary>
        protected virtual void OnWorkerCompleted()
        {
            // AQUÍ NO S'HI ARRIBA MAI, NOMÉS AMB EXECUCIÓ SINCRONA

            Debug.WriteLine("[AsyncComponentBase] Worker completed - Before UI thread");

            // Only handle display updates here
            Message = "Done";
            Debug.WriteLine("[AsyncComponentBase] About to invoke on UI thread");
            RhinoApp.InvokeOnUiThread((Action)delegate
            {
                Debug.WriteLine("[AsyncComponentBase] Inside UI thread delegate");
                // OnDisplayExpired(true);
                // Only expire downstream objects after we've set the data
                if (StateManager.SetData)
                {
                    Debug.WriteLine($">> [AsyncComponentBase] Expiring downstream objects after worker completion - SetData: {StateManager.SetData}");
                    ExpireDownStreamObjects();
                    StateManager.SetData = false;
                    Debug.WriteLine("[AsyncComponentBase] Finished expiring downstream objects");
                }
            });
            Debug.WriteLine("[AsyncComponentBase] After scheduling UI thread work");
        }
    }
}
