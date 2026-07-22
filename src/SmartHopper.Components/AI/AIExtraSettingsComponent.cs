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
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.ProviderSdk.AIProviders;

namespace SmartHopper.Components.AI
{
    /// <summary>
    /// Stateless component that dynamically generates provider-specific extra input parameters
    /// based on the selected AI provider's registered <see cref="AIExtraDescriptor"/> set.
    /// Output is a JSON object string to connect to the Extras input of <see cref="AISettingsComponent"/>.
    /// </summary>
    public class AIExtraSettingsComponent : ProviderComponentBase, IGH_VariableParameterComponent
    {
        /// <summary>Provider name for which the current params were built.</summary>
        private string _builtForProvider;

        private bool _infrastructureReady = false;
        private bool _retryScheduled = false;
        private System.Diagnostics.Stopwatch _retryStopwatch = new System.Diagnostics.Stopwatch();
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private const int MaxRetryDurationSeconds = 60;

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("8872AB8F-A76E-4FBB-96B8-1C3838D2C51B");

        /// <inheritdoc/>
        protected override Bitmap Icon => Resources.settingsextra;

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <inheritdoc/>
        public override IEnumerable<string> Keywords => new[] {
            "Extra Settings",
            "AI Extra Settings",
            "Advanced Settings",
            "Provider Settings",
            "Custom Settings",
        };

        /// <summary>Initializes a new instance of <see cref="AIExtraSettingsComponent"/>.</summary>
        public AIExtraSettingsComponent()
            : base(
                "Extra Settings",
                "Extras",
                "Generates provider-specific AI settings (service tier, reasoning effort, etc.) as a JSON extras object.\nConnect output to the Extras input of AI Settings component.\nRight-click to select provider.",
                "SmartHopper",
                "AI")
        {
            this._retryStopwatch.Start();
        }

        /// <summary>
        /// Checks if the plugin infrastructure is ready for processing.
        /// Readiness means the provider infrastructure has completed initialization,
        /// regardless of whether any providers were successfully discovered.
        /// </summary>
        private bool IsInfrastructureReady()
        {
            try
            {
                var manager = ProviderManager.Instance;
                return manager != null && manager.IsInfrastructureReady;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIExtraSettingsComponent] Infrastructure check failed: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public override void RemovedFromDocument(GH_Document document)
        {
            // Cancel any pending retry operations
            this._cancellationTokenSource?.Cancel();
            this._cancellationTokenSource?.Dispose();
            base.RemovedFromDocument(document);
        }

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Dynamic inputs are added in RebuildInputParams; no static inputs.
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Extras", "X", "Provider-specific settings as a JSON object. Connect to AI Settings component Extras input.", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Check if infrastructure is ready
            this._infrastructureReady = this.IsInfrastructureReady();

            if (!this._infrastructureReady)
            {
                // Check if we've exceeded the retry duration
                if (this._retryStopwatch.Elapsed.TotalSeconds > MaxRetryDurationSeconds)
                {
                    // Max retry duration exceeded - show error and stop trying
                    this.AddRuntimeMessage(
                        GH_RuntimeMessageLevel.Error,
                        "Plugin infrastructure failed to initialize within 60 seconds. Please restart Rhino and try again.");
                    DA.SetData("Extras", "{}");
                    return;
                }

                // Schedule retry only if not already scheduled
                if (!this._retryScheduled)
                {
                    this._retryScheduled = true;
                    this.ScheduleRetry();
                }

                // Return empty extras while waiting
                DA.SetData("Extras", "{}");
                return;
            }

            var effectiveProvider = this.GetActualAIProviderName();

            // Rebuild inputs if provider changed
            if (effectiveProvider != this._builtForProvider)
            {
                this.RebuildInputParams(effectiveProvider);

                // Expire solution to trigger recompute with new parameter structure.
                // DA was created for the old structure, so we must not continue using it.
                this.ExpireSolution(true);
                return;
            }

            var descriptors = ProviderManager.Instance.GetExtraDescriptors(effectiveProvider).ToList();
            var extras = new JObject();

            // Build name-to-index map from actual params to handle provider switching
            // where param order may not match descriptor order
            var paramIndexByName = this.Params.Input
                .Select((p, idx) => new { p.Name, Index = idx })
                .ToDictionary(x => x.Name, x => x.Index);

            foreach (var d in descriptors)
            {
                // Skip if no param exists with this descriptor's key name
                if (!paramIndexByName.TryGetValue(d.Key, out int paramIndex))
                {
                    continue;
                }

                if (d.Type == typeof(bool))
                {
                    bool val = false;
                    if (DA.GetData(paramIndex, ref val))
                    {
                        extras[d.Key] = val;
                    }
                }
                else if (d.Type == typeof(int))
                {
                    int val = 0;
                    if (DA.GetData(paramIndex, ref val))
                    {
                        extras[d.Key] = val;
                    }
                }
                else if (d.Type == typeof(double))
                {
                    double val = double.NaN;
                    if (DA.GetData(paramIndex, ref val) && !double.IsNaN(val))
                    {
                        extras[d.Key] = val;
                    }
                }
                else
                {
                    string val = null;
                    if (DA.GetData(paramIndex, ref val) && !string.IsNullOrEmpty(val))
                    {
                        extras[d.Key] = val;
                    }
                }
            }

            DA.SetData("Extras", extras.Count > 0 ? extras.ToString(Newtonsoft.Json.Formatting.None) : "{}");
        }

        // ─── IGH_VariableParameterComponent ────────────────────────────────────────

        /// <inheritdoc/>
        public bool CanInsertParameter(GH_ParameterSide side, int index) => false;

        /// <inheritdoc/>
        public bool CanRemoveParameter(GH_ParameterSide side, int index) => false;

        /// <inheritdoc/>
        public IGH_Param CreateParameter(GH_ParameterSide side, int index) => null;

        /// <inheritdoc/>
        public bool DestroyParameter(GH_ParameterSide side, int index) => true;

        /// <inheritdoc/>
        public void VariableParameterMaintenance()
        {
            // Params are rebuilt in SolveInstance/RebuildInputParams
        }

        // ─── Provider selection ────────────────────────────────────────────────────

        /// <summary>
        /// Called when the provider selection changes.
        /// </summary>
        protected override void OnProviderChanged()
        {
            this.RebuildInputParams(this.GetActualAIProviderName());
        }

        internal void RebuildInputParams(string providerName)
        {
            // Guard: don't attempt to rebuild if infrastructure isn't ready
            if (!this.IsInfrastructureReady())
            {
                Debug.WriteLine("[AIExtraSettingsComponent] Skipping RebuildInputParams - infrastructure not ready");
                return;
            }

            var descriptors = ProviderManager.Instance.GetExtraDescriptors(providerName).ToList();

            // Build target param names
            var targetNames = descriptors.Select(d => d.Key).ToHashSet();

            // Remove params that no longer belong to this provider
            // (keep params whose name still appears in descriptors to preserve wires)
            var toRemove = this.Params.Input
                .Where(p => !targetNames.Contains(p.Name))
                .ToList();
            foreach (var p in toRemove)
            {
                this.Params.UnregisterInputParameter(p, true);
            }

            // Add new params that don't exist yet
            var existingNames = this.Params.Input.Select(p => p.Name).ToHashSet();

            for (int i = 0; i < descriptors.Count; i++)
            {
                var d = descriptors[i];
                if (existingNames.Contains(d.Key)) continue;

                var param = this.CreateParamFromDescriptor(d);
                if (param != null)
                {
                    this.Params.RegisterInputParam(param, i);
                }
            }

            this._builtForProvider = providerName;
            this.Params.OnParametersChanged();
        }

        /// <summary>
        /// Schedules a retry by delaying 2 seconds and then expiring the solution.
        /// </summary>
        private void ScheduleRetry()
        {
            // Use Task.Delay with cancellation support and explicit scheduler
            System.Threading.Tasks.Task.Delay(2000, this._cancellationTokenSource.Token)
                .ContinueWith(_ =>
                {
                    if (this._cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        Rhino.RhinoApp.InvokeOnUiThread(() =>
                        {
                            try
                            {
                                // Only expire if the component is still in the document
                                if (this.OnPingDocument() != null && !this.Locked)
                                {
                                    this._retryScheduled = false;
                                    this.ExpireSolution(true);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[AIExtraSettingsComponent] Error during solution expiration: {ex.Message}");
                                this._retryScheduled = false;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AIExtraSettingsComponent] Error during retry scheduling: {ex.Message}");
                        this._retryScheduled = false;
                    }
                }, System.Threading.Tasks.TaskScheduler.Default);
        }

        private IGH_Param CreateParamFromDescriptor(AIExtraDescriptor d)
        {
            IGH_Param param = null;

            if (d.Type == typeof(bool))
            {
                var p = new Param_Boolean { Name = d.Key, NickName = d.Key, Description = d.Description, Access = GH_ParamAccess.item, Optional = true };
                if (d.DefaultValue is bool defBool)
                {
                    p.SetPersistentData(defBool);
                }

                param = p;
            }
            else if (d.Type == typeof(int))
            {
                var p = new Param_Integer { Name = d.Key, NickName = d.Key, Description = d.Description, Access = GH_ParamAccess.item, Optional = true };
                if (d.DefaultValue is int defInt)
                {
                    p.SetPersistentData(defInt);
                }

                param = p;
            }
            else if (d.Type == typeof(double))
            {
                var p = new Param_Number { Name = d.Key, NickName = d.Key, Description = d.Description, Access = GH_ParamAccess.item, Optional = true };
                if (d.DefaultValue is double defDouble)
                {
                    p.SetPersistentData(defDouble);
                }

                param = p;
            }
            else
            {
                var p = new Param_String { Name = d.Key, NickName = d.Key, Description = d.Description, Access = GH_ParamAccess.item, Optional = true };
                if (d.AllowedValues != null && d.AllowedValues.Length > 0)
                {
                    p.Description += $"\nAllowed values: {string.Join(", ", d.AllowedValues)}";
                }

                if (d.DefaultValue is string defStr)
                {
                    p.SetPersistentData(new GH_String(defStr));
                }

                param = p;
            }

            return param;
        }
    }
}
