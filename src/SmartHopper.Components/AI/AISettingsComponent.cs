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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using Grasshopper.Kernel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Types;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Components.AI
{
    /// <summary>
    /// Stateless component that assembles an <see cref="AIRequestParameters"/> from
    /// cross-provider universal inputs and an optional Extras JSON object from
    /// <see cref="AIExtraSettingsComponent"/>.
    /// 
    /// Defers processing until plugin infrastructure is fully initialized to handle
    /// race conditions when files are opened directly from Windows Explorer.
    /// </summary>
    public class AISettingsComponent : GH_Component
    {
        private bool _infrastructureReady = false;
        private bool _initializationAttempted = false;
        private bool _retryScheduled = false;
        private const int MaxRetryAttempts = 30; // 30 * 2000ms = 60 seconds max
        private int _retryCount = 0;

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("133A3D6E-1DF0-4FD3-92B5-F686C2FD587D");

        /// <inheritdoc/>
        protected override Bitmap Icon => null;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>Initializes a new instance of <see cref="AISettingsComponent"/>.</summary>
        public AISettingsComponent()
            : base(
                "AI Settings",
                "AISett",
                "Assembles AI request settings (model, temperature, tokens, extras) to pass to any AI component.",
                "SmartHopper", "AI")
        {
        }

        /// <summary>
        /// Checks if the plugin infrastructure is ready for processing.
        /// </summary>
        private bool IsInfrastructureReady()
        {
            try
            {
                // Check if ProviderManager is initialized and has providers
                var manager = ProviderManager.Instance;
                return manager != null && manager.GetProviderCount() > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AISettingsComponent] Infrastructure check failed: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Model", "M", "AI model name override. Leave empty to use the provider default model.", GH_ParamAccess.item, string.Empty);
            pManager.AddIntegerParameter("Max Tokens", "Tok", "Maximum number of output tokens. Leave disconnected to use the global provider setting.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Temperature", "T", "Sampling temperature (0.0–2.0). Leave disconnected to use the global provider setting.\nSupported by OpenAI, Anthropic, MistralAI, DeepSeek.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Batch", "B", "When true, all AI calls in a single run are aggregated into one batch HTTP request (async, lower cost). Requires the active provider to support batch processing.", GH_ParamAccess.item, false);
            pManager.AddTextParameter("Extras", "X", "Provider-specific extra settings as a JSON object. Connect an AI Extra Settings component output here.", GH_ParamAccess.item, string.Empty);

            // All inputs are optional - use safe index access with bounds checking
            try
            {
                for (int i = 0; i < pManager.ParamCount; i++)
                {
                    pManager[i].Optional = true;
                }
            }
            catch (Exception ex)
            {
                // Silently handle any parameter manager issues during initialization
                System.Diagnostics.Debug.WriteLine($"[AISettingsComponent] Warning: Could not set parameter optional flags: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Settings", "S", "AI request settings. Connect to any AI component's Settings input.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            try
            {
                // Check infrastructure readiness
                if (!_initializationAttempted)
                {
                    _initializationAttempted = true;
                }

                // Re-check infrastructure status on every call until ready
                if (!_infrastructureReady)
                {
                    _infrastructureReady = IsInfrastructureReady();
                }

                // If not ready, schedule retry and return early without processing
                if (!_infrastructureReady)
                {
                    _retryCount++;
                    
                    if (_retryCount > MaxRetryAttempts)
                    {
                        // Max retries exceeded - show error and stop trying
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, 
                            "Plugin infrastructure failed to initialize. Please restart Rhino and try again.");
                        DA.SetData("Settings", new GH_AIRequestParameters(AIRequestParameters.Empty));
                        return;
                    }

                    if (!_retryScheduled)
                    {
                        _retryScheduled = true;
                        Debug.WriteLine($"[AISettingsComponent] Infrastructure not ready, scheduling retry #{_retryCount}");
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, 
                            "Plugin infrastructure is initializing. Retrying...");
                        
                        // Schedule retry with polling delay
                        System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ =>
                        {
                            try
                            {
                                Rhino.RhinoApp.InvokeOnUiThread(() =>
                                {
                                    _retryScheduled = false;

                                    // Expire solution to trigger another SolveInstance call
                                    this.ExpireSolution(true);
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[AISettingsComponent] Error during retry scheduling: {ex.Message}");
                                _retryScheduled = false;
                            }
                        });
                    }
                    
                    // Return early - do not process until ready
                    DA.SetData("Settings", new GH_AIRequestParameters(AIRequestParameters.Empty));
                    return;
                }

                // Reset retry count once ready
                _retryCount = 0;

                var builder = AIRequestParameters.Create();

                string model = null;
                if (DA.GetData("Model", ref model) && !string.IsNullOrWhiteSpace(model))
                {
                    builder.WithModel(model.Trim());
                }

                double temperature = double.NaN;
                if (DA.GetData("Temperature", ref temperature) && !double.IsNaN(temperature))
                {
                    builder.WithTemperature(temperature);
                }

                int maxTokens = 0;
                if (DA.GetData("Max Tokens", ref maxTokens) && maxTokens > 0)
                {
                    builder.WithMaxTokens(maxTokens);
                }

                string extrasJson = null;
                if (DA.GetData("Extras", ref extrasJson) && !string.IsNullOrWhiteSpace(extrasJson))
                {
                    try
                    {
                        var dict = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(extrasJson);
                        if (dict != null)
                        {
                            builder.WithExtras(new ReadOnlyDictionary<string, JToken>(dict));
                        }
                    }
                    catch (Exception ex)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Extras JSON is invalid and will be ignored: {ex.Message}");
                    }
                }

                bool batch = false;
                if (DA.GetData("Batch", ref batch) && batch)
                {
                    builder.WithBatchTier(true);
                }

                // int timeoutSeconds = 0;
                // if (DA.GetData("Timeout", ref timeoutSeconds) && timeoutSeconds > 0)
                // {
                //     builder.WithTimeout(timeoutSeconds);
                // }

                DA.SetData("Settings", new GH_AIRequestParameters(builder.Build()));
            }
            catch (IndexOutOfRangeException ex)
            {
                // Fallback for unexpected index errors during initialization
                Debug.WriteLine($"[AISettingsComponent] Index out of range: {ex.Message}");
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, 
                    "Unexpected initialization error. Please recompute the component.");
                DA.SetData("Settings", new GH_AIRequestParameters(AIRequestParameters.Empty));
            }
            catch (Exception ex)
            {
                // Catch any other unexpected errors
                Debug.WriteLine($"[AISettingsComponent] Unexpected error: {ex.Message}");
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, 
                    $"Unexpected error assembling AI settings: {ex.Message}");
                DA.SetData("Settings", new GH_AIRequestParameters(AIRequestParameters.Empty));
            }
        }
    }
}
