/* DEPRECATED */

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

using Grasshopper.Kernel;
using SmartHopper.Core.Async.Core.StateManagement;
using SmartHopper.Core.Async.Workers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace SmartHopper.Core.Async.Components
{
    /// <summary>
    /// A simplified base class for async components, adapted from Speckle Systems' implementation.
    /// Provides basic async functionality while maintaining compatibility with SmartHopper's state management.
    /// </summary>
    public abstract class SimpleAsyncComponentBase : GH_Component, IGH_TaskCapableComponent, IAsyncComponent
    {
        private readonly ConcurrentDictionary<string, double> _progressReports;
        private readonly List<Task> _tasks;
        private readonly List<CancellationTokenSource> _cancellationSources;
        private readonly Timer _displayProgressTimer;
        private int _state;
        private int _setData;
        private bool _inPreSolve;

        protected List<AsyncWorker> Workers { get; private set; }
        protected AsyncWorker CurrentWorker { get; private set; }
        public virtual IComponentStateManager StateManager { get; protected set; }

        public bool ITaskCapable => true;
        public virtual bool UseTasks { get; set; } = true;

        public bool InPreSolve
        {
            get => _inPreSolve;
            set => _inPreSolve = value;
        }

        protected SimpleAsyncComponentBase(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            _progressReports = new ConcurrentDictionary<string, double>();
            Workers = new List<AsyncWorker>();
            _cancellationSources = new List<CancellationTokenSource>();
            _tasks = new List<Task>();

            // Initialize progress timer
            _displayProgressTimer = new Timer(333) { AutoReset = false };
            _displayProgressTimer.Elapsed += DisplayProgress;

            // Initialize state manager
            StateManager = new ComponentStateManager(this);
        }

        protected abstract AsyncWorker CreateWorker(Action<string> progressReporter);

        protected virtual void DisplayProgress(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (Workers.Count == 0 || !_progressReports.Any())
                return;

            if (Workers.Count == 1)
            {
                Message = _progressReports.Values.Last().ToString("0.00%");
            }
            else
            {
                double total = _progressReports.Values.Sum();
                Message = (total / Workers.Count).ToString("0.00%");
            }

            Rhino.RhinoApp.InvokeOnUiThread((Action)(() => OnDisplayExpired(true)));
        }

        protected override void BeforeSolveInstance()
        {
            if (_state != 0 && _setData == 1)
                return;

            Debug.WriteLine("[SimpleAsyncComponentBase] Cleaning up previous run");
            foreach (var source in _cancellationSources)
            {
                source.Cancel();
            }

            _cancellationSources.Clear();
            Workers.Clear();
            _progressReports.Clear();
            _tasks.Clear();

            Interlocked.Exchange(ref _state, 0);
        }

        protected override void AfterSolveInstance()
        {
            Debug.WriteLine($"[SimpleAsyncComponentBase] AfterSolveInstance - State: {_state}, Tasks: {_tasks.Count}");
            if (_state == 0 && _tasks.Count > 0 && _setData == 0)
            {
                foreach (var task in _tasks)
                {
                    task.Start();
                }
            }
        }

        protected override void ExpireDownStreamObjects()
        {
            // Prevents data flashing until new solution is ready
            if (_setData == 1)
            {
                base.ExpireDownStreamObjects();
            }
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (_state == 0)
            {
                var worker = CreateWorker(progress =>
                {
                    string workerId = $"Worker-{DA.Iteration}";
                    _progressReports[workerId] = double.Parse(progress.TrimEnd('%')) / 100;
                    if (!_displayProgressTimer.Enabled)
                    {
                        _displayProgressTimer.Start();
                    }
                });

                if (worker == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not create worker instance.");
                    return;
                }

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
                    catch (Exception ex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                    }
                }, tokenSource.Token);

                Workers.Add(worker);
                CurrentWorker = worker;
                _tasks.Add(task);
                return;
            }

            if (_setData == 0)
                return;

            if (Workers.Count > 0)
            {
                Interlocked.Decrement(ref _state);
                string doneMessage = null;
                Workers[_state].SetOutput(DA, out doneMessage);
            }

            if (_state != 0)
                return;

            // Clean up
            _cancellationSources.Clear();
            Workers.Clear();
            _progressReports.Clear();
            _tasks.Clear();

            Interlocked.Exchange(ref _setData, 0);
            Message = "Done";
            OnDisplayExpired(true);
        }

        public void RequestTaskCancellation()
        {
            foreach (var source in _cancellationSources)
            {
                source.Cancel();
            }

            _cancellationSources.Clear();
            Workers.Clear();
            _progressReports.Clear();
            _tasks.Clear();

            Interlocked.Exchange(ref _state, 0);
            Interlocked.Exchange(ref _setData, 0);
            Message = "Cancelled";
            OnDisplayExpired(true);

            StateManager?.TransitionTo(ComponentState.Cancelled);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            StateManager?.TransitionTo(ComponentState.Waiting);
        }
    }
}
