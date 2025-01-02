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
using System.Diagnostics;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Async.Core.StateManagement
{
    /// <summary>
    /// Manages the state and state transitions of an asynchronous component.
    /// </summary>
    public class ComponentStateManager : IComponentStateManager
    {
        private readonly GH_Component _component;
        private ComponentState _currentState;
        private readonly ComponentMessaging _messaging;
        private bool _setData;

        public ComponentStateManager(GH_Component component)
        {
            _component = component ?? throw new ArgumentNullException(nameof(component));
            _messaging = new ComponentMessaging(component);
            _currentState = ComponentState.NeedsRun;
        }

        public ComponentState CurrentState => _currentState;

        public bool SetData
        {
            get => _setData;
            set
            {
                if (_setData != value)
                {
                    _setData = value;
                    Debug.WriteLine($"[ComponentStateManager] SetData changed to: {value}");
                }
            }
        }

        public event Action<ComponentState> StateChanged;

        public void TransitionTo(ComponentState newState)
        {
            _messaging.UpdateMessage(newState);

            if (_currentState == newState) return;

            _currentState = newState;
            StateChanged?.Invoke(newState);
        }

        /// <summary>
        /// Updates state to Error if a runtime error occurs
        /// </summary>
        public void HandleRuntimeError()
        {
            TransitionTo(ComponentState.Error);
        }
    }
}
