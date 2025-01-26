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
        private bool _IsResultOut;

        // Error tracking fields
        private bool _hasTemporaryError;
        private bool _hasPersistentError;
        private string _lastErrorMessage;

        public ComponentStateManager(GH_Component component)
        {
            _component = component ?? throw new ArgumentNullException(nameof(component));
            _messaging = new ComponentMessaging(component);
            _currentState = ComponentState.NeedsRun;
            ResetStateFlags();
            _messaging.UpdateMessage(_currentState);
        }

        private void ResetStateFlags()
        {
            _hasTemporaryError = false;
            _hasPersistentError = false;
            _lastErrorMessage = string.Empty;
            
        }

        public ComponentState CurrentState => _currentState;
        public bool HasTemporaryError => _hasTemporaryError;
        public bool HasPersistentError => _hasPersistentError;
        public string LastErrorMessage => _lastErrorMessage;

        public bool IsResultOut
        {
            get => _IsResultOut;
            set
            {
                if (_IsResultOut != value)
                {
                    _IsResultOut = value;
                    Debug.WriteLine($"[ComponentStateManager] IsResultOut changed to: {value}");
                }
            }
        }

        public event Action<ComponentState> StateChanged;

        public void TransitionTo(ComponentState newState)
        {
            Debug.WriteLine($"[ComponentStateManager] Transition to: {newState} --- HasTemporaryError: {HasTemporaryError}, HasPersistentError: {HasPersistentError}");

            _messaging.UpdateMessage(newState);

            if (_currentState == newState) return;
            _currentState = newState;
            StateChanged?.Invoke(newState);
        }

        public void SetError(string message, bool isPersistent = false)
        {
            _lastErrorMessage = message;
            if (isPersistent)
            {
                _hasPersistentError = true;
                _hasTemporaryError = false;
            }
            else
            {
                _hasTemporaryError = true;
            }
            TransitionTo(ComponentState.Error);
        }

        public void ClearErrors()
        {
            _hasTemporaryError = false;
            _hasPersistentError = false;
            _lastErrorMessage = string.Empty;
        }

        public void HandleRuntimeError()
        {
            SetError("Runtime error occurred", true);
        }
    }
}
