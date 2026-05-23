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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using SmartHopper.Infrastructure.Diagnostics;

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
        private readonly List<SHRuntimeMessage> _collectedMessages = new List<SHRuntimeMessage>();

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
        /// Thread-safe method to collect a structured runtime message from background worker code.
        /// Messages are flushed to the UI thread by <see cref="FlushCollectedMessages"/> after <see cref="SetOutput"/>.
        /// </summary>
        /// <param name="message">The message to collect.</param>
        protected void CollectMessage(SHRuntimeMessage message)
        {
            lock (this._collectedMessages)
            {
                this._collectedMessages.Add(message);
            }
        }

        /// <summary>
        /// Thread-safe convenience overload to collect a runtime message from background worker code.
        /// </summary>
        /// <param name="severity">The message severity.</param>
        /// <param name="message">The message text.</param>
        /// <param name="origin">The message origin (defaults to Worker).</param>
        protected void CollectMessage(SHRuntimeMessageSeverity severity, string message, SHRuntimeMessageOrigin origin = SHRuntimeMessageOrigin.Worker)
        {
            this.CollectMessage(new SHRuntimeMessage(severity, origin, SHMessageCode.Unknown, message));
        }

        /// <summary>
        /// Flushes all collected messages to the GH component as runtime messages.
        /// Must be called from the UI thread (after <see cref="SetOutput"/>).
        /// Does NOT clear the internal list — call <see cref="ResetCollectedMessages"/> after
        /// <see cref="PromoteCollectedToPersistent"/> has had a chance to run.
        /// </summary>
        internal void FlushCollectedMessages()
        {
            lock (this._collectedMessages)
            {
                foreach (var m in this._collectedMessages)
                {
                    if (!m.Surfaceable)
                    {
                        continue;
                    }

                    var level = m.Severity switch
                    {
                        SHRuntimeMessageSeverity.Error => GH_RuntimeMessageLevel.Error,
                        SHRuntimeMessageSeverity.Warning => GH_RuntimeMessageLevel.Warning,
                        _ => GH_RuntimeMessageLevel.Remark,
                    };

                    this._addRuntimeMessage(level, m.Message);
                }
            }
        }

        /// <summary>
        /// Clears the collected messages list. Called automatically by AsyncComponentBase after GatherInput.
        /// </summary>
        internal void ResetCollectedMessages()
        {
            lock (this._collectedMessages)
            {
                this._collectedMessages.Clear();
            }
        }

        /// <summary>
        /// Iterates the collected messages and invokes <paramref name="persist"/> for each surfaceable one,
        /// allowing the caller to promote them to a persistent keyed store (e.g. SetPersistentRuntimeMessage).
        /// Does NOT clear the internal list.
        /// </summary>
        /// <param name="persist">Callback receiving the GH level and message text for each surfaceable message.</param>
        internal void PromoteCollectedToPersistent(Action<GH_RuntimeMessageLevel, string> persist)
        {
            lock (this._collectedMessages)
            {
                foreach (var m in this._collectedMessages)
                {
                    if (!m.Surfaceable)
                    {
                        continue;
                    }

                    var level = m.Severity switch
                    {
                        SHRuntimeMessageSeverity.Error => GH_RuntimeMessageLevel.Error,
                        SHRuntimeMessageSeverity.Warning => GH_RuntimeMessageLevel.Warning,
                        _ => GH_RuntimeMessageLevel.Remark,
                    };

                    persist(level, m.Message);
                }
            }
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
