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
using SmartHopper.Core.Async.Components;
using SmartHopper.Core.Async.Core.StateManagement;
using SmartHopper.Core.Async.Workers;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace SmartHopper.Components.Misc
{
    /// <summary>
    /// Example component implementing SimpleAsyncComponentBase with state management to calculate the nth prime number.
    /// </summary>
    public class StatefulPrimeCalculatorComponent : SimpleAsyncComponentBase
    {
        private readonly IComponentStateManager _stateManager;
        private DateTime _lastCompletionTime = DateTime.MinValue;
        private const int DEBOUNCE_MS = 100;
        protected IGH_DataAccess DataAccess { get; private set; }

        public override Guid ComponentGuid => new Guid("5CDBF155-BE50-4921-8D0F-48D063F722A0");

        protected override System.Drawing.Bitmap Icon => null;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        public ComponentState GetCurrentState() => _stateManager.CurrentState;

        public StatefulPrimeCalculatorComponent() 
            : base("The N-th Prime Calculator (Stateful)", "PRIME", 
                  "Calculates the nth prime number with state management.", 
                  "SmartHopper", "Examples")
        {
            _stateManager = new ComponentStateManager(this);
            _stateManager.StateChanged += OnStateChanged;
            base.StateManager = _stateManager;
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter("N", "N", "Which n-th prime number. Minimum 1, maximum one million.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Output", "O", "The n-th prime number.", GH_ParamAccess.item);
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendItem(menu, "Cancel", (s, e) =>
            {
                RequestTaskCancellation();
                _stateManager.TransitionTo(ComponentState.Cancelled);
            });
        }

        protected virtual void OnStateChanged(ComponentState newState)
        {
            if (newState == ComponentState.Completed)
            {
                var now = DateTime.Now;
                if ((now - _lastCompletionTime).TotalMilliseconds > DEBOUNCE_MS)
                {
                    _lastCompletionTime = now;
                    Debug.WriteLine("[StatefulPrimeCalculator] Worker completed");
                    ExpireSolution(true);
                }
            }
        }

        protected override AsyncWorker CreateWorker(Action<string> progressReporter)
        {
            return new StatefulPrimeCalculatorWorker(progressReporter, this, AddRuntimeMessage, _stateManager);
        }

        private class StatefulPrimeCalculatorWorker : AsyncWorker
        {
            private int _nthPrime = 100;
            private long _result = -1;
            private readonly IComponentStateManager _stateManager;

            public StatefulPrimeCalculatorWorker(
                Action<string> progressReporter,
                GH_Component parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage,
                IComponentStateManager stateManager)
                : base(progressReporter, parent, addRuntimeMessage)
            {
                _stateManager = stateManager;
            }

            public override void GatherInput(IGH_DataAccess DA)
            {
                int n = 100;
                DA.GetData(0, ref n);
                
                // Clamp values
                _nthPrime = Math.Max(1, Math.Min(n, 1000000));
                _stateManager.TransitionTo(ComponentState.Processing);
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    int count = 0;
                    long a = 2;
                    var options = new ParallelOptions { CancellationToken = token };

                    while (count < _nthPrime)
                    {
                        token.ThrowIfCancellationRequested();
                        bool isPrime = true;

                        // Parallel check for factors
                        await Task.Run(() =>
                        {
                            Parallel.For(2, (int)Math.Sqrt(a) + 1, options, (b, loopState) =>
                            {
                                if (a % b == 0)
                                {
                                    isPrime = false;
                                    loopState.Stop();
                                }
                            });
                        }, token);

                        if (isPrime)
                        {
                            count++;
                            // ReportProgress($"{((double)count / _nthPrime * 100):F2}%");
                        }
                        a++;

                        if (count % 100 == 0)
                            await Task.Delay(1, token);
                    }
                    _result = --a;
                    
                    // Ensure state transition happens on UI thread
                    Rhino.RhinoApp.InvokeOnUiThread(new Action(() => _stateManager.TransitionTo(ComponentState.Completed)));
                }
                catch (OperationCanceledException)
                {
                    Rhino.RhinoApp.InvokeOnUiThread(new Action(() => _stateManager.TransitionTo(ComponentState.Cancelled)));
                    throw;
                }
                catch (Exception ex)
                {
                    Rhino.RhinoApp.InvokeOnUiThread(new Action(() => 
                    {
                        _parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                        _stateManager.HandleRuntimeError();
                    }));
                    throw;
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                DA.SetData(0, _result);
                message = $"Found {_nthPrime}th prime: {_result}";
            }
        }
    }
}
