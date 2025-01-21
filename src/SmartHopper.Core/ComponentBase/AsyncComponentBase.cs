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
 * https://github.com/specklesystems/GrasshopperAsyncComponent
 * Apache License 2.0
 * Copyright (c) 2021 Speckle Systems
 */

/*
 * Base class for all asynchronous components.
 * This class provides the fundamental structure for components that need to perform
 * asynchronous operations while maintaining Grasshopper's component lifecycle.
 */

using Grasshopper.Kernel;
using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

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
        /// Tracks the state of worker task completion:
        /// - Starts at 0 when component initializes
        /// - Increments when a worker task completes
        /// - Decrements when processing output from workers
        /// - When equals Workers.Count, all workers have completed
        /// This is used in conjunction with _setData to coordinate the async execution flow
        /// and ensure proper ordering of worker output processing.
        /// </summary>
        protected int _state;

        /// <summary>
        /// Flag indicating whether the component is ready to process worker outputs:
        /// - 0: Workers are still executing or haven't started
        /// - 1: All workers have completed and outputs can be processed
        /// Used as a latch to ensure outputs are processed only once and
        /// prevent re-execution of workers during the output phase.
        /// Set to 1 via Interlocked.Exchange when _state equals Workers.Count.
        /// </summary>
        protected int _setData;

        protected bool _inPreSolve;

        protected List<AsyncWorkerBase> Workers { get; private set; }
        protected AsyncWorkerBase CurrentWorker { get; private set; }

        // public bool ITaskCapable => true;
        // public virtual bool UseTasks { get; set; } = true;

        /// <summary>
        /// Gets whether the component is in pre-solve phase.
        /// Inherited from IGH_TaskCapableComponent.
        /// </summary>
        public bool InPreSolve
        {
            get => _inPreSolve;
            set => _inPreSolve = value;
        }

        /// <summary>
        /// Constructor for AsyncComponentBase.
        /// </summary>
        /// <param name="name">The display name of the component</param>
        /// <param name="nickname">The shortened display name</param>
        /// <param name="description">Description of the component's function</param>
        /// <param name="category">The tab category where the component appears</param>
        /// <param name="subCategory">The sub-category within the tab</param>
        protected AsyncComponentBase(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            _tasks = new List<Task>();
            _cancellationSources = new List<CancellationTokenSource>();
            Workers = new List<AsyncWorkerBase>();
        }

        /// <summary>
        /// Creates a new worker instance for this component.
        /// </summary>
        protected abstract AsyncWorkerBase CreateWorker(Action<string> progressReporter);

        protected override void BeforeSolveInstance()
        {
            if (_state != 0 && _setData == 1)
            {
                // Skip BeforeSolveInstance and jump to SolveInstance
                return;
            }

            Debug.WriteLine("[AsyncComponentBase] BeforeSolveInstance - Cleaning up previous run");
            foreach (var source in _cancellationSources)
            {
                source.Cancel();
            }

            _cancellationSources.Clear();
            _tasks.Clear();
            Workers.Clear();
            _state = 0;
            _setData = 0;
            //Message = string.Empty;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[AsyncComponentBase] SolveInstance - State: {_state}, Tasks: {_tasks.Count}, SetData: {_setData}");
            
            // Initial run
            if (_state == 0)
            {
                var worker = CreateWorker(m => Message = m);

                // Set up worker
                worker.GatherInput(DA);
                var tokenSource = new CancellationTokenSource();
                _cancellationSources.Add(tokenSource);

                // Create task
                var task = new Task(async () =>
                {
                    try
                    {
                        await worker.DoWorkAsync(tokenSource.Token);
                        Interlocked.Increment(ref _state);
                        if (_state == Workers.Count && _setData == 0)
                        {
                            Interlocked.Exchange(ref _setData, 1);
                            Workers.Reverse();
                            Rhino.RhinoApp.InvokeOnUiThread((Action)(() => ExpireSolution(true)));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine($"[AsyncComponentBase] Worker task cancelled");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AsyncComponentBase] Worker task failed: {ex.Message}");
                    }
                }, tokenSource.Token);

                Workers.Add(worker);
                CurrentWorker = worker;
                _tasks.Add(task);
                
                // First pass - Pre-solve
                InPreSolve = true;
                OnSolveInstancePreSolve(DA);
                return;
            }

            if (_setData == 0)
            {
                // Skip SolveInstance, jump to AfterSolveInstance
                return;
            }

            // Second pass - Post-solve
            InPreSolve = false;
            if (Workers.Count > 0)
            {
                Interlocked.Decrement(ref _state);
                string outMessage = null;
                Workers[_state].SetOutput(DA, out outMessage);
                //if (!string.IsNullOrEmpty(doneMessage))
                //    Message = doneMessage;
                
                OnSolveInstancePostSolve(DA);
            }

            if (_state != 0)
                return;

            // Clean up
            _cancellationSources.Clear();
            Workers.Clear();
            //_progressReports.Clear();
            _tasks.Clear();

            Interlocked.Exchange(ref _setData, 0);
            // Message = "Done";
            //OnDisplayExpired(true);

            OnWorkerCompleted();
        }

        protected override void AfterSolveInstance()
        {
            Debug.WriteLine($"[AsyncComponentBase] AfterSolveInstance - State: {_state}, Tasks: {_tasks.Count}, SetData: {_setData}");
            if (_state == 0 && _tasks.Count > 0 && _setData == 0)
            {
                // Run all tasks
                foreach (var task in _tasks)
                {
                    task.Start();
                }
            }
        }

        public virtual void RequestTaskCancellation()
        {
            foreach (var source in _cancellationSources)
            {
                source.Cancel();
            }
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "Cancel current process", (s, e) =>
            {
                RequestTaskCancellation();
            });
        }

        protected virtual void OnWorkerCompleted()
        {
            Debug.WriteLine($"[{GetType().Name}] All workers completed");
        }

        /// <summary>
        /// Override this method to implement custom solve logic.
        /// This will be called for pre-solve.
        /// </summary>
        protected virtual void OnSolveInstancePreSolve(IGH_DataAccess DA) { }

        /// <summary>
        /// Override this method to implement custom solve logic.
        /// This will be called for post-solve.
        /// </summary>
        protected virtual void OnSolveInstancePostSolve(IGH_DataAccess DA) { }

        /// <summary>
        /// Clears only the data from all outputs while preserving runtime messages
        /// </summary>
        protected void ClearDataOnly()
        {
            Debug.WriteLine($"[AsyncComponentBase] Cleaning Output Data Only");
            
            // Clear output data
            for (int i = 0; i < Params.Output.Count; i++)
            {
                Params.Output[i].ClearData();
            }
        }


    }
}
