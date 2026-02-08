/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
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
        // Backing fields to avoid visible instance fields (CA1051)
        private readonly GH_Component _parent;
        private readonly Action<GH_RuntimeMessageLevel, string> _addRuntimeMessage;

        /// <summary>
        /// Gets the parent component instance that owns this worker.
        /// </summary>
        protected GH_Component Parent => this._parent;

        /// <summary>
        /// Provides a helper to add runtime messages from the worker in a standardized way.
        /// </summary>
        protected Action<GH_RuntimeMessageLevel, string> AddRuntimeMessage => this._addRuntimeMessage;

        protected AsyncWorkerBase(
            GH_Component parent,
            Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
        {
            this._parent = parent;
            this._addRuntimeMessage = addRuntimeMessage;
        }

        /// <summary>
        /// Gather input data from the component's input parameters.
        /// </summary>
        public abstract void GatherInput(IGH_DataAccess DA, out int dataCount);

        /// <summary>
        /// Perform the asynchronous computation.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public abstract Task DoWorkAsync(CancellationToken token);

        /// <summary>
        /// Set the output data to the component's output parameters.
        /// </summary>
        public abstract void SetOutput(IGH_DataAccess DA, out string message);
    }
}
