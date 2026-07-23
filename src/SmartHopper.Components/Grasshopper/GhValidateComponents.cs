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
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GhJSON.Core;
using GhJSON.Core.Validation;
using Grasshopper.Kernel;
using SmartHopper.Core.ComponentBase;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Components.Grasshopper
{
    /// <summary>
    /// Grasshopper component that validates a GhJSON document against the official schema
    /// and performs structural checks.
    /// </summary>
    public class GhValidateComponents : AsyncComponentBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GhValidateComponents"/> class.
        /// </summary>
        public GhValidateComponents()
            : base(
                "Validate GhJSON",
                "GhValidate",
                "Validates a GhJSON document against the official schema and performs structural checks.",
                "SmartHopper",
                "Grasshopper")
        {
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("BBF48853-AC02-4786-B6A1-B74333DD1642");

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <inheritdoc/>
        protected override Bitmap Icon => null;

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("GhJSON", "J", "GhJSON document string to validate.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Level", "L", "Validation level: 0 = Minimal, 1 = Standard, 2 = Strict. Defaults to Standard.", GH_ParamAccess.item, 1);
            pManager.AddBooleanParameter("Prefer Online", "O", "Prefer downloading the schema from the official online repository.", GH_ParamAccess.item, false);
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Valid", "V", "Whether the GhJSON document is valid.", GH_ParamAccess.item);
            pManager.AddTextParameter("Errors", "E", "List of validation errors.", GH_ParamAccess.list);
            pManager.AddTextParameter("Warnings", "W", "List of validation warnings.", GH_ParamAccess.list);
            pManager.AddTextParameter("Info", "I", "List of informational messages.", GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new GhValidateWorker(this, this.AddRuntimeMessage);
        }

        private sealed class GhValidateWorker : AsyncWorkerBase
        {
            private string _json = string.Empty;
            private ValidationLevel _level = ValidationLevel.Standard;
            private bool _preferOnline = false;

            private bool _isValid;
            private List<string> _errors = new List<string>();
            private List<string> _warnings = new List<string>();
            private List<string> _info = new List<string>();

            public GhValidateWorker(
                GH_Component parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                string? json = null;
                if (!DA.GetData(0, ref json) || string.IsNullOrEmpty(json))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "GhJSON input is required");
                    dataCount = 0;
                    return;
                }

                this._json = json;

                int levelValue = 1;
                DA.GetData(1, ref levelValue);
                this._level = (ValidationLevel)Math.Max(0, Math.Min(2, levelValue));

                bool preferOnline = false;
                DA.GetData(2, ref preferOnline);
                this._preferOnline = preferOnline;

                dataCount = 1;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                try
                {
                    var result = await GhJson.ValidateAsync(this._json, this._level, schemaVersion: null, this._preferOnline, token).ConfigureAwait(false);

                    this._isValid = result.IsValid;
                    this._errors = result.Errors?.Select(e => e.ToString()).ToList() ?? new List<string>();
                    this._warnings = result.Warnings?.Select(w => w.ToString()).ToList() ?? new List<string>();
                    this._info = result.Info?.Select(i => i.ToString()).ToList() ?? new List<string>();

                    if (!this._isValid)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, $"GhJSON validation failed with {this._errors.Count} error(s)");
                    }
                    else if (this._warnings.Count > 0)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, $"GhJSON valid with {this._warnings.Count} warning(s)");
                    }

                    foreach (var error in this._errors)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Error, error);
                    }

                    foreach (var warning in this._warnings)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Warning, warning);
                    }

                    foreach (var message in this._info)
                    {
                        this.CollectMessage(SHRuntimeMessageSeverity.Info, message);
                    }
                }
                catch (Exception ex)
                {
                    this.CollectMessage(SHRuntimeMessageSeverity.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                DA.SetData(0, this._isValid);
                DA.SetDataList(1, this._errors);
                DA.SetDataList(2, this._warnings);
                DA.SetDataList(3, this._info);
                message = this._isValid ? "GhJSON is valid" : "GhJSON validation failed";
            }
        }
    }
}
