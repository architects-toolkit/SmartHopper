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
    /// Defines the possible states a component can be in.
    /// Add new states here as needed for your specific requirements.
    /// </summary>
    public enum ComponentState
    {
        Waiting, // Initial state. Output the previous results, is any. On next SolveInstance, transition to NeedsRun if any input changed
        NeedsRun, // Initial state or Run = False. On input changes && Run = True, transition to Processing
        Processing, // Run async work, transition to Completed when all workers finish
        Completed, // All workers finished, transition to Output
        Cancelled, // Manually cancelled, add error and transition to Waiting
        Error // An error occurred, add error and transition to Waiting
    }
}
