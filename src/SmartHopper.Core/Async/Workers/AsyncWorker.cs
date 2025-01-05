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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using SmartHopper.Core.Async.Core;
using SmartHopper.Core.Async.Components;
using SmartHopper.Core.Async.Core.StateManagement;

namespace SmartHopper.Core.Async.Workers
{
    public abstract class AsyncWorker : IAsyncWorker
    {
        protected readonly GH_Component _parent;
        protected readonly ComponentMessaging _messaging;

        public AsyncWorker(Action<string> progressReporter, GH_Component parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
        {
            _parent = parent;
            var stateManager = parent as IComponentStateManager ?? (parent as AsyncStatefulComponentBase)?.StateManager;
            _messaging = new ComponentMessaging(parent, progressReporter, stateManager);
        }

        bool IAsyncWorker.IsDone => IsDone;

        internal bool IsDone { get; private set; }

        public abstract Task DoWorkAsync(CancellationToken token);
        public abstract void GatherInput(IGH_DataAccess data);
        public abstract void SetOutput(IGH_DataAccess data, out string doneMessage);

        public virtual bool ShouldStartWork => true;

        protected void Report(GH_RuntimeMessageLevel level, string message)
        {
            switch (level)
            {
                case GH_RuntimeMessageLevel.Error:
                    _messaging.ReportError(message);
                    break;
                case GH_RuntimeMessageLevel.Warning:
                    _messaging.ReportWarning(message);
                    break;
                case GH_RuntimeMessageLevel.Remark:
                    _messaging.ReportRemark(message);
                    break;
            }
            OnMessageReported(level, message);
        }

        protected virtual void OnMessageReported(GH_RuntimeMessageLevel level, string message)
        {
            // Base implementation does nothing
        }

        protected void ReportProgress(string message)
        {
            _messaging.ReportProgress(message);
        }

        protected void ReportError(string message) => Report(GH_RuntimeMessageLevel.Error, message);
        protected void ReportWarning(string message) => Report(GH_RuntimeMessageLevel.Warning, message);
        protected void ReportRemark(string message) => Report(GH_RuntimeMessageLevel.Remark, message);

        internal void SetDone()
        {
            Debug.WriteLine("[AsyncWorker] SetDone called");
            IsDone = true;
            Debug.WriteLine($"[AsyncWorker] IsDone set to: {IsDone}");
        }

        protected virtual void OnWorkCompleted()
        {
            Debug.WriteLine("[AsyncWorker] OnWorkCompleted called");
            SetDone();
        }

        protected virtual void OnCancelled()
        {
            // Base implementation does nothing
        }
    }
}
