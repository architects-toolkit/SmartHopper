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
        Completed, // Initial state. All workers finished, output the previous results, if any.
        Waiting, // When running with a toggle set to True, waiting for input changes.On next SolveInstance (means input changed), transition to NeedsRun
        NeedsRun, // When running with a button, Run = False. On input changes && Run = True, transition to Processing
        Processing, // Run async work, transition to Completed when all workers finish
        Cancelled, // Manually cancelled, add error and transition to Waiting
        Error // An error occurred, add error and transition to Waiting
    }

    /// <summary>
    /// Extension methods for ComponentState enum
    /// </summary>
    public static class ComponentStateExtensions
    {
        /// <summary>
        /// Gets a user-friendly string representation of the ComponentState
        /// </summary>
        public static string ToMessageString(this ComponentState state)
        {
            switch (state)
            {
                case ComponentState.Waiting:
                    return "Done";
                case ComponentState.NeedsRun:
                    return "Run me!";
                case ComponentState.Processing:
                    return "Processing...";
                case ComponentState.Completed:
                    return "Done";
                case ComponentState.Cancelled:
                    return "Cancelled";
                case ComponentState.Error:
                    return "Error";
                default:
                    return state.ToString();
            }
        }
    }
}
