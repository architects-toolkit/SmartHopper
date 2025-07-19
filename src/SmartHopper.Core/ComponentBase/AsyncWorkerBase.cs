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
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for asynchronous workers that handle the computation logic for async components.
    /// </summary>
    public abstract class AsyncWorkerBase
    {
        protected readonly Action<string> ReportProgress;
        protected readonly GH_Component Parent;
        protected readonly Action<GH_RuntimeMessageLevel, string> AddRuntimeMessage;

        protected AsyncWorkerBase(
            //Action<string> progressReporter,
            GH_Component parent,
            Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
        {
            //ReportProgress = progressReporter;
            this.Parent = parent;
            this.AddRuntimeMessage = addRuntimeMessage;
        }

        /// <summary>
        /// Gather input data from the component's input parameters.
        /// </summary>
        public abstract void GatherInput(IGH_DataAccess DA);

        /// <summary>
        /// Perform the asynchronous computation.
        /// </summary>
        public abstract Task DoWorkAsync(CancellationToken token);

        /// <summary>
        /// Set the output data to the component's output parameters.
        /// </summary>
        public abstract void SetOutput(IGH_DataAccess DA, out string message);
    }
}
