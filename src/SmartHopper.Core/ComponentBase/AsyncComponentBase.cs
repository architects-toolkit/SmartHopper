/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 * 
 * Portions of this code adapted from:
 * https://github.com/specklesystems/GrasshopperAsyncComponent
 * Apache License 2.0
 * Copyright (c) 2021 Speckle Systems
 */

/*
 * Base class for all asynchronous components.
 * This class provides the fundamental structure for components that need to perform
 * asynchronous operations while maintaining Grasshopper's component lifecycle.
 */

using Grasshopper.Kernel;
using System;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for asynchronous Grasshopper components. Inherit from this class
    /// when you need to create a component that performs long-running operations.
    /// </summary>
    public abstract class AsyncComponentBase : GH_Component
    {
        // These services will be implemented later
        //private readonly ICancellationManager _cancellationManager;
        //private readonly IErrorHandler _errorHandler;
        //private readonly IMessagingService _messagingService;
        //private readonly IStateManager _stateManager;
        
        /// <summary>
        /// The current worker instance
        /// </summary>
        protected AsyncWorkerBase _worker;

        /// <summary>
        /// Gets whether the component is in pre-solve phase
        /// </summary>
        protected bool InPreSolve { get; private set; }

        /// <summary>
        /// Gets whether the component is in post-solve phase
        /// </summary>
        protected bool InPostSolve { get; private set; }

        /// <summary>
        /// Constructor for AsyncComponentBase.
        /// </summary>
        /// <param name="name">The display name of the component</param>
        /// <param name="nickname">The shortened display name</param>
        /// <param name="description">Description of the component's function</param>
        /// <param name="category">The tab category where the component appears</param>
        /// <param name="subCategory">The sub-category within the tab</param>
        protected AsyncComponentBase(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
        }

        /// <summary>
        /// Creates a new worker instance for this component.
        /// </summary>
        protected abstract AsyncWorkerBase CreateWorker(Action<string> progressReporter);

        /// <summary>
        /// Handles the component's solve instance, managing the pre-solve and post-solve phases.
        /// </summary>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (_worker == null)
            {
                _worker = CreateWorker(m => Message = m);
            }

            if (InPreSolve)
            {
                // Collect data
                _worker?.GatherInput(DA);
                return;
            }

            if (InPostSolve)
            {
                string message = string.Empty;
                _worker?.SetOutput(DA, out message);
                if (!string.IsNullOrEmpty(message))
                    Message = message;
                return;
            }

            // First pass - Pre-solve
            InPreSolve = true;
            InPostSolve = false;
            OnSolveInstance(DA);

            // Second pass - Post-solve
            InPreSolve = false;
            InPostSolve = true;
            OnSolveInstance(DA);

            // Reset state
            InPreSolve = false;
            InPostSolve = false;
        }

        /// <summary>
        /// Override this method to implement custom solve logic.
        /// This will be called twice: once for pre-solve and once for post-solve.
        /// </summary>
        protected abstract void OnSolveInstance(IGH_DataAccess DA);
    }
}
