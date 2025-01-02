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

namespace SmartHopper.Core.Async.Core.StateManagement
{
    /// <summary>
    /// Represents the possible states of an asynchronous component.
    /// </summary>
    public enum ComponentState
    {
        NeedsRun,    // Initial state, ready to start
        Processing,  // Currently processing
        Completed,   // Just finished, has results to output
        Waiting,     // Results output done, waiting for input changes
        Error,       // Error occurred
        Cancelled,   // Manually cancelled
        NeedsRerun   // When inputs change but run is false, wait until run is true
    }
}
