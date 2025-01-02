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
using Rhino;

namespace SmartHopper.Core.Async.Threading
{
    public class ProgressReporter : IProgress<string>
    {
        private readonly Action<string> _progressCallback;
        private readonly CancellationToken _cancellationToken;

        public ProgressReporter(Action<string> callback, CancellationToken token)
        {
            _progressCallback = callback;
            _cancellationToken = token;
        }

        public void Report(string value)
        {
            if (!_cancellationToken.IsCancellationRequested)
            {
                RhinoApp.InvokeOnUiThread(new Action(() => 
                {
                    if (!_cancellationToken.IsCancellationRequested)
                    {
                        _progressCallback?.Invoke(string.IsNullOrWhiteSpace(value) ? "Working..." : value);
                    }
                }));
            }
        }
    }
}
