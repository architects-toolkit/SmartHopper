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
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AIContext;

namespace SmartHopper.Components.AI
{
    /// <summary>
    /// Grasshopper component for retrieving all registered AI context providers.
    /// </summary>
    public class AIContextProvidersComponent : StatefulComponentBase
    {
        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("F8A2C4D1-3B5E-4A7F-9C0D-1E2F3A4B5C6D");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.contextproviders;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "Context Providers",
            "AI Context",
            "List Context Providers",
            "Get Context Providers",
            "Registered Context Providers",
        };

        /// <summary>
        /// Initializes a new instance of the AIContextProvidersComponent class.
        /// </summary>
        public AIContextProvidersComponent()
            : base(
                  "Context Providers",
                  "CxtProviders",
                  "Retrieve the list of all registered AI context providers.",
                  "SmartHopper",
                  "A. AI")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <summary>
        /// Registers additional input parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager.</param>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // No additional input parameters needed for this component
        }

        /// <summary>
        /// Registers additional output parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager.</param>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Providers", "P", "List of registered AI context provider IDs", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Creates the async worker for this component.
        /// </summary>
        /// <param name="progressReporter">Progress reporter callback.</param>
        /// <returns>The async worker instance.</returns>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIContextProvidersWorker(this, this.AddRuntimeMessage);
        }

        /// <summary>
        /// Async worker for the AI Context Providers component.
        /// </summary>
        private sealed class AIContextProvidersWorker : AsyncWorkerBase
        {
            private readonly AIContextProvidersComponent _parent;
            private readonly Dictionary<string, object> _result = new Dictionary<string, object>();

            public AIContextProvidersWorker(AIContextProvidersComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this._parent = parent;
            }

            /// <summary>
            /// Gathers input from the component.
            /// </summary>
            /// <param name="DA">Data access object.</param>
            /// <param name="dataCount">Output message.</param>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                // No inputs to gather for this component
                dataCount = 0;
            }

            /// <summary>
            /// Performs the work to retrieve registered context providers.
            /// </summary>
            /// <param name="token">Cancellation token.</param>
            /// <returns>A task representing the operation.</returns>
            public override Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    if (token.IsCancellationRequested)
                    {
                        return Task.CompletedTask;
                    }

                    var providers = AIContextManager.GetProviders();

                    var tree = new GH_Structure<GH_String>();
                    var path = new GH_Path(0);

                    if (providers != null && providers.Count > 0)
                    {
                        foreach (var provider in providers
                            .Select(p => p.ProviderId)
                            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
                        {
                            tree.Append(new GH_String(provider), path);
                        }

                        this._result["Providers"] = tree;
                        this._result["Success"] = true;
                        Debug.WriteLine($"[AIContextProvidersComponent] Found {providers.Count} registered context providers");
                    }
                    else
                    {
                        this._result["Success"] = true;
                        this._result["Providers"] = tree;
                        this._result["Info"] = "No context providers registered";
                        Debug.WriteLine("[AIContextProvidersComponent] No context providers registered");
                    }
                }
                catch (Exception ex)
                {
                    this._result["Success"] = false;
                    this._result["Error"] = ex.Message;
                    Debug.WriteLine("[AIContextProvidersComponent] Error: " + ex.Message);
                }

                return Task.CompletedTask;
            }

            /// <summary>
            /// Sets the output from the async work.
            /// </summary>
            /// <param name="DA">Data access object.</param>
            /// <param name="message">Output message.</param>
            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                if (this._result.TryGetValue("Success", out var success) && (bool)success)
                {
                    if (this._result.TryGetValue("Providers", out var providers) && providers is GH_Structure<GH_String> tree)
                    {
                        this._parent.SetPersistentOutput("Providers", tree, DA);

                        if (this._result.TryGetValue("Info", out var info) && info is string infoMsg)
                        {
                            this._parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, infoMsg);
                        }

                        message = "Context providers output set successfully";
                    }
                    else
                    {
                        message = "Error: No providers data available";
                    }
                }
                else
                {
                    var err = this._result.TryGetValue("Error", out var errObj) && errObj is string errMsg
                        ? errMsg
                        : "Error occurred while retrieving context providers";
                    this._parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, err);
                    message = err;
                }
            }
        }
    }
}
