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
        /// <summary>
        /// Initial state. All workers finished, output the previous results, if any.
        /// </summary>
        Completed,

        /// <summary>
        /// When running with a toggle set to True, waiting for input changes. On next SolveInstance (means input changed), transition to NeedsRun.
        /// </summary>
        Waiting,

        /// <summary>
        /// When running with a button, Run = False. On input changes && Run = True, transition to Processing.
        /// </summary>
        NeedsRun,

        /// <summary>
        /// Run async work, transition to Completed when all workers finish.
        /// </summary>
        Processing,

        /// <summary>
        /// Manually cancelled, add error and transition to Waiting.
        /// </summary>
        Cancelled,

        /// <summary>
        /// An error occurred, add error and transition to Waiting.
        /// </summary>
        Error,
    }

    /// <summary>
    /// Extension methods for ComponentState enum.
    /// </summary>
    public static class ComponentStateExtensions
    {
        /// <summary>
        /// Gets a user-friendly string representation of the ComponentState.
        /// </summary>
        /// <param name="state">The component state.</param>
        /// <returns>A formatted state message string.</returns>
        public static string ToMessageString(this ComponentState state)
        {
            return ToMessageString(state, null);
        }

        /// <summary>
        /// Gets a user-friendly string representation of the ComponentState with optional progress information.
        /// </summary>
        /// <param name="state">The component state.</param>
        /// <param name="progressInfo">Optional progress information for dynamic messages.</param>
        /// <returns>A formatted state message string.</returns>
        public static string ToMessageString(this ComponentState state, ProgressInfo progressInfo)
        {
            switch (state)
            {
                case ComponentState.Waiting:
                    return "Done";
                case ComponentState.NeedsRun:
                    return "Run me!";
                case ComponentState.Processing:
                    if (progressInfo?.IsActive == true)
                    {
                        return $"Process {progressInfo.ProgressString}...";
                    }

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
