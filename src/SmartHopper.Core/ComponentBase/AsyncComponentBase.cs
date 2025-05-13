/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

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
            if (_state > 0 && _setData == 1)
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
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Debug.WriteLine($"[AsyncComponentBase] SolveInstance - State: {_state}, Tasks: {_tasks.Count}, SetData: {_setData}");

            // Initial run
            if (_state == 0 && _tasks.Count == 0)
            {
                Debug.WriteLine($"[AsyncComponentBase] Creating a new worker, State: {_state}, Tasks: {_tasks.Count}, SetData: {_setData}, Workers: {Workers.Count}, CancellationSources: {_cancellationSources.Count}, CurrentWorker: {CurrentWorker != null}, Message: {Message}");

                // First pass - Pre-solve
                InPreSolve = true;

                // Create a new worker and add it to the list
                var worker = CreateWorker(s => Message = s);
                Workers.Add(worker);

                Debug.WriteLine("[AsyncComponentBase] Gathering input");

                // Gather input before starting the task
                worker.GatherInput(DA);
                CurrentWorker = worker;

                // Create cancellation token source
                var source = new CancellationTokenSource();
                _cancellationSources.Add(source);

                // Create task that properly awaits the async work
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await worker.DoWorkAsync(source.Token);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AsyncComponentBase] Task failed with error: {ex.Message}");
                        throw; // Re-throw to be caught by ContinueWith
                    }
                });

                // Add task to the list
                _tasks.Add(task);

                OnSolveInstancePreSolve(DA);
                return; // Jump to AfterSolveInstance to execute tasks
            }

            /* _state != 0 || _tasks.Count != 0 */

            // Second pass - Post-solve - Setting output
            InPreSolve = false;

            Debug.WriteLine($"[AsyncComponentBase] Post-solve - Setting output. InPreSolve: {InPreSolve}, State: {_state}, SetData: {_setData}, Workers.Count: {Workers.Count}");

            if (Workers.Count > 0)
            {
                // Call SetOutput for each worker in reverse order
                for (int i = Workers.Count - 1; i >= 0; i--)
                {
                    Debug.WriteLine($"[AsyncComponentBase] Setting output for worker {i + 1}/{Workers.Count}");
                    string outMessage = null;

                    // Ensure SetOutput runs on UI thread
                    Rhino.RhinoApp.InvokeOnUiThread(() =>
                    {
                        Workers[i].SetOutput(DA, out outMessage);
                        Message = outMessage;
                        Debug.WriteLine($"[AsyncComponentBase] Worker {i + 1} output set, message: {outMessage}");
                    });

                    Interlocked.Decrement(ref _state);
                }

                Debug.WriteLine($"[AsyncComponentBase] All workers output set. Final state: {_state}");
                OnSolveInstancePostSolve(DA);
            }

            if (_state != 0)
                return; // Call SolveInstanve again until state is 0

            // Clean up
            _cancellationSources.Clear();
            Workers.Clear();
            _tasks.Clear();

            Interlocked.Exchange(ref _setData, 0);

            OnWorkerCompleted();
        }

        protected override void AfterSolveInstance()
        {
            Debug.WriteLine($"[AsyncComponentBase] AfterSolveInstance - State: {_state}, Tasks: {_tasks.Count}, SetData: {_setData}");

            if (_state == 0 && _tasks.Count > 0 && _setData == 0)
            {
                Debug.WriteLine($"[AsyncComponentBase] Starting {_tasks.Count} tasks");

                // Create a continuation task that will handle completion of all tasks
                Task.WhenAll(_tasks)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Debug.WriteLine($"[AsyncComponentBase] Task exceptions occurred:");
                            var ae = t.Exception;
                            foreach (var ex in ae.InnerExceptions)
                            {
                                Debug.WriteLine($"[AsyncComponentBase] - {ex.Message}");
                                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Task error: {ex.Message}");
                            }
                            // Ensure state is valid even on error
                            if (_state == 0)
                            {
                                _state = Workers.Count;
                                _setData = 1;
                            }
                        }
                        else
                        {
                            // Only increment state and set data if we haven't already
                            if (_state == 0 && _setData == 0)
                            {
                                Interlocked.Increment(ref _state);
                                if (_state == Workers.Count)
                                {
                                    Interlocked.Exchange(ref _setData, 1);
                                    Workers.Reverse();
                                }
                            }

                            Debug.WriteLine($"[AsyncComponentBase] All tasks completed successfully. State: {_state}, SetData: {_setData}, Workers: {Workers.Count}");
                        }

                        // Schedule component update on UI thread
                        Rhino.RhinoApp.InvokeOnUiThread(() =>
                        {
                            ExpireSolution(true);
                        });
                    }, TaskScheduler.Default);
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
            Debug.WriteLine($"[{GetType().Name}] All workers completed. State: {_state}, Tasks: {_tasks.Count}, SetData: {_setData}");
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
        protected virtual void ClearDataOnly()
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
