/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;

namespace SmartHopper.Components.AI
{
    /// <summary>
    /// Grasshopper component for retrieving available AI models from the selected provider.
    /// </summary>
    public class AIModelsComponent : AIProviderComponentBase
    {
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("E5834FB2-4CC0-4D0A-8AB3-EF2345678901");

        /// <summary>
        /// Gets the icon for this component.
        /// </summary>
        protected override Bitmap Icon => Resources.aimodels;

        /// <summary>
        /// Gets the exposure level of this component in the ribbon.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Initializes a new instance of the AIModelsComponent class.
        /// </summary>
        public AIModelsComponent()
            : base(
                  "AI Models",
                  "AIModels",
                  "Retrieve the list of available models from the selected AI provider.",
                  "SmartHopper", "AI")
        {
            this.RunOnlyOnInputChanges = false;
        }

        /// <summary>
        /// Registers the output parameters for this component.
        /// </summary>
        /// <param name="pManager">The parameter manager.</param>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // Override to suppress the Metrics output
            pManager.AddTextParameter("Models", "M", "List of available model names from the selected provider", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers additional input parameters (required by StatefulAsyncComponentBase).
        /// </summary>
        /// <param name="pManager">The parameter manager to register inputs with.</param>
        protected override void RegisterAdditionalInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // No additional input parameters needed for this component
        }

        /// <summary>
        /// Registers additional output parameters (required by StatefulAsyncComponentBase).
        /// </summary>
        /// <param name="pManager">The parameter manager to register outputs with.</param>
        protected override void RegisterAdditionalOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // No additional output parameters needed for this component
        }

        /// <summary>
        /// Creates the async worker for this component.
        /// </summary>
        /// <param name="progressReporter">Progress reporter callback.</param>
        /// <returns>The async worker instance.</returns>
        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new AIModelsWorker(this, this.AddRuntimeMessage);
        }

        /// <summary>
        /// Async worker for the AI Models component.
        /// </summary>
        public class AIModelsWorker : AsyncWorkerBase
        {
            private readonly AIModelsComponent _parent;
            private readonly Dictionary<string, object> _result = new Dictionary<string, object>();

            public AIModelsWorker(AIModelsComponent parent, Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this._parent = parent;
            }

            /// <summary>
            /// Gathers input from the component.
            /// </summary>
            /// <param name="DA">Data access object.</param>
            /// <param name="message">Output message.</param>
            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                // No inputs to gather for this component
                dataCount = 0;
            }

            /// <summary>
            /// Performs the async work to retrieve available models.
            /// </summary>
            /// <param name="message">Output message.</param>
            /// <returns>Async task.</returns>
            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    // Get the current AI provider
                    var provider = this._parent.GetActualAIProvider();
                    if (provider == null)
                    {
                        this._result["Success"] = false;
                        this._result["Error"] = "No AI provider selected or available";
                        return;
                    }

                    // Retrieve available models
                    var models = await provider.Models.RetrieveAvailable().ConfigureAwait(false);
                    if (models == null || !models.Any())
                    {
                        this._result["Success"] = false;
                        this._result["Error"] = "No models available from the selected provider";
                        return;
                    }

                    // Convert to GH_Structure for output
                    var tree = new GH_Structure<GH_String>();
                    var path = new GH_Path(0);
                    foreach (var model in models)
                    {
                        tree.Append(new GH_String(model), path);
                    }

                    this._result["Models"] = tree;
                    this._result["Success"] = true;
                }
                catch (Exception ex)
                {
                    this._result["Success"] = false;
                    this._result["Error"] = ex.Message;
                }
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
                    if (this._result.TryGetValue("Models", out var models) && models is GH_Structure<GH_String> tree)
                    {
                        this._parent.SetPersistentOutput("Models", tree, DA);
                        message = "Models output set successfully";
                    }
                    else
                    {
                        message = "Error: No models data available";
                    }
                }
                else
                {
                    this._parent.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Error occurred while retrieving models");
                    message = "Error occurred while retrieving models";
                }
            }
        }
    }
}
