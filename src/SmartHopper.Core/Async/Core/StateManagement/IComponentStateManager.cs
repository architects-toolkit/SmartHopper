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
    /// Defines the contract for managing component states and their transitions.
    /// </summary>
    public interface IComponentStateManager
    {
        /// <summary>
        /// Gets the current state of the component.
        /// </summary>
        ComponentState CurrentState { get; }

        /// <summary>
        /// Core state tracking properties
        /// </summary>
        bool IsResultOut { get; set; }

        /// <summary>
        /// Error management properties
        /// </summary>
        bool HasTemporaryError { get; }
        bool HasPersistentError { get; }
        string LastErrorMessage { get; }

        /// <summary>
        /// Transitions the component to a new state.
        /// </summary>
        /// <param name="newState">The target state to transition to.</param>
        void TransitionTo(ComponentState newState);

        /// <summary>
        /// Sets an error state with the specified message
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="isPersistent">If true, marks as persistent error requiring user intervention</param>
        void SetError(string message, bool isPersistent = false);

        /// <summary>
        /// Clears all error states
        /// </summary>
        void ClearErrors();

        /// <summary>
        /// Event raised when the component's state changes.
        /// </summary>
        event Action<ComponentState> StateChanged;

        /// <summary>
        /// Updates state to Error if a runtime error occurs
        /// </summary>
        void HandleRuntimeError();
    }
}
