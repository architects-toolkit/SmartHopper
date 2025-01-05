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
 * https://github.com/lamest/AsyncComponent
 * MIT License
 * Copyright (c) 2022 Ivan Sukhikh
 */

using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;

namespace SmartHopper.Core.Async.Workers
{
    /// <summary>
    /// Defines the contract for asynchronous workers that handle component computation.
    /// </summary>
    public interface IAsyncWorker
    {
        /// <summary>
        /// Gets whether the worker should start processing.
        /// </summary>
        bool ShouldStartWork { get; }

        /// <summary>
        /// Gets whether the worker has completed its work.
        /// </summary>
        bool IsDone { get; }

        /// <summary>
        /// Performs the asynchronous work.
        /// </summary>
        /// <param name="token">Cancellation token to cancel the operation.</param>
        Task DoWorkAsync(CancellationToken token);

        /// <summary>
        /// Gathers input data from the component.
        /// </summary>
        /// <param name="data">Data access object for getting input parameters.</param>
        void GatherInput(IGH_DataAccess data);

        /// <summary>
        /// Sets the output data on the component.
        /// </summary>
        /// <param name="data">Data access object for setting output parameters.</param>
        /// <param name="doneMessage">Message to display when work is complete.</param>
        void SetOutput(IGH_DataAccess data, out string doneMessage);
    }
}
