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
using SmartHopper.Core.Async.Core.StateManagement;

namespace SmartHopper.Core.Async.Core
{
    public class ComponentMessaging
    {
        private readonly GH_Component _component;
        private readonly Action<string> _progressReporter;
        private readonly IComponentStateManager _stateManager;

        public ComponentMessaging(GH_Component component, Action<string> progressReporter = null, IComponentStateManager stateManager = null)
        {
            _component = component;
            _progressReporter = progressReporter;
            _stateManager = stateManager;
        }

        public void UpdateMessage(ComponentState state)
        {
            switch (state)
            {
                case ComponentState.NeedsRun:
                case ComponentState.NeedsRerun:
                    _component.Message = "Run me!";
                    _component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Set Run to True to recompute the results");
                    break;
                case ComponentState.Processing:
                    _component.Message = "Processing...";
                    break;
                case ComponentState.Completed:
                case ComponentState.Waiting:
                    _component.Message = "Done";
                    break;
                case ComponentState.Error:
                    _component.Message = "Error";
                    break;
                case ComponentState.Cancelled:
                    _component.Message = "Cancelled";
                    break;
            }
            Debug.WriteLine("COMPONENT STATE CHANGED TO: " + state);
        }

        /// <summary>
        /// Reports progress with an optional message.
        /// </summary>
        /// <param name="message">Optional progress message. If null or empty, shows "Working..."</param>
        public void ReportProgress(string message)
        {
            var displayMessage = string.IsNullOrWhiteSpace(message) ? "Working..." : message;
            _component.Message = displayMessage;
            _progressReporter?.Invoke(displayMessage);
        }

        /// <summary>
        /// Reports a message with the specified severity level to the component.
        /// </summary>
        /// <param name="level">The severity level of the message</param>
        /// <param name="message">The message to report</param>
        private void Report(GH_RuntimeMessageLevel level, string message)
        {
            _component.AddRuntimeMessage(level, message);
            if (level == GH_RuntimeMessageLevel.Error && _stateManager != null)
            {
                _stateManager.HandleRuntimeError();
            }
        }

        /// <summary>
        /// Reports an error message to the component.
        /// </summary>
        /// <param name="message">The error message to report</param>
        public void ReportError(string message)
        {
            Report(GH_RuntimeMessageLevel.Error, message);
        }

        /// <summary>
        /// Reports a warning message to the component.
        /// </summary>
        /// <param name="message">The warning message to report</param>
        public void ReportWarning(string message)
        {
            Report(GH_RuntimeMessageLevel.Warning, message);
        }

        /// <summary>
        /// Reports a remark message to the component.
        /// </summary>
        /// <param name="message">The remark message to report</param>
        public void ReportRemark(string message)
        {
            Report(GH_RuntimeMessageLevel.Remark, message);
        }
    }
}
