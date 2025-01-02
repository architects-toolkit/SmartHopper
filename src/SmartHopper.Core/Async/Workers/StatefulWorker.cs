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

using System;
using Grasshopper.Kernel;
using SmartHopper.Core.Async.Components;
using Rhino;
using SmartHopper.Core.Async.Core.StateManagement;

namespace SmartHopper.Core.Async.Workers
{
    public abstract class StatefulWorker : AsyncWorker
    {
        protected readonly IComponentStateManager _stateManager;

        protected StatefulWorker(
            Action<string> progressReporter,
            AsyncStatefulComponentBase parent,
            Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
            : base(progressReporter, parent, addRuntimeMessage)
        {
            _stateManager = parent.StateManager;
        }

        public override bool ShouldStartWork =>
            _stateManager.CurrentState == ComponentState.Processing;

        protected override void OnMessageReported(GH_RuntimeMessageLevel level, string message)
        {
            if (level == GH_RuntimeMessageLevel.Error)
            {
                _stateManager.HandleRuntimeError();
            }
        }

        protected override void OnWorkCompleted()
        {
            base.OnWorkCompleted();

            // Use BeginInvoke to ensure we're on the UI thread
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                _stateManager.TransitionTo(ComponentState.Completed);
            }));
        }

        protected override void OnCancelled()
        {
            base.OnCancelled();
            _stateManager.TransitionTo(ComponentState.Cancelled);
        }
    }
}
