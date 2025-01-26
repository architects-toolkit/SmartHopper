/* DEPRECATED */

/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Rhino;

namespace SmartHopper.Core.Async.Threading
{
    public class TaskManager
    {
        private CancellationTokenSource _cts;
        private readonly SynchronizationContext _syncContext;
        
        public TaskManager()
        {
            _cts = new CancellationTokenSource();
            _syncContext = SynchronizationContext.Current;
        }

        public Task RunAsync(Func<CancellationToken, Task> work, IProgress<string> progress)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            return Task.Run(() => work(token), token)
                .ContinueWith(HandleTaskCompletion);
        }

        public void Cancel() 
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        private void HandleTaskCompletion(Task task) 
        {
            if (task.IsFaulted)
            {
                var errorMessage = task.Exception?.InnerException?.Message ?? "Unknown error occurred";
                RhinoApp.InvokeOnUiThread(new Action(() => OnTaskError?.Invoke(errorMessage)));
            }
            else if (task.IsCanceled)
            {
                RhinoApp.InvokeOnUiThread(new Action(() => OnTaskCancelled?.Invoke()));
            }
            else
            {
                RhinoApp.InvokeOnUiThread(new Action(() => OnTaskCompleted?.Invoke()));
            }
        }

        public event Action<string> OnTaskError;
        public event Action OnTaskCancelled;
        public event Action OnTaskCompleted;

        public bool IsCancellationRequested => _cts?.IsCancellationRequested ?? false;
        public CancellationToken Token => _cts?.Token ?? CancellationToken.None;
    }
}
