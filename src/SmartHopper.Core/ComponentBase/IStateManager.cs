/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * Defines the contract for managing component states throughout their lifecycle.
 * The state manager is responsible for tracking, transitioning, and validating
 * component states while ensuring thread safety and proper state transitions.
 */

using System;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Interface for managing component states. Implement this to provide state
    /// management capabilities to your components.
    /// </summary>
    public interface IStateManager
    {
        /// <summary>
        /// Gets the current state of the component.
        /// </summary>
        ComponentState CurrentState { get; }

        /// <summary>
        /// Attempts to transition the component to a new state.
        /// Should validate the transition and handle thread synchronization.
        /// </summary>
        /// <param name="newState">The target state to transition to</param>
        void TransitionTo(ComponentState newState);

        /// <summary>
        /// Checks if a transition to the specified state is valid from the current state.
        /// Implement state machine rules here.
        /// </summary>
        /// <param name="newState">The state to check</param>
        /// <returns>True if the transition is valid</returns>
        bool CanTransitionTo(ComponentState newState);

        /// <summary>
        /// Event that fires when the component's state changes.
        /// Subscribers can use this to react to state changes.
        /// </summary>
        event Action<ComponentState> StateChanged;
    }

    /// <summary>
    /// Defines the possible states a component can be in.
    /// Add new states here as needed for your specific requirements.
    /// </summary>
    public enum ComponentState
    {
        /// <summary>
        /// Component needs to be executed
        /// </summary>
        NeedsRun,

        /// <summary>
        /// Component is currently processing
        /// </summary>
        Processing,

        /// <summary>
        /// Component has completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Component execution was cancelled
        /// </summary>
        Cancelled,

        /// <summary>
        /// Component encountered an error
        /// </summary>
        Error
    }
}
