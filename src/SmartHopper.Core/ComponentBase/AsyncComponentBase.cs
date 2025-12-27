/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
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
 * Base class for all asynchronous components.
 * This class provides the fundamental structure for components that need to perform
 * asynchronous operations while maintaining Grasshopper's component lifecycle.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grasshopper.Kernel;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for asynchronous Grasshopper components. Inherit from this class
    /// when you need to create a component that performs long-running operations.
    /// </summary>
    public abstract class AsyncComponentBase : GH_Component
    {
        private readonly List<Task> _tasks;
        private readonly List<CancellationTokenSource> _cancellationSources;

        /// <summary>
        /// Backing field for the number of data items to output.
        /// </summary>
        private int _dataCount;

        /// <summary>
        /// Gets the number of data items to output (read-only for derived classes).
        /// </summary>
        protected int DataCount => this._dataCount;

        /// <summary>
        /// Sets the number of data items to output. Intended for higher-level
        /// component bases (e.g., stateful async components) that centralise
        /// data count calculation based on shared processing plans.
        /// </summary>
        /// <param name="value">The computed data count.</param>
        protected void SetDataCount(int value)
        {
            this._dataCount = value;
        }

        /// <summary>
        /// Tracks the state of worker task completion:
        /// - Starts at 0 when component initializes
        /// - Increments when a worker task completes
        /// - Decrements when processing output from workers
        /// - When equals Workers.Count, all workers have completed
        /// This is used in conjunction with _setData to coordinate the async execution flow
        /// and ensure proper ordering of worker output processing.
        /// </summary>
        // Backing field for the async state counter. Private to allow safe interlocked operations.
        private int _state;

        /// <summary>
        /// Gets the current async worker completion state value.
        /// </summary>
        protected int State => this._state;

        /// <summary>
        /// Flag indicating whether the component is ready to process worker outputs:
        /// - 0: Workers are still executing or haven't started
        /// - 1: All workers have completed and outputs can be processed
        /// Used as a latch to ensure outputs are processed only once and
        /// prevent re-execution of workers during the output phase.
        /// Set to 1 via Interlocked.Exchange when _state equals Workers.Count.
        /// </summary>
        // Backing field for the output processing latch. Private to allow safe interlocked operations.
        private int _setData;

        /// <summary>
        /// Gets the current output processing latch value (0/1).
        /// </summary>
        protected int SetData => this._setData;

        // Backing field for InPreSolve flag.
        private bool _inPreSolve;

        protected List<AsyncWorkerBase> Workers { get; private set; }

        protected AsyncWorkerBase CurrentWorker { get; private set; }

        // public bool ITaskCapable => true;
        // public virtual bool UseTasks { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether gets the component is in pre-solve phase.
        /// Inherited from IGH_TaskCapableComponent.
        /// </summary>
        public bool InPreSolve
        {
            get => this._inPreSolve;
            set => this._inPreSolve = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncComponentBase"/> class.
        /// Constructor for AsyncComponentBase.
        /// </summary>
        /// <param name="name">The display name of the component.</param>
        /// <param name="nickname">The shortened display name.</param>
        /// <param name="description">Description of the component's function.</param>
        /// <param name="category">The tab category where the component appears.</param>
        /// <param name="subCategory">The sub-category within the tab.</param>
        protected AsyncComponentBase(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            this._tasks = new List<Task>();
            this._cancellationSources = new List<CancellationTokenSource>();
            this.Workers = new List<AsyncWorkerBase>();
        }

        /// <summary>
        /// Creates a new worker instance for this component.
        /// </summary>
        /// <returns></returns>
        protected abstract AsyncWorkerBase CreateWorker(Action<string> progressReporter);

        protected override void BeforeSolveInstance()
        {
            if (this._state > 0 && this._setData == 1)
            {
                // Skip BeforeSolveInstance and jump to SolveInstance
                return;
            }

            Debug.WriteLine("[AsyncComponentBase] BeforeSolveInstance - Cleaning up previous run");

            // Cancel any running tasks before reset
            this.TaskCancellation();

            // Use the centralized reset method
            this.ResetAsyncState();
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[AsyncComponentBase] SolveInstance - State: {this._state}, Tasks: {this._tasks.Count}, SetData: {this._setData}");

            // Initial run
            if (this._state == 0 && this._tasks.Count == 0)
            {
                Debug.WriteLine($"[AsyncComponentBase] Creating a new worker, State: {this._state}, Tasks: {this._tasks.Count}, SetData: {this._setData}, Workers: {this.Workers.Count}, CancellationSources: {this._cancellationSources.Count}, CurrentWorker: {this.CurrentWorker != null}, Message: {this.Message}");

                // First pass - Pre-solve
                this.InPreSolve = true;

                // Create a new worker and add it to the list
                var worker = this.CreateWorker(s => this.Message = s);
                this.Workers.Add(worker);

                Debug.WriteLine("[AsyncComponentBase] Gathering input");

                // Gather input before starting the task
                worker.GatherInput(DA, out int dataCount);
                this.CurrentWorker = worker;
                this._dataCount = dataCount;

                // Create cancellation token source
                var source = new CancellationTokenSource();
                this._cancellationSources.Add(source);

                // Create task that properly awaits the async work
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await worker.DoWorkAsync(source.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AsyncComponentBase] Task failed with error: {ex.Message}");
                        throw; // Re-throw to be caught by ContinueWith
                    }
                });

                // Add task to the list
                this._tasks.Add(task);

                this.OnSolveInstancePreSolve(DA);
                return; // Jump to AfterSolveInstance to execute tasks
            }

            /* _state != 0 || _tasks.Count != 0 */

            // Second pass - Post-solve - Setting output
            this.InPreSolve = false;

            // If tasks are still running (_setData == 0) or the state is not ready (>0),
            // skip output processing until the continuation sets _state and _setData.
            if (this._setData == 0 || this._state <= 0)
            {
                Debug.WriteLine($"[AsyncComponentBase] Post-solve skipped. Tasks running or state not ready. State: {this._state}, SetData: {this._setData}");
                return;
            }

            Debug.WriteLine($"[AsyncComponentBase] Post-solve - Setting output. InPreSolve: {this.InPreSolve}, State: {this._state}, SetData: {this._setData}, Workers.Count: {this.Workers.Count}");

            if (this.Workers.Count > 0)
            {
                // Call SetOutput for each worker in reverse order
                for (int i = this.Workers.Count - 1; i >= 0; i--)
                {
                    Debug.WriteLine($"[AsyncComponentBase] Setting output for worker {i + 1}/{this.Workers.Count}");
                    string? outMessage = null;

                    // Ensure SetOutput runs on UI thread
                    Rhino.RhinoApp.InvokeOnUiThread(() =>
                    {
                        this.Workers[i].SetOutput(DA, out outMessage);
                        if (!string.IsNullOrEmpty(outMessage))
                        {
                            this.Message = outMessage;
                        }
                        Debug.WriteLine($"[AsyncComponentBase] Worker {i + 1} output set, message: {outMessage}");
                    });

                    Interlocked.Decrement(ref this._state);
                }

                Debug.WriteLine($"[AsyncComponentBase] All workers output set. Final state: {this._state}");
                this.OnSolveInstancePostSolve(DA);

                // Do not expire downstream during an active solution.
                // Expiration is handled after tasks completion via the continuation in AfterSolveInstance.
            }

            if (this._state != 0)
            {
                return; // Call SolveInstanve again until state is 0
            }

            // Clean up
            this._cancellationSources.Clear();
            this.Workers.Clear();
            this._tasks.Clear();

            Interlocked.Exchange(ref this._setData, 0);

            this.OnWorkerCompleted();
        }

        protected override void AfterSolveInstance()
        {
            Debug.WriteLine($"[AsyncComponentBase] AfterSolveInstance - State: {this._state}, Tasks: {this._tasks.Count}, SetData: {this._setData}");

            if (this._state == 0 && this._tasks.Count > 0 && this._setData == 0)
            {
                Debug.WriteLine($"[AsyncComponentBase] Starting {this._tasks.Count} tasks");

                // Create a continuation task that will handle completion of all tasks
                Task.WhenAll(this._tasks)
                    .ContinueWith(
                        t =>
                    {
                        if (t.IsFaulted)
                        {
                            Debug.WriteLine($"[AsyncComponentBase] Task exceptions occurred:");
                            var ae = t.Exception;
                            foreach (var ex in ae.InnerExceptions)
                            {
                                Debug.WriteLine($"[AsyncComponentBase] - {ex.Message}");
                                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Task error: {ex.Message}");
                            }

                            // Ensure state is valid even on error
                            if (this._state == 0)
                            {
                                // When tasks fault, still proceed to post-solve once to surface messages
                                int workerCount = this.Workers.Count;
                                Debug.WriteLine($"[AsyncComponentBase] Faulted: setting state to Workers.Count ({workerCount}) and enabling output phase");
                                Interlocked.Exchange(ref this._state, workerCount);
                                Interlocked.Exchange(ref this._setData, 1);

                                // Preserve LIFO output order
                                this.Workers.Reverse();
                            }
                        }
                        else if (t.IsCanceled)
                        {
                            Debug.WriteLine("[AsyncComponentBase] Tasks were canceled. Resetting async state and skipping output phase.");

                            Rhino.RhinoApp.InvokeOnUiThread(() =>
                            {
                                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Tasks were canceled.");
                                this.ResetAsyncState();
                                this.OnTasksCanceled();
                                this.ExpireSolution(true);
                            });

                            return;
                        }
                        else
                        {
                            // All tasks completed successfully; set state to total workers so post-solve can decrement to zero
                            if (this._state == 0 && this._setData == 0)
                            {
                                int workerCount = this.Workers.Count;
                                Interlocked.Exchange(ref this._state, workerCount);
                                Interlocked.Exchange(ref this._setData, 1);

                                // Process outputs in reverse (LIFO) to match expected ordering
                                this.Workers.Reverse();
                                Debug.WriteLine($"[AsyncComponentBase] All tasks completed. Preparing output phase: State set to {workerCount}, SetData=1");
                            }
                        }

                        // Schedule component update on UI thread
                        Rhino.RhinoApp.InvokeOnUiThread(() =>
                        {
                            this.ExpireSolution(true);
                        });
                    }, TaskScheduler.Default);
            }
        }

        private void TaskCancellation()
        {
            foreach (var source in this._cancellationSources)
            {
                source.Cancel();
            }
        }

        public virtual void RequestTaskCancellation()
        {
            this.TaskCancellation();
        }

        /// <summary>
        /// Resets the AsyncComponentBase state variables to allow fresh worker creation.
        /// This is needed when transitioning to Processing state from other states,
        /// especially for boolean toggle scenarios where async state needs to be reset.
        /// </summary>
        protected void ResetAsyncState()
        {
            Debug.WriteLine($"[{this.GetType().Name}] ResetAsyncState - Before: State={this._state}, Tasks={this._tasks?.Count ?? 0}, SetData={this._setData}");

            // Clear cancellation sources from previous runs
            this._cancellationSources?.Clear();

            // Clear any existing tasks and workers from previous runs
            this._tasks?.Clear();
            this.Workers?.Clear();

            // Reset the async component state variables to initial values
            // This ensures that AsyncComponentBase.SolveInstance() will create new workers
            this._state = 0;
            this._setData = 0;

            Debug.WriteLine($"[{this.GetType().Name}] ResetAsyncState - After: State={this._state}, Tasks={this._tasks?.Count ?? 0}, SetData={this._setData}");
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Cancel current process", (s, e) =>
            {
                this.RequestTaskCancellation();
            });
        }

        protected virtual void OnWorkerCompleted()
        {
            Debug.WriteLine($"[{this.GetType().Name}] All workers completed. State: {this._state}, Tasks: {this._tasks.Count}, SetData: {this._setData}");
        }

        /// <summary>
        /// Called when the worker tasks are canceled and the output phase is skipped.
        /// Allows derived classes to react (e.g. transition state machines out of Processing).
        /// </summary>
        protected virtual void OnTasksCanceled()
        {
        }

        /// <summary>
        /// Override this method to implement custom solve logic.
        /// This will be called for pre-solve.
        /// </summary>
        protected virtual void OnSolveInstancePreSolve(IGH_DataAccess DA)
        {
        }

        /// <summary>
        /// Override this method to implement custom solve logic.
        /// This will be called for post-solve.
        /// </summary>
        protected virtual void OnSolveInstancePostSolve(IGH_DataAccess DA)
        {
        }

        /// <summary>
        /// Clears only the data from all outputs while preserving runtime messages.
        /// </summary>
        protected virtual void ClearDataOnly()
        {
            Debug.WriteLine($"[AsyncComponentBase] Cleaning Output Data Only");

            // Clear output data
            for (int i = 0; i < this.Params.Output.Count; i++)
            {
                this.Params.Output[i].ClearData();
            }
        }
    }
}
